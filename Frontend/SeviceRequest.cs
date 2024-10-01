using System;
using System.IO;
using System.Net;
using System.Text.Json;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;


namespace SemesterProjekt1
{

    public class UserServiceRequest
    {
        public UserServiceHandler _userServiceHandler = new UserServiceHandler();

        public async Task HandleRequestAsync(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            try
            {
                if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/")
                {
                    var users = _userServiceHandler.GetAllUsers();
                    int size = users.Count;

                    string htmlResponse = GenerateOptionsPage(size);
                    SendResponse(response, htmlResponse, "text/html");
                }
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/users")
                {
                    var users = _userServiceHandler.GetAllUsers();
                    string jsonResponse = SerializeToJson(users);
                    SendResponse(response, jsonResponse, "application/json");
                }
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath.StartsWith("/user/"))
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
                else if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/users")
                {
                    await HandleAddUserAsync(request, response);
                }
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/login")
                {
                    SendLoginPage(response);
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/login")
                {
                    await HandleLoginAsync(request, response);
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/sessions") //FOR CURL I GUESS
                {
                    await HandleLoginAsyncCURL(request, response);
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/logout")
                {
                    HandleLogout(request, response);
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/openpack")
                {
                    await HandleOpenCardPackAsync(request, response);
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/inventory")
                {
                    await HandleBuyPacksAsync(request, response);
                }
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/lobby")
                {
                    SendLobbyPage(request, response);
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/join-lobby")
                {
                    await HandleJoinLobbyAsync(request, response);
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/add-card-to-deck")
                {
                    await HandleAddCardToDeckAsync(request, response);
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

        private async Task HandleAddUserAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string requestBody = await reader.ReadToEndAsync();
                var user = DeserializeUser(requestBody);
                if (user != null)
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
                        string inventoryHtml = GenerateInventoryHtml(inventory);

                        // Setzen des Cookies
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
                    SendResponse(response, $"Invalid username or password. {username}+{password}", "text/plain");
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
                    var authenticatedUser = _userServiceHandler.AuthenticateUser(username, password);
                    if (authenticatedUser != null)
                    {
                        string token = $"{user.Username}-mtcgToken";

                        // Setzen des Cookies
                        response.SetCookie(new Cookie("authToken", token));
                        response.SetCookie(new Cookie("userData", $"username={username}&password={password}&userid={user.Id}"));

                        SendResponse(response,token , "text/html");
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        response.Close();
                    }
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
            // Clear cookie
            var cookie = new Cookie("userData", "");
            cookie.Expires = DateTime.Now.AddDays(-1);
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
                    Console.WriteLine("Userdata: " + userData);
                    string username = userData["username"];
                    string password = userData["password"];
                    string userIdString = userData["userid"];
                    int userid = int.Parse(userIdString);

                    var user = _userServiceHandler.AuthenticateUser(username, password);
                    if (user != null)
                    {
                        for (int i = 0; i < user.Inventory.CardPacks.Count; i++)
                        {
                            Console.WriteLine("CardPack at index " + i + ": " + user.Inventory.CardPacks[i]);
                            Console.WriteLine("Here: " + user.Inventory.CardPacks[i].Rarity);
                            Console.WriteLine("Cards in CardPack:");
                        }

                        // Sicherstellen, dass die Liste nicht leer ist, bevor RemoveAt aufgerufen wird
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

                    Console.WriteLine("Userdata: " + userData);

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


        // ...

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





        private void SendLoginPage(HttpListenerResponse response)
        {
            string loginForm = @"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='UTF-8'>
                <title>Login</title>
            </head>
            <body>
                <h1>Login</h1>
                <form method='post' action='/login'>
                    <label for='username'>Username:</label>
                    <input type='text' id='username' name='username'><br>
                    <label for='password'>Password:</label>
                    <input type='password' id='password' name='password'><br>
                    <input type='submit' value='Login'>
                </form>
            </body>
            </html>";

            SendResponse(response, loginForm, "text/html");
        }

        private void SendResponse(HttpListenerResponse response, string content, string contentType)
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

        private string GenerateOptionsPage(int size)
        {
            string htmlResponse = @"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='UTF-8'>
                <title>Options</title>
            </head>
            <body>
                <h1>Options</h1>
                <button onclick='window.location.href=""/users"";'>Show All Users</button>
                <button onclick='window.location.href=""/login"";'>Login</button>";

            for (int i = 1; i <= size; i++)
            {
                htmlResponse += $@"<button onclick='window.location.href=""/user/{i}"";'>Show User with ID {i}</button>";
            }

            htmlResponse += "</body></html>";
            return htmlResponse;
        }

        private string GenerateInventoryHtml(Inventory inventory)
        {
            string html = $@"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='UTF-8'>
                <title>Inventory</title>
            </head>
            <body>
                <h1>Inventory</h1>
                <h2>Owned Cards</h2>
                <form method='post' action='/add-card-to-deck'>
                    <ul>";

            foreach (var card in inventory.OwnedCards)
            {
                int cardIndex = inventory.OwnedCards.IndexOf(card);
                html += $@"
                    <li>
                        <input type='checkbox' name='cardIndices' value='{cardIndex}'>
                        {cardIndex} : {card.Name} - {card.Damage} Damage - {card.Element} - {card.Type}
                    </li>";
            }

            html += $@"
                    </ul>
                    <input type='hidden' name='userID' value='{inventory.UserID}' />
                    <input type='submit' value='Save to Deck'>
                </form>
                <h2>Money: {inventory.Money}</h2>
                <form method='post' action='/openpack'>
                    <input type='hidden' name='userID' value='{inventory.UserID}' />
                    <input type='submit' value='Open Card Pack'>
                </form>
                <form method='post' action='/inventory'>
                    <input type='hidden' name='userID' value='{inventory.UserID}' />
                    <label for='Amount'>Amount:</label>
                    <input type='number' id='Amount' name='Amount' required><br>
                    <input type='submit' value='Buy Card Pack'>
                </form>
                <form id='joinLobbyForm' method='post' action='/join-lobby'>
                 <input type='hidden' name='userID' value='{inventory.UserID}' />
                 <input type='submit' value='Lobby beitreten'>
                 </form>
                <form method='post' action='/logout'>
                    <input type='submit' value='Logout'>
                </form>
            </body>
            </html>";
            return html;
        }
        private void SendLobbyPage(HttpListenerRequest request, HttpListenerResponse response)
        {
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
                    string lobbyPage = $@"
                    <!DOCTYPE html>
                    <html lang='de'>
                    <head>
                        <meta charset='UTF-8'>
                        <title>Lobby</title>
                    </head>
                    <body>
                        <h1>Lobby</h1>
                        <p>ID: {user.Inventory.UserID}</p>
                        <form id='joinLobbyForm' method='post' action='/join-lobby'>
                            <input type='hidden' name='userID' value='{userId}' />
                            <input type='submit' value='Lobby beitreten'>
                        </form>
                    </body>
                    </html>";

                    SendResponse(response, lobbyPage, "text/html");
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    SendResponse(response, "Unauthorized access. Please log in.", "text/plain");
                }
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                SendResponse(response, "Unauthorized access. Please log in.", "text/plain");
            }
        }
    }
}