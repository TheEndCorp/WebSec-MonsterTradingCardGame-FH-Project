using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace SemesterProjekt1
{
    public class UserServiceRequest
    {
        public UserServiceHandler _userServiceHandler = new UserServiceHandler(); 
        private HTMLGEN _htmlgen = new HTMLGEN(new UserServiceHandler());

        public async Task HandleRequestAsync(TcpClient client, string method, string path)
        {
            try
            {
                var networkStream = client.GetStream();

                // Check if the network stream is readable and writable
                if (!networkStream.CanRead || !networkStream.CanWrite)
                {
                    Console.WriteLine("NetworkStream is not readable or writable.");
                    return;
                }

                var request = new StreamReader(networkStream); // Request
                var response = new StreamWriter(networkStream) { AutoFlush = true }; // Response

                switch (method)
                {
                    case "GET":
                        await HandleGetRequestAsync(request, response, client, path);
                        break;
                    case "POST":
                        await HandlePostRequestAsync(request, response, client, path);
                        break;
                    default:
                        response.WriteLine("HTTP/1.1 405 Method Not Allowed");
                        response.WriteLine("Content-Length: 0");
                        response.WriteLine();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling request: {ex.Message}");
            }
        }


        private async Task HandleGetRequestAsync(StreamReader request, StreamWriter response, TcpClient client, string path1)
        {
            switch (path1)
            {
                case "/":
                    var users = _userServiceHandler.GetAllUsers();
                    string htmlResponse = _htmlgen.GenerateOptionsPage(users.Count);
                    SendResponse(response, htmlResponse, "text/html");
                    break;
                case "/users":
                    var allUsers = _userServiceHandler.GetAllUsers();
                    string jsonResponse = SerializeToJson(allUsers);
                    SendResponse(response, jsonResponse, "application/json");
                    break;
                case string path when path.StartsWith("/user/"):
          //          await HandleGetUserByIdAsync(request, response);
                    break;
                case "/login":
            //        _htmlgen.SendLoginPage(response);
                    break;
                case "/lobby":
                    //_htmlgen.SendLobbyPage(request, response);
                    break;
                default:
                    response.WriteLine("HTTP/1.1 404 Not Found");
                    response.WriteLine("Content-Length: 0");
                    response.WriteLine();
                    break;
            }
        }

        private async Task HandlePostRequestAsync(StreamReader request, StreamWriter response, TcpClient client, string path1)
        {
            switch (path1)
            {
                case "/users":
            //        await HandleAddUserAsync(request, response);
                    break;
                case "/login":
              //      await HandleLoginAsync(request, response);
                    break;
                case "/sessions":
                    await HandleLoginAsyncCURL(request, response);
                    break;
                case "/logout":
             //       HandleLogout(request, response);
                    break;
                case "/openpack":
             //       await HandleOpenCardPackAsync(request, response);
                    break;
                case "/inventory":
             //       await HandleBuyPacksAsync(request, response);
                    break;
                case "/add-card-to-deck":
             //       await HandleAddCardToDeckAsync(request, response);
                    break;
                default:
                    response.WriteLine("HTTP/1.1 404 Not Found");
                    response.WriteLine("Content-Length: 0");
                    response.WriteLine();
                    break;
            }
        }

        private async Task HandleGetUserByIdAsync(StreamWriter writer, string path)
        {
            string userIdString = path.Substring(6);
            if (int.TryParse(userIdString, out int userId))
            {
                var user = _userServiceHandler.GetUserById(userId);
                if (user != null)
                {
                    string jsonResponse = SerializeToJson(user);
                    SendResponse(writer, jsonResponse, "application/json");
                }
                else
                {
                    SendErrorResponse(writer, HttpStatusCode.NotFound, "User Not Found");
                }
            }
            else
            {
                SendErrorResponse(writer, HttpStatusCode.BadRequest, "Invalid User ID");
            }
        }

        private async Task HandleAddUserAsync(StreamWriter writer, string requestBody)
        {
            var user = DeserializeUser(requestBody);
            if (user != null && (IsValidInput(user.Username) && IsValidInput(user.Password)))
            {
                var existingUser = _userServiceHandler.GetUserByName(user.Username);
                if (existingUser == null)
                {
                    _userServiceHandler.AddUser(user);
                    SendResponse(writer, "User created successfully", "application/json", HttpStatusCode.Created);
                }
                else
                {
                    SendErrorResponse(writer, HttpStatusCode.Conflict, "User already exists");
                }
            }
            else
            {
                SendErrorResponse(writer, HttpStatusCode.BadRequest, "Invalid user data");
            }
        }

        private async Task HandleLoginAsync(StreamWriter writer, string requestBody)
        {
            var formData = System.Web.HttpUtility.ParseQueryString(requestBody);
            string username = formData["username"];
            string password = formData["password"];

            var user = _userServiceHandler.AuthenticateUser(username, password);
            if (user != null)
            {
                string token = $"{user.Username}-mtcgToken";
                SendResponse(writer, $"Login successful. Token: {token}", "text/plain");
            }
            else
            {
                SendErrorResponse(writer, HttpStatusCode.Unauthorized, "Invalid username or password");
            }
        }

        private void SendErrorResponse(StreamWriter writer, HttpStatusCode statusCode, string message)
        {
            writer.WriteLine($"HTTP/1.1 {(int)statusCode} {statusCode}");
            writer.WriteLine("Content-Type: text/plain");
            writer.WriteLine("Content-Length: " + message.Length);
            writer.WriteLine();
            writer.Write(message);
        }



        private async Task HandleLoginAsyncCURL(StreamReader reader, StreamWriter writer)
        {
            try
            {
                string requestBody = await reader.ReadToEndAsync();
                string username = null;
                string password = null;

           /*    // if ( Check content type manually or assume JSON )
                {
                    var user = JsonSerializer.Deserialize<User>(requestBody);
                    username = user.Username;
                    password = user.Password;
                }
                */

                var authenticatedUser = _userServiceHandler.AuthenticateUser(username, password);
                if (authenticatedUser != null)
                {
                    string token = $"{authenticatedUser.Username}-mtcgToken";

                    // Create the cookies as strings to mimic HTTP cookies
                    writer.WriteLine("HTTP/1.1 200 OK");
                    writer.WriteLine("Set-Cookie: authToken=" + token);
                    writer.WriteLine("Set-Cookie: userData=username=" + username + "&password=" + password + "&userid=" + authenticatedUser.Id);
                    writer.WriteLine("Content-Type: text/html");
                    writer.WriteLine();
                    writer.WriteLine(token);
                }
                else
                {
                    writer.WriteLine("HTTP/1.1 400 Bad Request");
                    writer.WriteLine("Content-Length: 0");
                    writer.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during login: {ex.Message}");
                writer.WriteLine("HTTP/1.1 500 Internal Server Error");
                writer.WriteLine("Content-Length: 0");
                writer.WriteLine();
            }
        }


        private void HandleLogout(StreamWriter writer)
        {
            try
            {
                // Set expired cookie headers to clear them
                writer.WriteLine("HTTP/1.1 200 OK");
                writer.WriteLine("Set-Cookie: userData=; Expires=Thu, 01 Jan 1970 00:00:00 GMT");
                writer.WriteLine("Content-Type: text/plain");
                writer.WriteLine();
                writer.WriteLine("Logged out successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during logout: {ex.Message}");
                writer.WriteLine("HTTP/1.1 500 Internal Server Error");
                writer.WriteLine("Content-Length: 0");
                writer.WriteLine();
            }
        }

        /*
        private async Task HandleOpenCardPackAsync(StreamReader reader, StreamWriter writer)
        {
            string requestBody = await reader.ReadToEndAsync();
            var formData = System.Web.HttpUtility.ParseQueryString(requestBody);

            // Extract the user data, simulate cookie handling by parsing manually
          //  string userDataCookie =  retrieve userData value ;
            if (userDataCookie != null)
            {
                var userData = System.Web.HttpUtility.ParseQueryString(userDataCookie);
                string username = userData["username"];
                string password = userData["password"];
                string userIdString = userData["userid"];
                int userId = int.Parse(userIdString);

                var user = _userServiceHandler.AuthenticateUser(username, password);
                if (user != null)
                {
                    if (user.Inventory.CardPacks.Count > 0)
                    {
                        for (int i = user.Inventory.CardPacks.Count - 1; i >= 0; i--)
                        {
                            user.Inventory.OpenCardPack(user.Inventory.CardPacks[i]);
                            user.Inventory.CardPacks.RemoveAt(i);
                        }
                    }

                    string jsonResponse = SerializeToJson(user.Inventory.OwnedCards);
                    writer.WriteLine("HTTP/1.1 200 OK");
                    writer.WriteLine("Content-Type: application/json");
                    writer.WriteLine($"Content-Length: {jsonResponse.Length}");
                    writer.WriteLine();
                    writer.WriteLine(jsonResponse);
                }
                else
                {
                    writer.WriteLine("HTTP/1.1 401 Unauthorized");
                    writer.WriteLine("Content-Length: 0");
                    writer.WriteLine();
                }
            }
            else
            {
                writer.WriteLine("HTTP/1.1 400 Bad Request");
                writer.WriteLine("Content-Length: 0");
                writer.WriteLine();
            }
        }


        private async Task HandleBuyPacksAsync(StreamReader reader, StreamWriter writer)
        {
            string requestBody = await reader.ReadToEndAsync();
            var formData = System.Web.HttpUtility.ParseQueryString(requestBody);

            // Simulate cookie handling by parsing manually
          //  string userDataCookie =  retrieve userData value ;
            if (userDataCookie != null)
            {
                var userData = System.Web.HttpUtility.ParseQueryString(userDataCookie);
                string username = userData["username"];
                string password = userData["password"];
                string userIdString = userData["userid"];
                int userid = int.Parse(userIdString);

                var user = _userServiceHandler.AuthenticateUser(username, password);
                if (user != null)
                {
                    if (int.TryParse(formData["Amount"], out int amount))
                    {
                        user.Inventory.AddCardPack(new CardPack(userid), amount);
                        _userServiceHandler.UpdateUser(user.Id, user);
                        string jsonResponse = SerializeToJson(new { message = "Packs bought successfully" });
                        SendResponse(writer, jsonResponse, "application/json");
                    }
                    else
                    {
                        SendErrorResponse(writer, HttpStatusCode.BadRequest, "Invalid amount.");
                    }
                }
                else
                {
                    SendErrorResponse(writer, HttpStatusCode.Unauthorized, "Invalid username or password.");
                }
            }
            else
            {
                SendErrorResponse(writer, HttpStatusCode.BadRequest, "Invalid user ID.");
            }
        }

        

async Task HandleAddCardToDeckAsync(StreamReader reader, StreamWriter writer)
        {
            try
            {
                string requestBody = await reader.ReadToEndAsync();
                var formData = System.Web.HttpUtility.ParseQueryString(requestBody);

                // Simulate cookie handling by parsing manually
                string userDataCookie = 
                if (userDataCookie != null)
                {
                    var userData = System.Web.HttpUtility.ParseQueryString(userDataCookie);
                    string username = userData["username"];
                    string password = userData["password"];
                    string userIdString = userData["userid"];
                    int userId = int.Parse(userIdString);

                    var user = _userServiceHandler.AuthenticateUser(username, password);
                    string[] cardIndices = formData.GetValues("cardIndices");

                    if (cardIndices != null)
                    {
                        int[] cardPositions = cardIndices.Select(int.Parse).ToArray();
                        _userServiceHandler.AddCardToDeck(user.Id, user.Username, user.Password, cardPositions);
                        SendResponse(writer, "Cards added to deck successfully.", "text/html");
                    }
                    else
                    {
                        SendErrorResponse(writer, HttpStatusCode.BadRequest, "Invalid card positions.");
                    }
                }
                else
                {
                    SendErrorResponse(writer, HttpStatusCode.BadRequest, "Invalid user credentials.");
                }
            }
            catch (Exception ex)
            {
                SendErrorResponse(writer, HttpStatusCode.InternalServerError, ex.Message);
            }
        }

*/
        private void SendResponse(StreamWriter writer, string content, string contentType, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            writer.WriteLine($"HTTP/1.1 {(int)statusCode} {statusCode}");
            writer.WriteLine("Content-Type: " + contentType);
            writer.WriteLine("Content-Length: " + content.Length);
            writer.WriteLine();
            writer.Write(content);
            writer.Flush(); // Ensure the content is flushed to the stream
        }

        private User DeserializeUser(string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<User>(json, options);
        }

        private string SerializeToJson(object obj)
        {
            return JsonSerializer.Serialize(obj);
        }
        
        
        private bool IsValidInput(string input)
        {
            // Überprüfen Sie auf schädliche Zeichen oder Muster
            string[] blackList = { "'", "\"", "--", ";", "/*", "*/", "xp_" };
            foreach (var item in blackList)
            {
                if (input.Contains(item))
                {
                    return false;
                }
            }

            // Überprüfen Sie die Länge der Eingabe
            if (input.Length > 20) // Beispielgrenze, anpassen nach Bedarf
            {
                return false;
            }

            return true;
        }



    }
}