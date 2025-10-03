using System.Configuration;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

//using System.Net.WebSockets;
using System.Security.Principal;
using System.Text;

namespace SemesterProjekt1
{
    internal class Program
    {
        private static TcpListener listener;
        private static X509Certificate2 serverCertificate;

        private static async Task Main()
        {
            bool isAdmin = IsAdministrator();
            if (!isAdmin)
            {
                Console.WriteLine("Die Anwendung wird nicht im Administratormodus ausgeführt. Verwende 'localhost' als lokale IP-Adresse.");
            }

            string localIPAddress = isAdmin ? GetLocalIPAddress() : "127.0.0.1";
            listener = new TcpListener(IPAddress.Parse(localIPAddress), 10001);
            listener.Start();
            string certificatePath = ConfigurationManager.AppSettings["CERTIFICATE_PATH"] ??
                                      Environment.GetEnvironmentVariable("CERTIFICATE_PATH");

            string certificatePassword = ConfigurationManager.AppSettings["CERTIFICATE_PASSWORD"] ?? Environment.GetEnvironmentVariable("CERTIFICATE_PASSWORD");

            if (certificatePath == null || certificatePassword == null)
            {
                Console.WriteLine("Zertifikatspfad oder Passwort ist nicht konfiguriert.");
                return;
            }

            // Laden Sie Ihr SSL-Zertifikat
            serverCertificate = new X509Certificate2(certificatePath, certificatePassword);

            Console.WriteLine($"Server gestartet auf https://{localIPAddress}:10001/");

            UserServiceRequest requester = new UserServiceRequest();

            while (true)
            {
                try
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client, requester));
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

        private static async Task HandleClientAsync(TcpClient client, UserServiceRequest requester)
        {
            try
            {
                using (var networkStream = client.GetStream())
                using (var sslStream = new SslStream(networkStream, true))
                {
                    try
                    {
                        await sslStream.AuthenticateAsServerAsync(
    serverCertificate,
    clientCertificateRequired: false,
    enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13,
    checkCertificateRevocation: true);

                        if (sslStream.CanRead && sslStream.CanWrite)
                        {
                            using (MemoryStream memoryStream = new MemoryStream())
                            {
                                byte[] buffer = new byte[4096];
                                int bytesRead;

                                bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length);

                                if (bytesRead > 0)
                                {
                                    await memoryStream.WriteAsync(buffer, 0, bytesRead);
                                    memoryStream.Position = 0;

                                    string requestText = Encoding.UTF8.GetString(memoryStream.ToArray(), 0, (int)memoryStream.Length);

                                    Console.BackgroundColor = ConsoleColor.DarkBlue;
                                    Console.WriteLine($"Received request:\n{requestText}");
                                    Console.ResetColor();

                                    string[] requestLines = requestText.Split(new[] { "\r\n\r\n" }, 2, StringSplitOptions.None);
                                    if (requestLines.Length < 2)
                                    {
                                        SendErrorResponse(sslStream);
                                        return;
                                    }

                                    string requestLine = requestLines[0];
                                    string[] requestParts = requestLine.Split(' ');

                                    if (requestParts.Length < 2)
                                    {
                                        SendErrorResponse(sslStream);
                                        return;
                                    }

                                    string method = requestParts[0];
                                    string path = requestParts[1];

                                    if (!IsValidHttpMethod(method) || !IsValidPath(path))
                                    {
                                        SendErrorResponse(sslStream);
                                        return;
                                    }

                                    Console.WriteLine("Generic HTTP request received.");
                                    await HandleRequestAsync(memoryStream, sslStream, method, path, requester);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("SslStream is not readable or writable.");
                        }
                    }
                    catch (AuthenticationException e)
                    {
                        // Console.WriteLine($"Authentication failed: {e.Message}");
                        if (e.InnerException != null)
                        {
                            //   Console.WriteLine($"Inner exception: {e.InnerException.Message}");
                        }
                        client.Close();
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

        private static bool ValidateClientCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            Console.WriteLine($"Certificate error: {sslPolicyErrors}");
            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
            {
                foreach (X509ChainStatus status in chain.ChainStatus)
                {
                    Console.WriteLine($"Chain error: {status.StatusInformation}");
                }
            }

            return false;
        }

        private static bool IsValidHttpMethod(string method)
        {
            string[] validMethods = { "GET", "POST", "PUT", "DELETE", "OPTIONS", "HEAD" };
            return validMethods.Contains(method.ToUpper());
        }

        private static bool IsValidPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.Length <= 2048 && !path.Contains("..");
        }

        private static async Task HandleRequestAsync(MemoryStream memoryStream, SslStream sslStream, string method, string path, UserServiceRequest requester)
        {
            try
            {
                memoryStream.Position = 0;
                using var reader = new StreamReader(memoryStream);
                using var writer = new StreamWriter(sslStream) { AutoFlush = true };

                await requester.HandleRequestAsync(reader, writer, method, path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling request: {ex.Message}");
                SendErrorResponse(sslStream);
            }
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                Console.WriteLine("Keine IPv4-Adresse im lokalen Netzwerk gefunden. Verwende localhost.");
                return "127.0.0.1";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Abrufen der IP-Adresse: {ex.Message}. Verwende localhost.");
                return "127.0.0.1";
            }
        }

        private static void SendErrorResponse(Stream stream)
        {
            Console.WriteLine("Error Response");
            string response = $"HTTP/1.1 404 Not found\r\nContent-Length: 0\r\n\r\n";
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
            stream.Flush();
        }
    }
}