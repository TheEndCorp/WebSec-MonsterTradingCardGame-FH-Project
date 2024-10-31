using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


// 20 hours already Wasted from HTTPLISTENER -> TCP ANDWiwkndiunwaidon Day 3 Note of this shit 




namespace SemesterProjekt1
{
    public class UserServiceRequest
    {
        public UserServiceHandler _userServiceHandler = new UserServiceHandler();
        private HTMLGEN _htmlgen = new HTMLGEN(new UserServiceHandler());

        public async Task HandleRequestAsync(StreamReader request, StreamWriter response, string method, string path)
        {
            try
            {


                switch (method)
                {
                    case "GET":
                        await HandleGetRequestAsync(request, response, path);
                        break;
                    case "POST":
                        await HandlePostRequestAsync(request, response, path);
                        break;
                    default:
                        response.WriteLine("HTTP/1.1 405 Method Not Allowed");
                        response.WriteLine("Content-Length: 0");
                        response.WriteLine();
                        response.Flush();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling request: {ex.Message}");
            }
        }


        private async Task HandleGetRequestAsync(StreamReader request, StreamWriter response, string path1)
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
                case string path when path1.StartsWith("/user/"):
                    await HandleGetUserByIdAsync(request, response, path1);
                    break;
                case "/login":
                    //        _htmlgen.SendLoginPage(response);
                    break;
                case "/lobby":
                    //_htmlgen.SendLobbyPage(request, response);
                    break;
                case "/logout":
                    await HandleLogout(response);
                    break;
                default:
                    response.WriteLine("HTTP/1.1 404 Not Found");
                    response.WriteLine("Content-Length: 0");
                    response.WriteLine();
                    break;
            }
        }

        private async Task HandlePostRequestAsync(StreamReader request, StreamWriter response, string path1)
        {
            switch (path1)
            {
                case "/users":
                    await HandleAddUserAsync(request, response);
                    break;
                case "/login":
                    await HandleLoginAsync(request, response);
                    break;
                case "/sessions":
                    await HandleLoginAsyncCURL(request, response);
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

        private async Task HandleGetUserByIdAsync(StreamReader request, StreamWriter response, string path)
        {
            Console.WriteLine(path);
            string userIdString = path.Substring(6);

            if (int.TryParse(userIdString, out int userId))
            {
                var user = _userServiceHandler.GetUserById(userId);
                if (user != null)
                {
                    string jsonResponse = SerializeToJson(user);
                    SendResponse(response, jsonResponse, "application/json");
                }
                else
                {
                    SendErrorResponse(response, HttpStatusCode.BadRequest);
                }
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.BadRequest);
            }
        }

        private async Task HandleAddUserAsync(StreamReader request, StreamWriter response)
        {

            string requestBody = await ReadRequestBodyAsync(request, response);


            var user = DeserializeUser(requestBody);
            if (user != null && (IsValidInput(user.Username) && IsValidInput(user.Password)))
            {
                var existingUser = _userServiceHandler.GetUserByName(user.Username);
                if (existingUser == null)
                {
                    _userServiceHandler.AddUser(user);
                    SendResponse(response, "User created successfully", "application/text", HttpStatusCode.Created);
                }
                else
                {
                    SendErrorResponse(response, HttpStatusCode.Conflict, "User already exists");
                }
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid user data");
            }
        }

        private async Task HandleLoginAsync(StreamReader request, StreamWriter response)
        {

                var user = await IsIdentiyYesUser(request, response);
                if (user != null)
                {
                    string token = $"{user.Username}-mtcgToken";
                    response.WriteLine($"HTTP/1.1 200 OK");
                    response.WriteLine($"Set-Cookie: authToken={token};");
                    response.WriteLine($"Set-Cookie: userData=username={user.Username}&password={user.Password}&userid={user.Id};");
                    response.WriteLine("Content-Type: text/plain");
                    response.WriteLine($"Content-Length: {token.Length}+24");
                    response.WriteLine();
                    response.WriteLine($"Login successful. Token: {token}");
                    response.Flush();
                }
                else
                {
                    SendErrorResponse(response, HttpStatusCode.Unauthorized, "Invalid username or password");
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
            if (!writer.BaseStream.CanWrite)
            { Console.WriteLine("Error"); }



            //////////////////////////////////////////////
            try
            {



                var authenticatedUser = await IsIdentiyYesUser(reader, writer);
                if (authenticatedUser != null)
                {
                    string token = $"{authenticatedUser.Username}-mtcgToken";
                    string response = $"HTTP/1.1 200 OK\r\nSet-Cookie: authToken={token}\r\nContent-Type: text/html\r\nContent-Length: {token.Length}\r\n\r\n{token}";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    Console.WriteLine("Yes");
                    await writer.BaseStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    await writer.BaseStream.FlushAsync();
                    Console.WriteLine("Yes");
                }
                else
                {
                    SendErrorResponse(writer, HttpStatusCode.BadRequest);
                }
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"I/O error during login: {ioEx.Message}");
                SendErrorResponse(writer, HttpStatusCode.InternalServerError);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during login: {ex.Message}");
                SendErrorResponse(writer, HttpStatusCode.InternalServerError);
            }
        }


        private async Task HandleLogout(StreamWriter writer)
        {
            try
            {
                // Set expired cookie headers to clear them
                writer.WriteLine("HTTP/1.1 200 OK");
                writer.WriteLine("Set-Cookie: userData=; Expires=Thu, 01 Jan 1970 00:00:00 GMT");
                writer.WriteLine("Content-Type: text/plain");
                writer.WriteLine();
                writer.WriteLine("Logged out successfully.");
                writer.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during logout: {ex.Message}");
                writer.WriteLine("HTTP/1.1 500 Internal Server Error");
                writer.WriteLine("Content-Length: 0");
                writer.WriteLine();
                writer.Flush();
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
        private bool IsJson(string input)
        {

            input = input.Trim();
            Console.WriteLine(input);
            return input.StartsWith("{") && input.EndsWith("}") || input.StartsWith("[") && input.EndsWith("]");
        }

        private async Task<User> IsIdentiyYesUser(StreamReader reader, StreamWriter writer)
        {

            if (!writer.BaseStream.CanWrite)
            { Console.WriteLine("Error");
                return null;
            }

            string requestBodyString = await ReadRequestBodyAsync(reader, writer);




                string? username = null;
                string? password = null;

                if (IsJson(requestBodyString))
                {
                    try
                    {
                        var user1 = JsonSerializer.Deserialize<User>(requestBodyString);
                        if (user1 != null)
                        {
                            username = user1.Username;
                            password = user1.Password;
                            Console.WriteLine($"Deserialized User - Username: {username}, Password: {password}");

                        }
                        else
                        {
                            Console.WriteLine("Deserialized user is null.");
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"JSON deserialization error: {jsonEx.Message}");
                        SendErrorResponse(writer, HttpStatusCode.BadRequest);
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine("Invalid JSON format.");
                    SendErrorResponse(writer, HttpStatusCode.BadRequest);
                    return null;
                }

                if (username == null || password == null)
                {
                    SendErrorResponse(writer, HttpStatusCode.BadRequest);
                    return null;
                }

                //////////////////////////////////////////////


                var authenticatedUser = _userServiceHandler.AuthenticateUser(username, password);


                if (authenticatedUser != null) return authenticatedUser;
                else
                {
                    SendErrorResponse(writer, HttpStatusCode.BadRequest);
                    return null;
                }

         }






            


        private async Task<string> ReadRequestBodyAsync(StreamReader reader, StreamWriter writer)
        {
            string? requestLine = await reader.ReadLineAsync();
            if (requestLine == null)
            {
                Console.WriteLine("No request line received.");
                SendErrorResponse(writer, HttpStatusCode.BadRequest);
                return string.Empty;
            }

            Console.WriteLine($"Request line: {requestLine}");

            int contentLength = 0;
            string? line;
            while ((line = await reader.ReadLineAsync()) != null && line != "")
            {
                Console.WriteLine($"Header: {line}");
                if (line.StartsWith("Content-Length:"))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out contentLength))
                    {
                        Console.WriteLine($"Content-Length: {contentLength}");
                    }
                }
            }

            if (contentLength <= 0)
            {
                Console.WriteLine("Content-Length is invalid or missing.");
                SendErrorResponse(writer, HttpStatusCode.BadRequest);
                return string.Empty;
            }

            var requestBody = new char[contentLength];
            int totalBytesRead = 0;

            while (totalBytesRead < contentLength)
            {
                int bytesRead = await reader.ReadAsync(requestBody, totalBytesRead, contentLength - totalBytesRead);
                if (bytesRead == 0)
                {
                    Console.WriteLine("End of stream reached before content length was fulfilled.");
                    break;
                }
                totalBytesRead += bytesRead;
            }

            string requestBodyString = new string(requestBody);
            Console.WriteLine($"Received request body: {requestBodyString}");

            // Setzen Sie den Stream zurück an den Anfang
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            reader.DiscardBufferedData();

            return requestBodyString;
        }

        private async Task<string> ReadRequestBodyCookieAsync(StreamReader reader, StreamWriter writer)
        {
            string? requestLine = await reader.ReadLineAsync();
            if (requestLine == null)
            {
                Console.WriteLine("No request line received.");
                SendErrorResponse(writer, HttpStatusCode.BadRequest);
                return string.Empty;
            }

            Console.WriteLine($"Request line: {requestLine}");

            string? line;
            string? userDataCookie = null;
            while ((line = await reader.ReadLineAsync()) != null && line != "")
            {
                if (line.StartsWith("Cookie:"))
                {
                    var cookies = line.Substring("Cookie:".Length).Trim().Split(';');
                    foreach (var cookie in cookies)
                    {
                        var cookieParts = cookie.Split('=');
                        if (cookieParts.Length == 2 && cookieParts[0].Trim() == "userData")
                        {
                            userDataCookie = cookieParts[1].Trim();
                            break;
                        }
                    }
                }
            }

            if (userDataCookie == null)
            {
                Console.WriteLine("userData cookie not found.");
                SendErrorResponse(writer, HttpStatusCode.BadRequest);
                return string.Empty;
            }

            Console.WriteLine($"userData cookie: {userDataCookie}");

            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            reader.DiscardBufferedData();
            return userDataCookie;
        }





        private void SendErrorResponse(StreamWriter writer, HttpStatusCode statusCode)
        {
            writer.WriteLine($"HTTP/1.1 {(int)statusCode} {statusCode}");
            writer.WriteLine("Content-Length: 0");
            writer.WriteLine();
            writer.Flush();
        }




    }
}