using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace SemesterProjekt1
{
    public class SocketRequester
    {
        public UserServiceHandler _userServiceHandler { get; set; }
        private static ConcurrentDictionary<WebSocket, string?> ConnectedSockets = new ConcurrentDictionary<WebSocket, string?>();
        private static ConcurrentDictionary<string, WebSocket> LobbySockets = new ConcurrentDictionary<string, WebSocket>();
        private static ConcurrentDictionary<int, WebSocket> LobbyUserSockets = new ConcurrentDictionary<int, WebSocket>();
        private static int RematchCount = 0;
        private static SemaphoreSlim LobbySemaphore = new SemaphoreSlim(2, 2);

        public SocketRequester(UserServiceHandler userServiceHandler)
        {
            _userServiceHandler = userServiceHandler;
        }

        public static async Task BroadcastMessage(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var tasks = ConnectedSockets.Keys
                .Where(socket => socket.State == WebSocketState.Open)
                .Select(socket => socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None))
                .ToList();

            await Task.WhenAll(tasks);
        }

        public static async Task HandleWebSocketConnection(HttpListenerContext context)
        {
            try
            {
                var webSocketContext = await context.AcceptWebSocketAsync(null);
                var webSocket = webSocketContext.WebSocket;

                if (ConnectedSockets.TryAdd(webSocket, null))
                {
                    Console.WriteLine($"User connected to lobby. Total users: {ConnectedSockets.Count}");
                    await ReceiveMessages(webSocket);
                }
                else
                {
                    Console.WriteLine("Failed to add WebSocket to the dictionary.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling WebSocket connection: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        public static async Task ReceiveMessages(WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"Received: {message}");
                        await BroadcastMessage(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
            }
            finally
            {
                if (ConnectedSockets.TryRemove(webSocket, out _))
                {
                    Console.WriteLine($"User disconnected from lobby. Total users: {ConnectedSockets.Count}");
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
        }

        public async Task ReceiveRematchMessages(WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            var rematchTimer = new CancellationTokenSource();

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"Received: {message}");

                        if (message == "rematch")
                        {
                            Interlocked.Increment(ref RematchCount);
                            if (RematchCount == 2)
                            {
                                RematchCount = 0;
                                rematchTimer.Cancel();
                                await StartRematch();
                            }
                            else
                            {
                                rematchTimer.Cancel();
                                rematchTimer = new CancellationTokenSource();
                                _ = Task.Delay(TimeSpan.FromSeconds(20), rematchTimer.Token).ContinueWith(async t =>
                                {
                                    if (!t.IsCanceled)
                                    {
                                        await DisconnectUsers();
                                    }
                                });
                            }
                        }
                        else
                        {
                            await BroadcastMessage(message);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving rematch message: {ex.Message}");
            }
            finally
            {
                if (ConnectedSockets.TryRemove(webSocket, out _))
                {
                    Console.WriteLine($"User disconnected from lobby. Total users: {ConnectedSockets.Count}");
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
        }

        private async Task DisconnectUsers()
        {
            foreach (var socket in LobbySockets.Values)
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "No rematch request received", CancellationToken.None);
                }
            }

            LobbySockets.Clear();
            LobbyUserSockets.Clear();
            Console.WriteLine("Users disconnected due to no rematch request.");
        }

        private async Task StartRematch()
        {
            var users = LobbyUserSockets.Keys.ToList();
            var sockets = LobbySockets.Values.ToList();

            User user1 = _userServiceHandler.GetUserById(users[0]);
            User user2 = _userServiceHandler.GetUserById(users[1]);
            var fightLogic = new FightLogic(user1, user2);
            await fightLogic.StartBattleAsync(sockets[0], sockets[1]);

            await BroadcastMessage("Log cleared for rematch.");
        }

        public async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                if (context.Request.IsWebSocketRequest && context.Request.Url?.AbsolutePath == "/lobby2")
                {
                    Console.WriteLine("Lobby 2 WebSocket request detected.");
                    _ = HandleWebSocketConnection(context);
                }
                else if (context.Request.IsWebSocketRequest && context.Request.Url?.AbsolutePath == "/lobby3")
                {
                    Console.WriteLine("Lobby 3 WebSocket request detected.");
                    await HandleLobbyFight(context);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                SendResponse(context.Response, $"Error: {ex.Message}\n{ex.StackTrace}", "text/plain");
            }
        }

        public void SendResponse(HttpListenerResponse response, string content, string contentType)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            response.ContentType = contentType;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        public async Task HandleLobbyFight(HttpListenerContext context)
        {
            var user = AuthenticateUserFromCookie(context);
            if (user == null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            await HandleWebSocketFightConnection(context, user);
        }

        private async Task HandleWebSocketFightConnection(HttpListenerContext context, User user)
        {
            if (LobbySockets.Count >= 2)
            {
                var disconnectedUsers = LobbyUserSockets.Where(kvp => kvp.Value.State != WebSocketState.Open).Select(kvp => kvp.Key).ToList();
                foreach (var userId in disconnectedUsers)
                {
                    LobbyUserSockets.TryRemove(userId, out _);
                }

                var disconnectedSockets = LobbySockets.Where(kvp => kvp.Value.State != WebSocketState.Open).Select(kvp => kvp.Key).ToList();
                foreach (var socketKey in disconnectedSockets)
                {
                    LobbySockets.TryRemove(socketKey, out _);
                }
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.Close();
                Console.Write(LobbySockets.Count);
                return;
            }

           

            try
            {
                var webSocketContext = await context.AcceptWebSocketAsync(null);
                var webSocket = webSocketContext.WebSocket;

                if (LobbySockets.TryAdd(context.Request.RemoteEndPoint.ToString(), webSocket))
                {
                    LobbyUserSockets.TryAdd(user.Id, webSocket);
                    Console.WriteLine($"User connected to fight lobby. Total users: {LobbySockets.Count}");

                    if (LobbySockets.Count == 2)
                    {
                        var users = LobbyUserSockets.Keys.ToList();
                        var sockets = LobbySockets.Values.ToList();

                        User user1 = _userServiceHandler.GetUserById(users[0]);
                        User user2 = _userServiceHandler.GetUserById(users[1]);
                        var fightLogic = new FightLogic(user1, user2);
                        await fightLogic.StartBattleAsync(sockets[0], sockets[1]);
                    }
                    else
                    {
                        await ReceiveRematchMessages(webSocket);
                    }
                }
                else
                {
                    Console.WriteLine("Failed to add WebSocket to lobby.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling WebSocket connection for fight lobby: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        private User AuthenticateUserFromCookie(HttpListenerContext context)
        {
            var userDataCookie = context.Request.Cookies["userData"]?.Value;
            var userData = System.Web.HttpUtility.ParseQueryString(userDataCookie);
            string username = userData["username"];
            string password = userData["password"];
            string userIdString = userData["userid"];
            int userId = int.Parse(userIdString);

            return _userServiceHandler.AuthenticateUser(username, password);
        }
    }
}
