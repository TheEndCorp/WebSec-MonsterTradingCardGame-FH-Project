using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

// 20 hours already Wasted from HTTPLISTENER -> TCP ANDWiwkndiunwaidon Day 3 Note of this shit
// 25 and nearly finished
// i lost track of time :skull: Wakatime gonna save me i guess

namespace SemesterProjekt1
{
    internal enum HttpStatusCode
    {
        OK = 200,
        Created = 201,
        BadRequest = 400,
        Unauthorized = 401,
        Conflict = 409,
        InternalServerError = 500
    }

    public class UserServiceRequest
    {
        private static ConcurrentQueue<User> _battleQueue = new ConcurrentQueue<User>();
        private static ConcurrentDictionary<int, TaskCompletionSource<string>> _userResponses = new ConcurrentDictionary<int, TaskCompletionSource<string>>();
        private HTMLGEN _htmlgen;
        public UserServiceHandler _userServiceHandler;

        public UserServiceRequest()
        {
            _userServiceHandler = new UserServiceHandler();
            _htmlgen = new HTMLGEN(_userServiceHandler);
        }

        private void SendErrorResponse(StreamWriter writer, HttpStatusCode statusCode, string message)
        {
            writer.WriteLine($"HTTP/1.1 {(int)statusCode} {statusCode}");
            writer.WriteLine("Content-Type: text/plain");
            writer.WriteLine("Content-Length: " + message.Length);
            writer.WriteLine();
            writer.Write(message);
            writer.Flush();
        }

        private async Task HandleGetRequestAsync(StreamReader request, StreamWriter response, string path1)
        {
            switch (path1.Split('?')[0])
            {
                case "/":
                    {
                        var users = _userServiceHandler.GetAllUsers();
                        string htmlResponse = _htmlgen.GenerateOptionsPage(users.Count);
                        SendResponse(response, htmlResponse, "text/html");
                        break;
                    }
                case "/users":
                    {
                        var allUsers = _userServiceHandler.GetAllUsers();
                        string jsonResponse = SerializeToJson(allUsers);
                        SendResponse(response, jsonResponse, "application/json");
                        break;
                    }
                case "/cards":
                    {
                        var user = await IsIdentiyYesUserCookie(request, response);
                        if (user == null)
                        {
                            SendErrorResponse(response, HttpStatusCode.Unauthorized);
                        }
                        else
                        {
                            var allCards = user.Inventory.OwnedCards;
                            string jsonResponse = SerializeToJson(allCards);
                            SendResponse(response, jsonResponse, "application/json");
                        }
                        break;
                    }
                case string path when path.StartsWith("/user/"):
                    {
                        await HandleGetUserByIdAsync(request, response, path);
                        break;
                    }
                case string path when path.StartsWith("/users/"):
                    {
                        await HandleGetUserByUsernameAsync(request, response, path);
                        break;
                    }
                case "/login":
                    {
                        _htmlgen.SendLoginPage(response);
                        break;
                    }
                case "/scoreboard":
                    {
                        var allUsers = _userServiceHandler.GetAllUsers();
                        var sortedUsers = allUsers.OrderByDescending(user => user.Inventory.ELO)
                                                  .Select(user => new { user.Username, user.Inventory.ELO });
                        string jsonResponse = SerializeToJson(sortedUsers);
                        SendResponse(response, jsonResponse, "application/json");
                        break;
                    }
                case "/lobby":
                    {
                        // _htmlgen.SendLobbyPage(request, response);
                        break;
                    }
                case "/logout":
                    {
                        await HandleLogout(response);
                        break;
                    }

                /////////////// CURL Functions
                case "/tradings":
                    {
                        var deals = _userServiceHandler.GetAllTradingDeals();
                        string jsonResponse = SerializeToJson(deals);
                        SendResponse(response, jsonResponse, "application/json");
                        break;
                    }
                case "/stats":
                    {
                        var user = await IsIdentiyYesUserCookie(request, response);
                        if (user == null)
                        {
                            SendErrorResponse(response, HttpStatusCode.Unauthorized);
                        }
                        else
                        {
                            string jsonResponse = SerializeToJson(user.Inventory.ELO);
                            SendResponse(response, jsonResponse, "application/json");
                        }
                        break;
                    }
                case "/deck":
                    {
                        var user = await IsIdentiyYesUserCookie(request, response);
                        if (user == null)
                        {
                            SendErrorResponse(response, HttpStatusCode.Unauthorized);
                        }
                        else
                        {
                            string query = path1.Contains("?") ? path1.Split('?')[1] : string.Empty;
                            if (query.Contains("format=plain"))
                            {
                                var deck = user.Inventory.Deck.Cards;
                                string plainResponse = string.Join("\n", deck.Select(card => card.ToString()));
                                SendResponse(response, plainResponse, "text/plain");
                            }
                            else
                            {
                                var deck = user.Inventory.Deck.Cards;
                                string jsonResponse = SerializeToJson(deck);
                                SendResponse(response, jsonResponse, "application/json");
                            }
                        }
                        break;
                    }
                default:
                    {
                        response.WriteLine("HTTP/1.1 404 Not Found");
                        response.WriteLine("Content-Length: 0");
                        response.WriteLine();
                        break;
                    }
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

                case "/openpack":
                    await HandleOpenCardPackAsync(request, response);
                    break;

                case "/inventory":
                    await HandleBuyPacksAsync(request, response);
                    break;

                case "/add-card-to-deck":
                    await HandleAddCardToDeckAsync(request, response);
                    break;

                /////////////// CURL Functions
                case "/tradings":
                    await HandleAddTradingDealAsync(request, response);
                    break;

                case string path when path1.StartsWith("/tradings/"):
                    await HandleExecuteTradeAsync(request, response, path1);
                    break;

                case "/sessions":
                    await HandleLoginAsyncCURL(request, response);
                    break;

                case "/transactions/packages":
                    await HandleBuyPacksCURLAsync(request, response);
                    break;

                case "/battles":
                    await HandleBattleRequestAsync(request, response);
                    break;

                case "/packages":
                    await HandleAddPackagesAsync(request, response);
                    break;

                default:
                    response.WriteLine("HTTP/1.1 404 Not Found");
                    response.WriteLine("Content-Length: 0");
                    response.WriteLine();
                    response.Flush();
                    break;
            }
        }

        private async Task HandlePutRequestAsync(StreamReader request, StreamWriter response, string path1)
        {
            switch (path1)
            {
                case "/deck":
                    await HandleConfigureDeckAsync(request, response);
                    break;

                case string path when path.StartsWith("/users/"):
                    await HandleUpdateUserAsync(request, response, path);
                    break;

                default:
                    response.WriteLine("HTTP/1.1 404 Not Found");
                    response.WriteLine("Content-Length: 0");
                    response.WriteLine();
                    response.Flush();
                    break;
            }
        }

        private async Task HandleDeleteRequestAsync(StreamReader request, StreamWriter response, string path1)
        {
            switch (path1)
            {
                case string path when path1.StartsWith("/tradings/"):
                    await HandleDeleteTradingDealAsync(request, response, path1);
                    break;

                default:
                    response.WriteLine("HTTP/1.1 404 Not Found");
                    response.WriteLine("Content-Length: 0");
                    response.WriteLine();
                    response.Flush();
                    break;
            }
        }

        private async Task HandleConfigureDeckAsync(StreamReader reader, StreamWriter writer)
        {
            var user = await IsIdentiyYesUserCookie(reader, writer);
            if (user != null)
            {
                string requestBody = await ReadRequestBodyAsync(reader, writer);
                try
                {
                    var cardIds = JsonSerializer.Deserialize<List<Guid>>(requestBody);
                    if (cardIds != null)
                    {
                        if (cardIds.Count < 4)
                        {
                            SendErrorResponse(writer, HttpStatusCode.BadRequest, "A deck must contain at least 4 cards.");
                        }

                        var cards = user.Inventory.OwnedCards
                            .Where(card => cardIds.Contains(card.ID) && !card.InTrade)
                            .ToList();
                        if (cards.Count == cardIds.Count && cards.Count >= 4)
                        {
                            foreach (var card in cards)
                            {
                                card.InDeck = true;
                            }

                            user.Inventory.Deck.Cards = cards;
                            _userServiceHandler.UpdateUserInventory(user);
                            SendResponse(writer, "Deck configured successfully", "application/text", HttpStatusCode.OK);
                        }
                        else
                        {
                            SendErrorResponse(writer, HttpStatusCode.BadRequest, "Some cards not found in user's inventory or are in trade");
                        }
                    }
                    else
                    {
                        SendErrorResponse(writer, HttpStatusCode.BadRequest, "Invalid card IDs");
                    }
                }
                catch (JsonException ex)
                {
                    SendErrorResponse(writer, HttpStatusCode.BadRequest, $"Invalid JSON format: {ex.Message}");
                }
            }
            else
            {
                SendErrorResponse(writer, HttpStatusCode.Unauthorized, "Invalid user");
            }
        }

        private async Task HandleBattleRequestAsync(StreamReader request, StreamWriter response)
        {
            var user = await IsIdentiyYesUserCookie(request, response);

            if (user == null)
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Invalid user");
                return;
            }

            lock (_battleQueue)
            {
                if (_battleQueue.Any(u => u.Id == user.Id))
                {
                    SendErrorResponse(response, HttpStatusCode.BadRequest, "User already in queue");
                    return;
                }
                _battleQueue.Enqueue(user);
            }

            var tcs = new TaskCompletionSource<string>();
            _userResponses[user.Id] = tcs;

            try
            {
                if (_battleQueue.Count >= 2)
                {
                    lock (_battleQueue)
                    {
                        if (_battleQueue.Count >= 2 &&
                            _battleQueue.TryDequeue(out var user1) &&
                            _battleQueue.TryDequeue(out var user2))
                        {
                            if (user1.Id == user2.Id)
                            {
                                SendErrorResponse(response, HttpStatusCode.BadRequest, "Cannot fight yourself");
                                return;
                            }

                            Console.WriteLine(user1.Username);
                            Console.WriteLine(user2.Username);

                            var fightLogic = new FightLogic(user1, user2);
                            var battleResult = fightLogic.StartBattleAsync();

                            // Update user ELO
                            UpdateELO(fightLogic, user1, user2);

                            // Serialize response
                            string jsonResponse = SerializeToJson(battleResult);

                            CompleteBattleResponse(user1.Id, jsonResponse);
                            CompleteBattleResponse(user2.Id, jsonResponse);
                        }
                    }
                }

                string result = await tcs.Task;
                SendResponse(response, result, "application/json", HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling request: {ex.Message}");
                SendErrorResponse(response, HttpStatusCode.InternalServerError, "An error occurred while processing the request.");
            }
            finally
            {
                _userResponses.TryRemove(user.Id, out _);
            }
        }

        private void UpdateELO(FightLogic fightLogic, User user1, User user2)
        {
            if (fightLogic.winner == user1.Username)
            {
                user1.Inventory.ELO += 3;
                user2.Inventory.ELO -= 5;
            }
            else if (fightLogic.winner == user2.Username)
            {
                user2.Inventory.ELO += 3;
                user1.Inventory.ELO -= 5;
            }

            _userServiceHandler.UpdateUser(user1.Id, user1);
            _userServiceHandler.UpdateUser(user2.Id, user2);
        }

        private void CompleteBattleResponse(int userId, string jsonResponse)
        {
            if (_userResponses.TryRemove(userId, out var tcs))
            {
                tcs.SetResult(jsonResponse);
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
            await Task.CompletedTask;
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
                var inventory = user.Inventory;
                string responseContent = $"Login successful. Token: {token}";

                if (inventory != null)
                {
                    string inventoryHtml = _htmlgen.GenerateInventoryHtml(inventory);
                    responseContent += "\n" + inventoryHtml;
                }

                response.WriteLine("HTTP/1.1 200 OK");
                response.WriteLine($"Set-Cookie: authToken={token}; Path=/;");
                response.WriteLine($"Set-Cookie: userData=username={user.Username}&password={user.Password}&userid={user.Id};");
                response.WriteLine("Content-Type: text/html");
                response.WriteLine($"Content-Length: {responseContent.Length}");
                response.WriteLine();
                response.WriteLine(responseContent);
                response.Flush();
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Invalid username or password");
            }
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

        private async Task HandleOpenCardPackAsync(StreamReader reader, StreamWriter writer)
        {
            var userinfo = await IsIdentiyYesUserCookie(reader, writer);

            if (userinfo != null)
            {
                if (userinfo.Inventory.CardPacks.Count > 0)
                {
                    List<Card> Liste = new List<Card>();
                    Liste = _userServiceHandler.OpenCardPack(userinfo.Id, userinfo.Username, userinfo.Password);

                    string jsonResponse = SerializeToJson(Liste);
                    writer.WriteLine("HTTP/1.1 200 OK");
                    writer.WriteLine("Content-Type: application/json");
                    writer.WriteLine($"Content-Length: {jsonResponse.Length}");
                    writer.WriteLine();
                    writer.WriteLine(jsonResponse);
                    writer.Flush();
                }
                else
                {
                    SendErrorResponse(writer, HttpStatusCode.Conflict, "Nothing to Open");
                }
            }
            else
            {
                SendErrorResponse(writer, HttpStatusCode.Unauthorized, "Invalid at HandleOpenCard");
            }
        }

        private async Task HandleBuyPacksAsync(StreamReader reader, StreamWriter writer)
        {
            var user = await IsIdentiyYesUserCookie(reader, writer);
            string requestBodyString = await ReadRequestBodyAsync(reader, writer);

            if (user != null)
            {
                string[] parameters = requestBodyString.Split('&');
                int amount = 0;

                foreach (var param in parameters)
                {
                    string[] keyValue = param.Split('=');
                    if (keyValue[0] == "Amount" && int.TryParse(keyValue[1], out amount))
                    {
                        break;
                    }
                }

                if (amount > 0)
                {
                    try { _userServiceHandler.BuyPacks(user.Id, amount, user.Username, user.Password); }
                    catch (InvalidOperationException ex)
                    {
                        SendErrorResponse(writer, HttpStatusCode.BadRequest, ex.Message);
                        return;
                    }

                    _userServiceHandler.UpdateUserInventory(user.Id, user);

                    string jsonResponse = SerializeToJson(new { message = "Packs bought successfully" });
                    writer.WriteLine("HTTP/1.1 200 OK");
                    writer.WriteLine("Content-Type: application/json");
                    writer.WriteLine($"Content-Length: {jsonResponse.Length}");
                    writer.WriteLine();
                    writer.WriteLine(jsonResponse);
                    writer.Flush();
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

        private async Task HandleBuyPacksCURLAsync(StreamReader reader, StreamWriter writer)
        {
            var user = await IsIdentiyYesUserCookie(reader, writer);

            if (user != null)
            {
                int amount = 1;

                if (user.Inventory.Money < 5)
                {
                    SendErrorResponse(writer, HttpStatusCode.BadRequest, "Not enough money.");
                    return;
                }

                _userServiceHandler.BuyPacks(user.Id, amount, user.Username, user.Password);
                _userServiceHandler.UpdateUser(user.Id, user);
                string jsonResponse = SerializeToJson(new { message = "Packs bought successfully" });

                writer.WriteLine("HTTP/1.1 200 OK");
                writer.WriteLine("Content-Type: application/json");
                writer.WriteLine($"Content-Length: {jsonResponse.Length}");
                writer.WriteLine();
                writer.WriteLine(jsonResponse);
                writer.Flush();
            }
            else
            {
                SendErrorResponse(writer, HttpStatusCode.Unauthorized, "Invalid username or password.");
            }
        }

        private async Task HandleAddCardToDeckAsync(StreamReader reader, StreamWriter writer)
        {
            try
            {
                var user = await IsIdentiyYesUserCookie(reader, writer);
                string requestBodyString = await ReadRequestBodyAsync(reader, writer);

                if (user != null)
                {
                    var parameters = requestBodyString.Split('&');
                    List<int> cardIndices = new List<int>();

                    foreach (var param in parameters)
                    {
                        string[] keyValue = param.Split('=');
                        if (keyValue[0] == "cardIndices" && int.TryParse(keyValue[1], out int index))
                        {
                            cardIndices.Add(index);
                        }
                    }

                    if (cardIndices != null)
                    {
                        int[] cardPositions = cardIndices.Select(index => index).ToArray();
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

        private void SendResponse(StreamWriter writer, string content, string contentType, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            writer.WriteLine($"HTTP/1.1 {(int)statusCode} {statusCode}");
            writer.WriteLine("Content-Type: " + contentType);
            writer.WriteLine("Content-Length: " + content.Length);
            writer.WriteLine();
            writer.Write(content);
            writer.Flush();
        }

        private void SendResponseWeb(StreamWriter writer, string content, string contentType, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
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
            string[] blackList = { "'", "\"", "--", ";", "/*", "*/", "xp_" };
            foreach (var item in blackList)
            {
                if (input.Contains(item))
                {
                    return false;
                }
            }

            if (input.Length > 20)
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

        private async Task<User?> IsIdentiyYesUser(StreamReader reader, StreamWriter writer)
        {
            if (!writer.BaseStream.CanWrite)
            {
                Console.WriteLine("Error");
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
                var formData = requestBodyString.Split('&')
                    .Select(part => part.Split('='))
                    .ToDictionary(split => split[0], split => split.Length > 1 ? split[1] : string.Empty);
                username = formData.ContainsKey("username") ? formData["username"] : null;
                password = formData.ContainsKey("password") ? formData["password"] : null;
            }

            if (username == null || password == null)
            {
                SendErrorResponse(writer, HttpStatusCode.BadRequest);
                return null;
            }

            var authenticatedUser = _userServiceHandler.AuthenticateUser(username, password);

            if (authenticatedUser != null)
                return authenticatedUser;
            else
            {
                SendErrorResponse(writer, HttpStatusCode.BadRequest);
                return null;
            }
        }

        private async Task<User?> IsIdentiyYesUserCookie(StreamReader reader, StreamWriter writer)
        {
            if (!writer.BaseStream.CanWrite)
            {
                Console.WriteLine("Error");
                return null;
            }

            string requestBodyString = await ReadRequestBodyCookieAsync(reader, writer);

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
                var formData = requestBodyString.Split('&')
                    .Select(part => part.Split('='))
                    .ToDictionary(split => split[0], split => split.Length > 1 ? split[1] : string.Empty);
                username = formData.ContainsKey("username") ? formData["username"] : null;
                password = formData.ContainsKey("password") ? formData["password"] : null;
            }

            if (username == null || password == null)
            {
                SendErrorResponse(writer, HttpStatusCode.BadRequest);
                return null;
            }

            var authenticatedUser = _userServiceHandler.AuthenticateUser(username, password);

            if (authenticatedUser != null)
                return authenticatedUser;
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
                    SendErrorResponse(writer, HttpStatusCode.BadRequest, "Incomplete request body.");
                    return string.Empty;
                }
                totalBytesRead += bytesRead;
            }

            string requestBodyString = new string(requestBody);
            Console.WriteLine($"Received request body: {requestBodyString}");
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            reader.DiscardBufferedData();

            return requestBodyString;
        }

        private async Task<string> ReadRequestBodyCookieAsync(StreamReader reader, StreamWriter writer)
        {
            string requestLine = await reader.ReadLineAsync();
            if (requestLine == null)
            {
                Console.WriteLine("No request line received.");
                SendErrorResponse(writer, HttpStatusCode.BadRequest);
                return string.Empty;
            }
            Console.BackgroundColor = ConsoleColor.Red;
            Console.WriteLine($"Request line: {requestLine}");

            string? line;
            string? userDataCookie = null;
            while ((line = await reader.ReadLineAsync()) != null && line != "")
            {
                if (line.StartsWith("Cookie:"))
                {
                    Console.WriteLine($"Request line: {line}");
                    var cookies = line.Substring("Cookie:".Length).Trim().Split(';');

                    foreach (var cookie in cookies)
                    {
                        var trimmedCookie = cookie.Trim();

                        if (trimmedCookie.StartsWith("userData="))
                        {
                            userDataCookie = trimmedCookie.Substring("userData=".Length);
                            break;
                        }
                    }
                }
                else if (line.StartsWith("Authorization: Bearer"))
                {
                    var parts = line.Split(' ');
                    if (parts.Length == 3)
                    {
                        var tokenParts = parts[2].Split('-');
                        if (tokenParts.Length == 2 && tokenParts[1] == "mtcgToken")
                        {
                            string username = tokenParts[0];
                            Console.WriteLine($"Extracted Username: {username}");
                            var user1 = _userServiceHandler.GetUserByName(username);
                            userDataCookie = $"{{\"Username\":\"{user1.Username}\",\"Password\":\"{user1.Password}\"}}";
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

            // Reset the reader to start of stream workarround to get it back
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            reader.DiscardBufferedData();
            Console.ResetColor();
            return userDataCookie;
        }

        private void SendErrorResponse(StreamWriter writer, HttpStatusCode statusCode)
        {
            writer.WriteLine($"HTTP/1.1  {statusCode}");
            writer.WriteLine("Content-Length: 0");
            writer.Flush();
        }

        private async Task HandleAddTradingDealAsync(StreamReader request, StreamWriter response)
        {
            var user = await IsIdentiyYesUserCookie(request, response);
            if (user != null)
            {
                string requestBody = await ReadRequestBodyAsync(request, response);
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new TradingLogicJsonConverter() }
                    };

                    var deal = JsonSerializer.Deserialize<TradingLogic>(requestBody, options);
                    if (deal != null)
                    {
                        deal.UserId = user.Id;
                        try { _userServiceHandler.AddTradingDeal(deal, user); }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Unexpected error: {ex.Message}");
                            SendErrorResponse(response, HttpStatusCode.InternalServerError, $"Unexpected error: {ex.Message}");
                        }

                        SendResponse(response, "Trading deal created successfully", "application/text", HttpStatusCode.Created);
                    }
                    else
                    {
                        SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid trading deal data");
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"JSON deserialization error: {ex.Message}");
                    SendErrorResponse(response, HttpStatusCode.BadRequest, $"Invalid JSON format: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error: {ex.Message}");
                    SendErrorResponse(response, HttpStatusCode.InternalServerError, $"Unexpected error: {ex.Message}");
                }
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Invalid user");
            }
        }

        private async Task HandleExecuteTradeAsync(StreamReader request, StreamWriter response, string path)
        {
            var user = await IsIdentiyYesUserCookie(request, response);
            if (user != null)
            {
                string dealIdString = path.Substring(10);
                if (Guid.TryParse(dealIdString, out Guid dealId))
                {
                    string requestBody = await ReadRequestBodyAsync(request, response);
                    if (Guid.TryParse(requestBody.Trim('"'), out Guid cardId))
                    {
                        if (user.Inventory.OwnedCards.Any(c => c.ID == cardId))
                        {
                            _userServiceHandler.ExecuteTrade(dealId, cardId, user.Id);
                            SendResponse(response, "Trade executed successfully", "application/text", HttpStatusCode.OK);
                        }
                        else
                        {
                            SendErrorResponse(response, HttpStatusCode.BadRequest, "Card not found in user's inventory");
                        }
                    }
                    else
                    {
                        SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid card ID");
                    }
                }
                else
                {
                    SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid trading deal ID");
                }
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Invalid user");
            }
        }

        private async Task HandleDeleteTradingDealAsync(StreamReader request, StreamWriter response, string path)
        {
            var user = await IsIdentiyYesUserCookie(request, response);
            if (user != null)
            {
                string dealIdString = path.Substring(10);
                if (Guid.TryParse(dealIdString, out Guid dealId))
                {
                    var deal = _userServiceHandler.GetTradingDealById(dealId);
                    if (deal != null && deal.UserId == user.Id)
                    {
                        _userServiceHandler.DeleteTradingDeal(dealId);
                        SendResponse(response, "Trading deal deleted successfully", "application/text", HttpStatusCode.OK);
                    }
                    else
                    {
                        SendErrorResponse(response, HttpStatusCode.BadRequest, "Trading deal not found or user not authorized");
                    }
                }
                else
                {
                    SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid trading deal ID");
                }
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Invalid user");
            }
        }

        private async Task HandleAddPackagesAsync(StreamReader request, StreamWriter response)
        {
            var user = await IsIdentiyYesUserCookie(request, response);
            if (user != null && user.Username == "admin")
            {
                string requestBody = await ReadRequestBodyAsync(request, response);
                try
                {
                    var cardDataList = JsonSerializer.Deserialize<List<CardData>>(requestBody);
                    if (cardDataList != null)
                    {
                        var cards = cardDataList.Select(data =>
                        {
                            return CardCreator9000.CreateCard(data);
                        }).ToList();
                        _userServiceHandler.CreatePack(cards);
                        SendResponse(response, "Packages added successfully", "application/json", HttpStatusCode.Created);
                    }
                    else
                    {
                        SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid package data");
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"JSON deserialization error: {ex.Message}");
                    SendErrorResponse(response, HttpStatusCode.BadRequest, $"Invalid JSON format: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error: {ex.Message}");
                    SendErrorResponse(response, HttpStatusCode.InternalServerError, $"Unexpected error: {ex.Message}");
                }
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Unauthorized user");
            }
        }

        private async Task HandleGetUserByUsernameAsync(StreamReader request, StreamWriter response, string path)
        {
            string username = path.Substring(7);

            var user = await IsIdentiyYesUserCookie(request, response);

            if (user.Username == username)
            {
                if (user != null)
                {
                    string jsonResponse = SerializeToJson(user);
                    SendResponse(response, jsonResponse, "application/json");
                }
                else
                {
                    SendErrorResponse(response, HttpStatusCode.BadRequest, "User not found");
                }
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Unauthorized");
            }
        }

        private async Task HandleUpdateUserAsync(StreamReader request, StreamWriter response, string path)
        {
            var user = await IsIdentiyYesUserCookie(request, response);
            if (user != null)
            {
                string username = path.Substring(7);
                if (user.Username == username)
                {
                    string requestBody = await ReadRequestBodyAsync(request, response);
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(requestBody))
                        {
                            JsonElement root = doc.RootElement;

                            if (root.TryGetProperty("Name", out JsonElement nameElement))
                            {
                                user.Name = nameElement.GetString();
                            }

                            if (root.TryGetProperty("Bio", out JsonElement bioElement))
                            {
                                user.Bio = bioElement.GetString();
                            }

                            if (root.TryGetProperty("Image", out JsonElement imageElement))
                            {
                                user.Image = imageElement.GetString();
                            }
                        }

                        _userServiceHandler.UpdateUser(user.Id, user);
                        SendResponse(response, "User updated successfully", "application/json", HttpStatusCode.OK);
                    }
                    catch (JsonException ex)
                    {
                        SendErrorResponse(response, HttpStatusCode.BadRequest, $"Invalid JSON format: {ex.Message}");
                    }
                }
                else
                {
                    SendErrorResponse(response, HttpStatusCode.Unauthorized, "Unauthorized");
                }
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Invalid user");
            }
        }

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

                    case "PUT":
                        await HandlePutRequestAsync(request, response, path);
                        break;

                    case "DELETE":
                        await HandleDeleteRequestAsync(request, response, path);
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
    }
}