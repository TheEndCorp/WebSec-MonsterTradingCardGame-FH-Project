using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SemesterProjekt1
{
    class Program
    {
        private static HttpListener listener;
        private static ConcurrentDictionary<WebSocket, string?> _sockets = new ConcurrentDictionary<WebSocket, string?>();

        static async Task Main()
        {
            if (!IsAdministrator())
            {
                Console.WriteLine("Die Anwendung wird nicht im Administratormodus ausgeführt. Verwende 'localhost' als lokale IP-Adresse.");
            }

            string localIPAddress = IsAdministrator() ? GetLocalIPAddress() : "localhost";
            listener = new HttpListener();
            listener.Prefixes.Add($"http://{localIPAddress}:10000/");
            listener.Start();

            Console.WriteLine($"Server gestartet auf http://{localIPAddress}:10000/");
            PrintListeningAddresses();

            Console.WriteLine("WebSocket server started on ws://localhost:10000/lobby");
            UserServiceRequest requester = new UserServiceRequest();
            SocketRequester socketRequester = new SocketRequester(requester._userServiceHandler);


            while (true)
            {
                try
                {
                    HttpListenerContext context = await listener.GetContextAsync();

                    Console.WriteLine($"Received request for: {context.Request.Url}");

                    // Handle WebSocket requests asynchronously
                    if (context.Request.IsWebSocketRequest)
                    {
                        Console.WriteLine("WebSocket request detected.");
                        _ = Task.Run(() => HandleSocketRequestAsync(socketRequester, context));
                    }
                    else if (context.Request.Url?.AbsolutePath == "/login2")
                    {
                        Console.WriteLine("HTTP request for login page.");
                        HandleLoginRequest(context);
                    }
                    else
                    {
                        Console.WriteLine("HTTP request received.");
                        _ = Task.Run(() => HandleRequestAsync(requester, context));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting context: {ex.Message}");
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
        private static async Task HandleRequestAsync(UserServiceRequest requester, HttpListenerContext context)
        {
            await requester.HandleRequestAsync(context);
            await Task.Run(() => requester._userServiceHandler._databaseHandler.SaveUsers(requester._userServiceHandler._users));
            Console.WriteLine("Action");
        }

        private static async Task HandleSocketRequestAsync(SocketRequester socketRequester, HttpListenerContext context)
        {
            await socketRequester.HandleRequestAsync(context);
            await Task.Run(() => socketRequester._userServiceHandler._databaseHandler.SaveUsers(socketRequester._userServiceHandler._users));
            Console.WriteLine("Action");
        }




        private static void HandleLoginRequest(HttpListenerContext context)
        {
            string responseString = @"
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

            byte[] responseBuffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = responseBuffer.Length;
            context.Response.ContentType = "text/html";
            context.Response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
            context.Response.OutputStream.Close();
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
