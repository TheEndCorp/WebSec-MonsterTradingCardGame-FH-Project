using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.WebSockets;

namespace SemesterProjekt1
{
    class Program
    {
        private static TcpListener listener;
        private static ConcurrentDictionary<WebSocket, string?> _sockets = new ConcurrentDictionary<WebSocket, string?>();

        static async Task Main()
        {
            if (!IsAdministrator())
            {
                Console.WriteLine("Die Anwendung wird nicht im Administratormodus ausgeführt. Verwende 'localhost' als lokale IP-Adresse.");
            }

            string localIPAddress = IsAdministrator() ? GetLocalIPAddress() : "127.0.0.1";
            listener = new TcpListener(IPAddress.Parse(localIPAddress), 10000);
            listener.Start();

            Console.WriteLine($"Server gestartet auf http://{localIPAddress}:10000/");
            PrintListeningAddresses();

            UserServiceRequest requester = new UserServiceRequest();
            SocketRequester socketRequester = new SocketRequester(requester._userServiceHandler);

            while (true)
            {
                try
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client, requester, socketRequester));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }

        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void PrintListeningAddresses()
        {
            var host = Dns.GetHostName();
            var ipAddresses = Dns.GetHostAddresses(host);

            Console.WriteLine("Listening on the following addresses:");
            foreach (var ipAddress in ipAddresses)
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork || ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    Console.WriteLine($"http://{ipAddress}:10000/");
                }
            }
        }

        private static async Task HandleClientAsync(TcpClient client, UserServiceRequest requester, SocketRequester socketRequester)
        {
            try
            {
                using (var networkStream = client.GetStream())
                {
                    // Check if the stream is usable
                    if (networkStream.CanRead && networkStream.CanWrite)
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                        string requestText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.BackgroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine($"Received request:\n{requestText}");
                        Console.ResetColor();   
                        // Parse HTTP request
                        string[] requestLines = requestText.Split(new[] { "\r\n" }, StringSplitOptions.None);
                        string requestLine = requestLines[0];
                        string[] requestParts = requestLine.Split(' ');
                        if (requestParts.Length < 2)
                        {
                            return;
                        }

                        string method = requestParts[0];
                        string path = requestParts[1];

                        // Handle WebSocket upgrade or HTTP request based on path
                        if (requestText.Contains("Upgrade: websocket"))
                        {
                            Console.WriteLine("WebSocket request detected.");
                            await HandleSocketRequestAsync(socketRequester, client, path);
                        }
                        else if (path == "/login2")
                        {
                            Console.WriteLine("HTTP request for login page.");
                            await SendHttpResponseAsync(networkStream, GenerateLoginPageHtml());
                        }
                        else if (path == "/login3")
                        {
                            Console.WriteLine("HTTP request for fight lobby.");
                            await SendHttpResponseAsync(networkStream, GenerateFightLobbyHtml());
                        }
                        else
                        {
                            Console.WriteLine("Generic HTTP request received.");
                            await HandleRequestAsync(requester, client, method, path);
                        }
                    }
                    else
                    {
                        Console.WriteLine("NetworkStream is not readable or writable.");
                    }
                }
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"IO Error handling client: {ioEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }

        }






        private static async Task HandleRequestAsync(UserServiceRequest requester, TcpClient client, string method, string path)
        {
            try
            {

                var networkStream = client.GetStream();
                if (networkStream.CanRead && networkStream.CanWrite)
                {



                    await requester.HandleRequestAsync(client, method, path);
                    await Task.Run(() => requester._userServiceHandler._databaseHandler.SaveUsers(requester._userServiceHandler._users));
                    Console.WriteLine("Action");
                }
            
            else Console.WriteLine("PAIN");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling request: {ex.Message}");
            }
        }

        private static async Task HandleSocketRequestAsync(SocketRequester socketRequester, TcpClient client, string path)
        {
            await socketRequester.HandleRequestAsync(path,client);
            await Task.Run(() => socketRequester._userServiceHandler._databaseHandler.SaveUsers(socketRequester._userServiceHandler._users));
            Console.WriteLine("Action");
        }









        private static async Task SendHttpResponseAsync(NetworkStream networkStream, string content, string contentType = "text/html")
        {
            string response = $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {Encoding.UTF8.GetByteCount(content)}\r\n\r\n{content}";
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await networkStream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private static string GenerateLoginPageHtml()
        {
            return @"
                <html>
                    <head>
                        <title>WebSocket Lobby</title>
                        <script>
                            var socket;
                            function connect() {
                                socket = new WebSocket('ws://' + window.location.host + '/lobby2');
                                socket.onopen = function() {
                                    console.log('Connected to WebSocket server');
                                };
                                socket.onmessage = function(event) {
                                    var messages = document.getElementById('messages');
                                    messages.value += event.data + '\\n';
                                };
                                socket.onclose = function() {
                                    console.log('Disconnected from WebSocket server');
                                };
                            }

                            function sendMessage() {
                                var messageInput = document.getElementById('messageInput');
                                if (messageInput.value.trim() !== '') {
                                    socket.send(messageInput.value);
                                    messageInput.value = '';
                                }
                            }
                        </script>
                    </head>
                    <body>
                        <h1>WebSocket Lobby</h1>
                        <button onclick='connect()'>Connect to WebSocket</button>
                        <br/><br/>
                        <textarea id='messages' rows='10' cols='50' readonly></textarea>
                        <br/>
                        <input type='text' id='messageInput' placeholder='Enter message...' />
                        <button onclick='sendMessage()'>Send</button>
                    </body>
                </html>";
        }

        private static string GenerateFightLobbyHtml()
        {
            return @"
            <!DOCTYPE html>
            <html lang='en'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Fight Lobby</title>
                    <script>
                        let socket;
                        let rematchTimeout;

                        function connect() {
                            socket = new WebSocket('ws://' + window.location.host + '/lobby3');
                            socket.onopen = function() {
                                console.log('Connected to WebSocket for fight.');
                                document.getElementById('log').innerHTML = '<p>Waiting for an opponent...</p>';
                            };
                            socket.onmessage = function(event) {
                                const logDiv = document.getElementById('log');
                                const message = event.data;
                                logDiv.innerHTML += `<p>${message}</p>`;
                                if (message.includes('Kampf beendet!')) {
                                    document.getElementById('rematchButton').style.display = 'block';
                                    rematchTimeout = setTimeout(() => {
                                        document.getElementById('rematchButton').style.display = 'none';
                                        socket.close();
                                    }, 20000);
                                }
                            };
                            socket.onclose = function() {
                                window.location.href = '/'; // Redirect to home page
                            };
                        }

                        function rematch() {
                            clearTimeout(rematchTimeout);
                            socket.send('rematch');
                            document.getElementById('log').innerHTML = '';
                            document.getElementById('rematchButton').style.display = 'none';
                        }
                    </script>
                </head>
                <body onload='connect()'>
                    <h1>Fight Lobby</h1>
                    <div id='log'></div>
                    <button id='rematchButton' style='display:none;' onclick='rematch()'>Rematch</button>
                </body>
            </html>";
        }

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("Keine IPv4-Adresse im lokalen Netzwerk gefunden.");
        }
    }
}
