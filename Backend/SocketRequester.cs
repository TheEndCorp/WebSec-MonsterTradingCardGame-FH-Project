using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace SemesterProjekt1
{
    public class SocketRequester
    {
        public UserServiceHandler _userServiceHandler { get; set; }
        private static ConcurrentDictionary<WebSocket, string?> _sockets = new ConcurrentDictionary<WebSocket, string?>();
        private static ConcurrentDictionary<string, WebSocket> _lobbySockets = new ConcurrentDictionary<string, WebSocket>();
        private static ConcurrentDictionary<int, WebSocket> _lobbySocketsUser = new ConcurrentDictionary<int, WebSocket>();

        private static SemaphoreSlim _lobbySemaphore = new SemaphoreSlim(2, 2);


        public SocketRequester(UserServiceHandler userServiceHandlerFROMServiceRequest)
        {
            this._userServiceHandler = userServiceHandlerFROMServiceRequest;
        }




        public static async Task BroadcastMessage(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var tasks = new List<Task>();

            foreach (var socket in _sockets.Keys)
            {
                if (socket.State == WebSocketState.Open)
                {
                    tasks.Add(socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None));
                }
            }

            // Wait for all send operations to complete
            await Task.WhenAll(tasks);
        }



        public static async Task HandleWebSocketConnection(HttpListenerContext context)
        {
            try
            {
                var webSocketContext = await context.AcceptWebSocketAsync(null);
                var webSocket = webSocketContext.WebSocket;

                // Add the new socket to the dictionary
                if (_sockets.TryAdd(webSocket, null))
                {
                    Console.WriteLine("User connected to lobby. Total users: " + _sockets.Count);
                    await ReceiveMessages(webSocket);
                }
                else
                {
                    Console.WriteLine("Failed to add WebSocket to the dictionary. It might already exist.");
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
                        break; // Exit the loop on close message
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
            }
            finally
            {
                // Remove the socket from the dictionary upon disconnection
                if (_sockets.TryRemove(webSocket, out _))
                {
                    Console.WriteLine("User disconnected from lobby. Total users: " + _sockets.Count);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
        }


        public async Task HandleRequestAsync(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            try
            {
                if (context.Request.IsWebSocketRequest && context.Request.Url?.AbsolutePath == "/lobby2")
                {
                    Console.WriteLine("WebSocket request detected.");
                    _ = HandleWebSocketConnection(context); // Fire-and-forget to handle multiple connections
                }
                if (context.Request.IsWebSocketRequest && context.Request.Url?.AbsolutePath == "/lobby3")
                {
                    Console.WriteLine("WebSocket request detected.");
                    LOBBYFIGHT(context);
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Close();
                }
                
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                string errorMessage = $"Error: {ex.Message}\n{ex.StackTrace}";
                SendResponse(response, errorMessage, "text/plain");
            }
        }

 
        public void SendResponse(HttpListenerResponse response, string content, string contentType)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            response.ContentType = contentType;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }


        public async Task HandleWebSocketConnectionFight(HttpListenerContext context, User user)
        {
            try
            {
                if (!context.Request.Headers["Connection"].Contains("Upgrade") || !context.Request.Headers["Upgrade"].Contains("websocket"))
                {
                    throw new InvalidOperationException("The request does not contain the required WebSocket headers.");
                }

                var webSocketContext = await context.AcceptWebSocketAsync(null);
                var webSocket = webSocketContext.WebSocket;

                // Add the new socket to the dictionary
                if (_lobbySockets.TryAdd(context.Request.RemoteEndPoint.ToString(), webSocket))
                {
                    _lobbySocketsUser.TryAdd(user.Id, webSocket);
                    Console.WriteLine("User connected to lobby. Total users: " + _lobbySockets.Count);

                    if (_lobbySockets.Count == 2)
                    {
                        // Start the fight when two users are connected
                        var users = _lobbySocketsUser.Keys.ToList();
                        var users2 = _lobbySockets.Values.ToList();

                        User user1 = _userServiceHandler.GetUserById(users[0]);
                        User user2 = _userServiceHandler.GetUserById(users[1]);
                        var fightLogic = new FightLogic(user1, user2);
                        await fightLogic.StartBattleAsync(users2[0], users2[1]);
                    }
                    else
                    {
                        await ReceiveMessages(webSocket);
                    }
                }
                else
                {
                    Console.WriteLine("Failed to add WebSocket to the dictionary. It might already exist.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling WebSocket connection: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }



        private void LOBBYFIGHT( HttpListenerContext context)
        {
            // Existing code to retrieve user data
            var userDataCookie = context.Request.Cookies["userData"]?.Value;
            var userData = System.Web.HttpUtility.ParseQueryString(userDataCookie);
            string username = userData["username"];
            string password = userData["password"];
            string userIdString = userData["userid"];
            int userId = int.Parse(userIdString);

            // Authenticate the user
            var user = _userServiceHandler.AuthenticateUser(username, password);
            if (user == null)
            {
                // Handle unauthorized
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            // Connect to the fight WebSocket with user data
            _ = HandleWebSocketConnectionFight(context, user);
        }


















    }
}