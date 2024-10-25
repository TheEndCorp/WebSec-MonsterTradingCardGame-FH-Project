using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace SemesterProjekt1
{
    public class UserServiceRequest
    {
        public UserServiceHandler _userServiceHandler = new UserServiceHandler(); 
        private HTMLGEN _htmlgen = new HTMLGEN(new UserServiceHandler());

        public async Task HandleRequestAsync(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            try
            {
                switch (request.HttpMethod)
                {
                    case "GET":
                        await HandleGetRequestAsync(request, response);
                        break;
                    case "POST":
                        await HandlePostRequestAsync(request, response);
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        response.Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                string errorMessage = $"Error: {ex.Message}\n{ex.StackTrace}";
                SendResponse(response, errorMessage, "text/plain");
            }
        }

        private async Task HandleGetRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            switch (request.Url?.AbsolutePath)
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
                    await HandleGetUserByIdAsync(request, response);
                    break;
                case "/login":
                    _htmlgen.SendLoginPage(response);
                    break;
                case "/lobby":
                    _htmlgen.SendLobbyPage(request, response);
                    break;
                default:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Close();
                    break;
            }
        }

        private async Task HandlePostRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            switch (request.Url?.AbsolutePath)
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
                case "/logout":
                    HandleLogout(request, response);
                    break;
                case "/openpack":
                    await HandleOpenCardPackAsync(request, response);
                    break;
                case "/inventory":
                    await HandleBuyPacksAsync(request, response);
                    break;
                case "/join-lobby":
                    await HandleJoinLobbyAsync(request, response);
                    break;
                case "/add-card-to-deck":
                    await HandleAddCardToDeckAsync(request, response);
                    break;
                default:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Close();
                    break;
            }
        }

        private async Task HandleGetUserByIdAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            string userIdString = request.Url.AbsolutePath.Substring(6);
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
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Close();
                }
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Close();
            }
        }

        private async Task HandleAddUserAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string requestBody = await reader.ReadToEndAsync();
                var user = DeserializeUser(requestBody);
                if (user != null && (IsValidInput(user.Username) || IsValidInput(user.Password)))
                {
                    var existingUser = _userServiceHandler.GetUserByName(user.Username);
                    if (existingUser == null)
                    {
                        _userServiceHandler.AddUser(user);
                        response.StatusCode = (int)HttpStatusCode.Created;
                        SendResponse(response, SerializeToJson(new { message = "User created successfully" }), "application/json");
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.Conflict;
                        SendResponse(response, SerializeToJson(new { error = "User already exists" }), "application/json");
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    SendResponse(response, SerializeToJson(new { error = "Invalid user data" }), "application/json");
                }
            }
            response.OutputStream.Close();
        }

        private async Task HandleLoginAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string requestBody = await reader.ReadToEndAsync();
                var formData = System.Web.HttpUtility.ParseQueryString(requestBody);
                string username = formData["username"];
                string password = formData["password"];

                var user = _userServiceHandler.AuthenticateUser(username, password);
                if (user != null)
                {
                    var inventory = user.Inventory;
                    if (inventory != null)
                    {
                        string token = $"{user.Username}-mtcgToken";
                        string inventoryHtml = _htmlgen.GenerateInventoryHtml(inventory);

                        response.SetCookie(new Cookie("authToken", token));
                        response.SetCookie(new Cookie("userData", $"username={username}&password={password}&userid={user.Id}"));

                        SendResponse(response, inventoryHtml, "text/html");
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        SendResponse(response, "Inventory not found.", "text/plain");
                        user.Inventory = new Inventory(user.Id);
                        _userServiceHandler.UpdateUser(user.Id, user);
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    SendResponse(response, "Invalid username or password.", "text/plain");
                }
            }
            response.OutputStream.Close();
        }

        private async Task HandleLoginAsyncCURL(HttpListenerRequest request, HttpListenerResponse response)
        {
            using (var reader = new StreamReader(request.InputStream))
            {
                var requestBody = await reader.ReadToEndAsync();
                string username = null;
                string password = null;

                if (request.ContentType == "application/json")
                {

                    var user1 = JsonSerializer.Deserialize<User>(requestBody);
                    username = user1.Username;
                    password = user1.Password;
                }

                var user = _userServiceHandler.AuthenticateUser(username, password);
                if (user != null)
                {
                    string token = $"{user.Username}-mtcgToken";

                    response.SetCookie(new Cookie("authToken", token));
                    response.SetCookie(new Cookie("userData", $"username={username}&password={password}&userid={user.Id}"));

                    SendResponse(response, token, "text/html");
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Close();
                }
            }
        }

        private void HandleLogout(HttpListenerRequest request, HttpListenerResponse response)
        {
            var cookie = new Cookie("userData", "") { Expires = DateTime.Now.AddDays(-1) };
            response.SetCookie(cookie);

            response.StatusCode = (int)HttpStatusCode.OK;
            SendResponse(response, "Logged out successfully.", "text/plain");
            response.OutputStream.Close();
        }

        private async Task HandleOpenCardPackAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string requestBody = await reader.ReadToEndAsync();
                var formData = System.Web.HttpUtility.ParseQueryString(requestBody);

                var userDataCookie = request.Cookies["userData"]?.Value;
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
                        if (user.Inventory.CardPacks.Count > 0)
                        {
                            for (int i = user.Inventory.CardPacks.Count - 1; i >= 0; i--)
                            {
                                user.Inventory.OpenCardPack(user.Inventory.CardPacks[i]);
                                user.Inventory.CardPacks.RemoveAt(i);
                            }
                        }

                        string jsonResponse = SerializeToJson(user.Inventory.OwnedCards);
                        SendResponse(response, jsonResponse, "application/json");
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        SendResponse(response, "Invalid username or password.", "text/plain");
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    SendResponse(response, "Invalid user ID.", "text/plain");
                }
            }

            response.OutputStream.Close();
        }

        private async Task HandleBuyPacksAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string requestBody = await reader.ReadToEndAsync();
                var formData = System.Web.HttpUtility.ParseQueryString(requestBody);

                var userDataCookie = request.Cookies["userData"]?.Value;
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
                            SendResponse(response, jsonResponse, "application/json");
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                            SendResponse(response, "Invalid amount.", "text/plain");
                        }
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        SendResponse(response, "Invalid username or password.", "text/plain");
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    SendResponse(response, "Invalid user ID.", "text/plain");
                }
            }

            response.OutputStream.Close();
        }

        private async Task HandleJoinLobbyAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string requestBody = await reader.ReadToEndAsync();
                var formData = System.Web.HttpUtility.ParseQueryString(requestBody);

                var userDataCookie = request.Cookies["userData"]?.Value;
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
                        _userServiceHandler.AddUserToLobby(user);
                        SendResponse(response, "User added to lobby", "text/plain");
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        SendResponse(response, "Invalid username or password.", "text/plain");
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    SendResponse(response, "Invalid user ID.", "text/plain");
                }
            }

            response.OutputStream.Close();
        }



        private async Task HandleAddCardToDeckAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string requestBody = await reader.ReadToEndAsync();
                    var formData = System.Web.HttpUtility.ParseQueryString(requestBody);

                    var userDataCookie = request.Cookies["userData"]?.Value;
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
                            SendResponse(response, "Cards added to deck successfully.", "text/html");
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                            SendResponse(response, "Invalid card positions.", "text/plain");
                        }
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        SendResponse(response, "Invalid user credentials.", "text/plain");
                    }
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                SendResponse(response, ex.Message, "text/plain");
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