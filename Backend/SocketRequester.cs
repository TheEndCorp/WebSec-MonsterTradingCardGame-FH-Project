using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace SemesterProjekt1
{
    public class SocketRequester
    {
        private static ConcurrentDictionary<WebSocket, string?> ConnectedSockets = new ConcurrentDictionary<WebSocket, string?>();
        private static ConcurrentDictionary<string, WebSocket> LobbySockets = new ConcurrentDictionary<string, WebSocket>();
        private static ConcurrentDictionary<int, WebSocket> LobbyUserSockets = new ConcurrentDictionary<int, WebSocket>();
        private static int RematchCount = 0;
        private static SemaphoreSlim LobbySemaphore = new SemaphoreSlim(2, 2);
        public UserServiceHandler _userServiceHandler { get; set; }

        public SocketRequester(UserServiceHandler userServiceHandler)
        {
            _userServiceHandler = userServiceHandler;
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

        private async Task HandleWebSocketFightConnection(TcpClient client, User user)
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

                SendResponse(client, "403 Forbidden", "text/plain");
                Console.WriteLine("Lobby is full, connection denied.");
                return;
            }

            try
            {
                var stream = client.GetStream();
                using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
                using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    // 1. Read and process the WebSocket handshake request
                    var requestLine = await reader.ReadLineAsync();
                    var headers = new Dictionary<string, string>();

                    while (!string.IsNullOrEmpty(requestLine))
                    {
                        var parts = requestLine.Split(':');
                        if (parts.Length == 2)
                        {
                            headers[parts[0].Trim()] = parts[1].Trim();
                        }
                        requestLine = await reader.ReadLineAsync();
                    }

                    if (headers.TryGetValue("Sec-WebSocket-Key", out var webSocketKey))
                    {
                        // 2. Respond with WebSocket handshake
                        string acceptKey = Convert.ToBase64String(
                            System.Security.Cryptography.SHA1.Create()
                                .ComputeHash(Encoding.UTF8.GetBytes(webSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"))
                        );

                        await writer.WriteAsync(
                            "HTTP/1.1 101 Switching Protocols\r\n" +
                            "Connection: Upgrade\r\n" +
                            "Upgrade: websocket\r\n" +
                            $"Sec-WebSocket-Accept: {acceptKey}\r\n" +
                            "\r\n"
                        );
                        await writer.FlushAsync();

                        // 3. Create WebSocket from the TCP stream
                        var webSocket = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, TimeSpan.FromSeconds(30));

                        // Add WebSocket to lobby dictionaries
                        if (LobbySockets.TryAdd(client.Client.RemoteEndPoint.ToString(), webSocket))
                        {
                            LobbyUserSockets.TryAdd(user.Id, webSocket);
                            Console.WriteLine($"User connected to fight lobby. Total users: {LobbySockets.Count}");

                            if (LobbySockets.Count == 2)
                            {
                                var users = LobbyUserSockets.Keys.ToList();
                                var sockets = LobbySockets.Values.ToList();

                                // Start fight between two users
                                User user1 = _userServiceHandler.GetUserById(users[0]);
                                User user2 = _userServiceHandler.GetUserById(users[1]);
                                var fightLogic = new FightLogic(user1, user2);
                                await fightLogic.StartBattleAsync(sockets[0], sockets[1]);
                            }
                            else
                            {
                                // Wait for rematch messages if only one user in the lobby
                                await ReceiveRematchMessages(webSocket);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Failed to add WebSocket to lobby.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling WebSocket connection for fight lobby: {ex.Message}");
                client.Close();
            }
        }

        private User AuthenticateUserFromCookie(TcpClient client)
        {
            var stream = client.GetStream();
            var reader = new StreamReader(stream);
            var requestLine = reader.ReadLine();
            var headers = new Dictionary<string, string>();

            string line;
            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                var parts = line.Split(':');
                if (parts.Length == 2)
                {
                    headers[parts[0].Trim()] = parts[1].Trim();
                }
            }

            if (!headers.TryGetValue("Cookie", out var cookieHeader))
            {
                return null;
            }

            var cookies = cookieHeader.Split(';');
            var userDataCookie = cookies.FirstOrDefault(c => c.Trim().StartsWith("userData="))?.Substring("userData=".Length);
            if (userDataCookie == null)
            {
                return null;
            }

            var userData = System.Web.HttpUtility.ParseQueryString(userDataCookie);
            string username = userData["username"];
            string password = userData["password"];
            string userIdString = userData["userid"];
            int userId = int.Parse(userIdString);

            return _userServiceHandler.AuthenticateUser(username, password);
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

        public static async Task HandleWebSocketConnection(TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
                using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    // 1. Read handshake request
                    var requestLine = await reader.ReadLineAsync();
                    var headers = new Dictionary<string, string>();

                    while (!string.IsNullOrEmpty(requestLine))
                    {
                        var parts = requestLine.Split(':');
                        if (parts.Length == 2)
                        {
                            headers[parts[0].Trim()] = parts[1].Trim();
                        }
                        requestLine = await reader.ReadLineAsync();
                    }

                    if (headers.TryGetValue("Sec-WebSocket-Key", out var webSocketKey))
                    {
                        // 2. Perform WebSocket handshake
                        string acceptKey = Convert.ToBase64String(
                            System.Security.Cryptography.SHA1.Create()
                                .ComputeHash(Encoding.UTF8.GetBytes(webSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"))
                        );

                        // Write response
                        await writer.WriteAsync(
                            "HTTP/1.1 101 Switching Protocols\r\n" +
                            "Connection: Upgrade\r\n" +
                            "Upgrade: websocket\r\n" +
                            $"Sec-WebSocket-Accept: {acceptKey}\r\n" +
                            "\r\n"
                        );
                        await writer.FlushAsync();

                        // 3. Create WebSocket and begin handling messages
                        var webSocket = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, TimeSpan.FromSeconds(30));
                        if (ConnectedSockets.TryAdd(webSocket, null))
                        {
                            Console.WriteLine($"User connected to lobby. Total users: {ConnectedSockets.Count}");
                            await ReceiveMessages(webSocket);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling WebSocket connection: {ex.Message}");
                client.Close();
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

        public async Task HandleRequestAsync(string path, TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                var reader = new StreamReader(stream);
                var requestLine = await reader.ReadLineAsync();
                var requestParts = requestLine.Split(' ');

                if (requestParts.Length < 3)
                {
                    client.Close();
                    return;
                }

                var method = requestParts[0];
                var requestPath = requestParts[1];

                if (method == "GET" && requestPath == "/lobby2")
                {
                    Console.WriteLine("Lobby 2 WebSocket request detected.");
                    _ = HandleWebSocketConnection(client);
                }
                else if (method == "GET" && requestPath == "/lobby3")
                {
                    Console.WriteLine("Lobby 3 WebSocket request detected.");
                    await HandleLobbyFight(client);
                }
                else
                {
                    SendResponse(client, "404 Not Found", "text/plain");
                }
            }
            catch (Exception ex)
            {
                SendResponse(client, $"Error: {ex.Message}\n{ex.StackTrace}", "text/plain");
            }
        }

        public void SendResponse(TcpClient client, string content, string contentType)
        {
            var response = $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {content.Length}\r\n\r\n{content}";
            var buffer = Encoding.UTF8.GetBytes(response);
            client.GetStream().Write(buffer, 0, buffer.Length);
            client.Close();
        }

        public async Task HandleLobbyFight(TcpClient client)
        {
            var user = AuthenticateUserFromCookie(client);
            if (user == null)
            {
                SendResponse(client, "401 Unauthorized", "text/plain");
                return;
            }

            await HandleWebSocketFightConnection(client, user);
        }
    }
}