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
                    HttpListenerResponse response = context.Response;

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
                    else if (context.Request.Url?.AbsolutePath == "/login3")
                    {
                        Console.WriteLine("HTTP request for login page.");
                        HandleLoginRequest2(context);

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



        /*
                private static void LOBBYFIGHT(SocketRequester socketRequester,HttpListenerContext context)
                {
                    // Cookie aus der Anfrage lesen
                    var userDataCookie = context.Request.Cookies["userData"]?.Value;
                    if (userDataCookie == null)
                    {
                        // Wenn kein Cookie vorhanden ist, eine Fehlermeldung senden
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        byte[] errorResponse = Encoding.UTF8.GetBytes("Unauthorized: No UserId cookie found.");
                        context.Response.OutputStream.Write(errorResponse, 0, errorResponse.Length);
                        context.Response.OutputStream.Close();
                        return;
                    }


                    var userData = System.Web.HttpUtility.ParseQueryString(userDataCookie);
                    string username = userData["username"];
                    string password = userData["password"];
                    string userIdString = userData["userid"];
                    int userId = int.Parse(userIdString);

                    var user = socketRequester._userServiceHandler.AuthenticateUser(username, password);



                    // Benutzer-ID aus dem Cookie extrahieren


                    // Hier kannst du die Benutzer-ID verwenden, um den Benutzer zu identifizieren
                    Console.WriteLine($"Benutzer-ID aus Cookie: {userId}");

                    string responseString = @"
                <!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Fight Lobby</title>
                </head>
                <body>
                    <h1>Fight Lobby</h1>
                    <div id='log'></div>
                    <button id='rematchButton' style='display:none;'>Rematch</button>

                    <script>
                        const logDiv = document.getElementById('log');
                        const rematchButton = document.getElementById('rematchButton');
                        const socket = new WebSocket('ws://' + window.location.host + '/lobby3');

                        socket.onmessage = function(event) {
                            const message = event.data;
                            logDiv.innerHTML += `<p>${message}</p>`;
                            if (message.includes('Kampf beendet!')) {
                                rematchButton.style.display = 'block';
                            }
                        };

                        rematchButton.onclick = function() {
                            socket.send('rematch');
                            rematchButton.style.display = 'none';
                            logDiv.innerHTML = '';
                        };
                    </script>
                </body>
                </html>
                ";
                    byte[] responseBuffer = Encoding.UTF8.GetBytes(responseString);
                    context.Response.ContentLength64 = responseBuffer.Length;
                    context.Response.ContentType = "text/html";
                    context.Response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
                    context.Response.OutputStream.Close();
                }

                */

        private static void LOBBYFIGHT(SocketRequester socketRequester, HttpListenerContext context)
        {
            // Existing code to retrieve user data
            var userDataCookie = context.Request.Cookies["userData"]?.Value;
            var userData = System.Web.HttpUtility.ParseQueryString(userDataCookie);
            string username = userData["username"];
            string password = userData["password"];
            string userIdString = userData["userid"];
            int userId = int.Parse(userIdString);

            // Authenticate the user
            var user = socketRequester._userServiceHandler.AuthenticateUser(username, password);
            if (user == null)
            {
                // Handle unauthorized
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            // Connect to the fight WebSocket with user data
            _ = socketRequester.HandleWebSocketConnectionFight(context,user);
        }


        private static void HandleLoginRequest2(HttpListenerContext context)
        {
            string responseString = @"
    <!DOCTYPE html>
    <html lang='en'>
        <head>
            <meta charset='UTF-8'>
            <title>Fight Lobby</title>
            <script>
                let socket;
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
                        if (message.includes('Fight over')) {
                            document.getElementById('rematchButton').style.display = 'block';
                        }
                    };
                    socket.onclose = function() {
                        console.log('Disconnected from WebSocket.');
                    };
                }

                function rematch() {
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

            byte[] responseBuffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = responseBuffer.Length;
            context.Response.ContentType = "text/html";
            context.Response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
            context.Response.OutputStream.Close();
        }







    }
}
