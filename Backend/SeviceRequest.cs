using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SemesterProjekt1
{
    internal enum HttpStatusCode
    {
        OK = 200,
        Created = 201,
        BadRequest = 400,
        Unauthorized = 401,
        Conflict = 409,
        TooManyRequests = 429,
        InternalServerError = 500
    }

    public class UserServiceRequest
    {
        // Maximale Request-Größen zur Vermeidung von DoS
        private const int MAX_REQUEST_SIZE = 1024 * 1024; // 1MB

        private const int MAX_USERNAME_LENGTH = 20;
        private const int MAX_PASSWORD_LENGTH = 100;
        private const int MAX_LOGIN_ATTEMPTS = 5;
        private const int LOCKOUT_DURATION_MINUTES = 15;

        private const int TOKEN_EXPIRY_HOURS = 24;
        private static ConcurrentQueue<User> _battleQueue = new ConcurrentQueue<User>();
        private static ConcurrentDictionary<int, TaskCompletionSource<string>> _userResponses = new ConcurrentDictionary<int, TaskCompletionSource<string>>();

        // Rate Limiting für Login-Versuche
        private static ConcurrentDictionary<string, (int attempts, DateTime lockoutUntil)> _loginAttempts = new ConcurrentDictionary<string, (int, DateTime)>();

        // Sichere Token-Verwaltung
        private static ConcurrentDictionary<string, (int userId, DateTime expiry)> _activeTokens = new ConcurrentDictionary<string, (int, DateTime)>();

        private HTMLGEN _htmlgen;
        public UserServiceHandler _userServiceHandler;

        public UserServiceRequest()
        {
            _userServiceHandler = new UserServiceHandler();
            _htmlgen = new HTMLGEN(_userServiceHandler);
        }

        // Sichere Token-Generierung
        private static string GenerateSecureToken()
        {
            byte[] tokenBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            return Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        // Token-Validierung
        private int? ValidateToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            if (_activeTokens.TryGetValue(token, out var tokenData))
            {
                if (DateTime.UtcNow < tokenData.expiry)
                {
                    return tokenData.userId;
                }
                else
                {
                    // Abgelaufenes Token entfernen
                    _activeTokens.TryRemove(token, out _);
                }
            }
            return null;
        }

        // Rate Limiting Check
        private bool CheckRateLimit(string identifier)
        {
            var now = DateTime.UtcNow;

            if (_loginAttempts.TryGetValue(identifier, out var data))
            {
                if (now < data.lockoutUntil)
                {
                    return false; // Noch im Lockout
                }

                if (data.attempts >= MAX_LOGIN_ATTEMPTS)
                {
                    // Lockout aktivieren
                    _loginAttempts[identifier] = (data.attempts, now.AddMinutes(LOCKOUT_DURATION_MINUTES));
                    return false;
                }
            }

            return true;
        }

        private void IncrementLoginAttempt(string identifier)
        {
            _loginAttempts.AddOrUpdate(
                identifier,
                (1, DateTime.MinValue),
                (key, old) => (old.attempts + 1, old.lockoutUntil)
            );
        }

        private void ResetLoginAttempts(string identifier)
        {
            _loginAttempts.TryRemove(identifier, out _);
        }

        private void SendErrorResponse(StreamWriter writer, HttpStatusCode statusCode, string message)
        {
            message = SanitizeErrorMessage(message);

            writer.WriteLine($"HTTP/1.1 {(int)statusCode} {statusCode}");
            writer.WriteLine("Content-Type: text/plain");
            writer.WriteLine("Content-Length: " + message.Length);
            writer.WriteLine("X-Content-Type-Options: nosniff");
            writer.WriteLine("X-Frame-Options: DENY");
            writer.WriteLine("X-XSS-Protection: 1; mode=block");
            writer.WriteLine("Strict-Transport-Security: max-age=31536000; includeSubDomains");
            writer.WriteLine("Content-Security-Policy: default-src 'self'");
            writer.WriteLine();
            writer.Write(message);
            writer.Flush();
        }

        private string SanitizeErrorMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "An error occurred";

            // Entfernt potenziell gefährliche Zeichen und begrenzt Länge
            message = Regex.Replace(message, @"[<>""']", "");
            return message.Length > 200 ? message.Substring(0, 200) : message;
        }

        private async Task HandleGetRequestAsync(StreamReader request, StreamWriter response, string path1)
        {
            // Path Traversal Protection
            if (path1.Contains("..") || path1.Contains("%2e%2e") || path1.Contains("%00"))
            {
                SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid path");
                return;
            }

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
                        var user = await AuthenticateFromToken(request, response);
                        if (user == null)
                        {
                            SendErrorResponse(response, HttpStatusCode.Unauthorized, "Unauthorized");
                            return;
                        }

                        var allUsers = _userServiceHandler.GetAllUsers();
                        // Passwörter aus Response entfernen
                        var sanitizedUsers = allUsers.Select(u => new
                        {
                            u.Id,
                            u.Username,
                            u.Name,
                            u.Bio,
                            u.Image,
                            ELO = u.Inventory?.ELO
                        });
                        string jsonResponse = SerializeToJson(sanitizedUsers);
                        SendResponse(response, jsonResponse, "application/json");
                        break;
                    }
                case "/cards":
                    {
                        var user = await AuthenticateFromToken(request, response);
                        if (user == null)
                        {
                            SendErrorResponse(response, HttpStatusCode.Unauthorized, "Unauthorized");
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
                case "/logout":
                    {
                        await HandleLogout(request, response);
                        break;
                    }
                case "/tradings":
                    {
                        var deals = _userServiceHandler.GetAllTradingDeals();
                        string jsonResponse = SerializeToJson(deals);
                        SendResponse(response, jsonResponse, "application/json");
                        break;
                    }
                case "/stats":
                    {
                        var user = await AuthenticateFromToken(request, response);
                        if (user == null)
                        {
                            SendErrorResponse(response, HttpStatusCode.Unauthorized, "Unauthorized");
                        }
                        else
                        {
                            var stats = new
                            {
                                Username = user.Username,
                                ELO = user.Inventory.ELO,
                                CardCount = user.Inventory.OwnedCards.Count,
                                DeckSize = user.Inventory.Deck.Cards.Count
                            };
                            string jsonResponse = SerializeToJson(stats);
                            SendResponse(response, jsonResponse, "application/json");
                        }
                        break;
                    }
                case "/deck":
                    {
                        var user = await AuthenticateFromToken(request, response);
                        if (user == null)
                        {
                            SendErrorResponse(response, HttpStatusCode.Unauthorized, "Unauthorized");
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
                        response.Flush();
                        break;
                    }
            }
        }

        private async Task HandlePostRequestAsync(StreamReader request, StreamWriter response, string path1)
        {
            if (path1.Contains("..") || path1.Contains("%2e%2e") || path1.Contains("%00"))
            {
                SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid path");
                return;
            }

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
            if (path1.Contains("..") || path1.Contains("%2e%2e") || path1.Contains("%00"))
            {
                SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid path");
                return;
            }

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
            if (path1.Contains("..") || path1.Contains("%2e%2e") || path1.Contains("%00"))
            {
                SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid path");
                return;
            }

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
            var user = await AuthenticateFromToken(reader, writer);
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
                            return;
                        }

                        if (cardIds.Count > 20)
                        {
                            SendErrorResponse(writer, HttpStatusCode.BadRequest, "A deck cannot contain more than 20 cards.");
                            return;
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
                    SendErrorResponse(writer, HttpStatusCode.BadRequest, "Invalid JSON format");
                }
            }
            else
            {
                SendErrorResponse(writer, HttpStatusCode.Unauthorized, "Invalid user");
            }
        }

        private async Task HandleBattleRequestAsync(StreamReader request, StreamWriter response)
        {
            var user = await AuthenticateFromToken(request, response);

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

                            UpdateELO(fightLogic, user1, user2);

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
                user2.Inventory.ELO = Math.Max(0, user2.Inventory.ELO - 5); // Verhindert negative ELO
            }
            else if (fightLogic.winner == user2.Username)
            {
                user2.Inventory.ELO += 3;
                user1.Inventory.ELO = Math.Max(0, user1.Inventory.ELO - 5);
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
            var authenticatedUser = await AuthenticateFromToken(request, response);
            if (authenticatedUser == null)
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Unauthorized");
                return;
            }

            string userIdString = path.Substring(6);

            if (int.TryParse(userIdString, out int userId))
            {
                if (userId < 0 || userId > int.MaxValue)
                {
                    SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid user ID");
                    return;
                }

                var user = _userServiceHandler.GetUserById(userId);
                if (user != null)
                {
                    // Sanitize user data - entferne Passwort
                    var sanitizedUser = new
                    {
                        user.Id,
                        user.Username,
                        user.Name,
                        user.Bio,
                        user.Image,
                        ELO = user.Inventory?.ELO
                    };
                    string jsonResponse = SerializeToJson(sanitizedUser);
                    SendResponse(response, jsonResponse, "application/json");
                }
                else
                {
                    SendErrorResponse(response, HttpStatusCode.BadRequest, "User not found");
                }
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid user ID format");
            }
            await Task.CompletedTask;
        }

        private async Task HandleAddUserAsync(StreamReader request, StreamWriter response)
        {
            string requestBody = await ReadRequestBodyAsync(request, response);

            var user = DeserializeUser(requestBody);
            if (user != null && (IsValidInput(user.Username) && IsValidPassword(user.Password)))
            {
                if (user.Username.Length > MAX_USERNAME_LENGTH || user.Password.Length > MAX_PASSWORD_LENGTH)
                {
                    SendErrorResponse(response, HttpStatusCode.BadRequest, "Username or password too long");
                    return;
                }

                var existingUser = _userServiceHandler.GetUserByName(user.Username);
                if (existingUser == null)
                {
                    // WICHTIG: Hier sollte das Passwort gehasht werden vor dem Speichern
                    // user.Password = HashPassword(user.Password);
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
            string requestBody = await ReadRequestBodyAsync(request, response);

            // Extrahiere Credentials
            string? username = null;
            string? password = null;

            if (IsJson(requestBody))
            {
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, MaxDepth = 32 };
                    var loginData = JsonSerializer.Deserialize<User>(requestBody, options);
                    if (loginData != null)
                    {
                        username = loginData.Username;
                        password = loginData.Password;
                    }
                }
                catch (JsonException)
                {
                    SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid JSON");
                    return;
                }
            }
            else
            {
                var formData = requestBody.Split('&')
                    .Select(part => part.Split('='))
                    .Where(split => split.Length == 2)
                    .ToDictionary(split => Uri.UnescapeDataString(split[0]), split => Uri.UnescapeDataString(split[1]));

                username = formData.ContainsKey("username") ? formData["username"] : null;
                password = formData.ContainsKey("password") ? formData["password"] : null;
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                SendErrorResponse(response, HttpStatusCode.BadRequest, "Missing credentials");
                return;
            }

            // Rate Limiting Check
            if (!CheckRateLimit(username))
            {
                SendErrorResponse(response, HttpStatusCode.TooManyRequests, "Too many login attempts. Please try again later.");
                return;
            }

            var user = _userServiceHandler.AuthenticateUser(username, password);

            if (user != null)
            {
                ResetLoginAttempts(username);

                // Generiere sicheres Token
                string token = GenerateSecureToken();
                var expiry = DateTime.UtcNow.AddHours(TOKEN_EXPIRY_HOURS);
                _activeTokens[token] = (user.Id, expiry);

                var inventory = user.Inventory;
                string responseContent = "Login successful";

                if (inventory != null)
                {
                    string inventoryHtml = _htmlgen.GenerateInventoryHtml(inventory);
                    responseContent += "\n" + inventoryHtml;
                }

                response.WriteLine("HTTP/1.1 200 OK");
                response.WriteLine($"Set-Cookie: authToken={token}; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age={TOKEN_EXPIRY_HOURS * 3600}");
                response.WriteLine("Content-Type: text/html");
                response.WriteLine($"Content-Length: {responseContent.Length}");
                response.WriteLine("X-Content-Type-Options: nosniff");
                response.WriteLine("X-Frame-Options: DENY");
                response.WriteLine("X-XSS-Protection: 1; mode=block");
                response.WriteLine("Strict-Transport-Security: max-age=31536000; includeSubDomains");
                response.WriteLine();
                response.WriteLine(responseContent);
                response.Flush();
            }
            else
            {
                IncrementLoginAttempt(username);
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Invalid username or password");
            }
        }

        private async Task HandleLoginAsyncCURL(StreamReader reader, StreamWriter writer)
        {
            if (!writer.BaseStream.CanWrite)
            {
                Console.WriteLine("Error");
                return;
            }

            string requestBody = await ReadRequestBodyAsync(reader, writer);

            string? username = null;
            string? password = null;

            if (IsJson(requestBody))
            {
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, MaxDepth = 32 };
                    var user1 = JsonSerializer.Deserialize<User>(requestBody, options);
                    if (user1 != null)
                    {
                        username = user1.Username;
                        password = user1.Password;
                    }
                }
                catch (JsonException)
                {
                    SendErrorResponse(writer, HttpStatusCode.BadRequest, "Invalid JSON");
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                SendErrorResponse(writer, HttpStatusCode.BadRequest, "Missing credentials");
                return;
            }

            if (!CheckRateLimit(username))
            {
                SendErrorResponse(writer, HttpStatusCode.TooManyRequests, "Too many login attempts");
                return;
            }

            try
            {
                var authenticatedUser = _userServiceHandler.AuthenticateUser(username, password);
                if (authenticatedUser != null)
                {
                    ResetLoginAttempts(username);

                    string token = GenerateSecureToken();
                    var expiry = DateTime.UtcNow.AddHours(TOKEN_EXPIRY_HOURS);
                    _activeTokens[token] = (authenticatedUser.Id, expiry);

                    string response = $"HTTP/1.1 200 OK\r\n" +
                                    $"Set-Cookie: authToken={token}; HttpOnly; Secure; SameSite=Strict; Max-Age={TOKEN_EXPIRY_HOURS * 3600}\r\n" +
                                    $"Content-Type: text/plain\r\n" +
                                    $"Content-Length: {token.Length}\r\n" +
                                    $"X-Content-Type-Options: nosniff\r\n" +
                                    $"X-Frame-Options: DENY\r\n" +
                                    $"Strict-Transport-Security: max-age=31536000; includeSubDomains\r\n" +
                                    $"\r\n{token}";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    await writer.BaseStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    await writer.BaseStream.FlushAsync();
                }
                else
                {
                    IncrementLoginAttempt(username);
                    SendErrorResponse(writer, HttpStatusCode.Unauthorized, "Authentication failed");
                }
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"I/O error during login: {ioEx.Message}");
                SendErrorResponse(writer, HttpStatusCode.InternalServerError, "Internal server error");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during login: {ex.Message}");
                SendErrorResponse(writer, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }

        private async Task HandleLogout(StreamReader reader, StreamWriter writer)
        {
            try
            {
                // Token aus Request extrahieren und invalidieren
                string token = await ExtractTokenFromRequest(reader);
                if (!string.IsNullOrEmpty(token))
                {
                    _activeTokens.TryRemove(token, out _);
                }

                writer.WriteLine("HTTP/1.1 200 OK");
                writer.WriteLine("Set-Cookie: authToken=; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Path=/; HttpOnly; Secure; SameSite=Strict");
                writer.WriteLine("Content-Type: text/plain");
                writer.WriteLine("X-Content-Type-Options: nosniff");
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

        private async Task<string> ExtractTokenFromRequest(StreamReader reader)
        {
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            reader.DiscardBufferedData();

            string? line;
            string? token = null;

            while ((line = await reader.ReadLineAsync()) != null && line != "")
            {
                if (line.StartsWith("Cookie:"))
                {
                    var cookies = line.Substring("Cookie:".Length).Trim().Split(';');
                    foreach (var cookie in cookies)
                    {
                        var trimmedCookie = cookie.Trim();
                        if (trimmedCookie.StartsWith("authToken="))
                        {
                            token = trimmedCookie.Substring("authToken=".Length);
                            break;
                        }
                    }
                }
                else if (line.StartsWith("Authorization: Bearer "))
                {
                    token = line.Substring("Authorization: Bearer ".Length).Trim();
                }
            }

            if (string.IsNullOrEmpty(token))
            {
                return string.Empty;
            }

            return token;
        }

        private async Task HandleOpenCardPackAsync(StreamReader reader, StreamWriter writer)
        {
            var userinfo = await AuthenticateFromToken(reader, writer);

            if (userinfo != null)
            {
                if (userinfo.Inventory.CardPacks.Count > 0)
                {
                    List<Card> Liste = _userServiceHandler.OpenCardPack(userinfo.Id, userinfo.Username, userinfo.Password);

                    string jsonResponse = SerializeToJson(Liste);
                    writer.WriteLine("HTTP/1.1 200 OK");
                    writer.WriteLine("Content-Type: application/json");
                    writer.WriteLine($"Content-Length: {jsonResponse.Length}");
                    writer.WriteLine("X-Content-Type-Options: nosniff");
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
                SendErrorResponse(writer, HttpStatusCode.Unauthorized, "Unauthorized");
            }
        }

        private async Task HandleBuyPacksAsync(StreamReader reader, StreamWriter writer)
        {
            var user = await AuthenticateFromToken(reader, writer);
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

                if (amount <= 0 || amount > 100)
                {
                    SendErrorResponse(writer, HttpStatusCode.BadRequest, "Invalid amount. Must be between 1 and 100.");
                    return;
                }

                if (amount > 0)
                {
                    try
                    {
                        _userServiceHandler.BuyPacks(user.Id, amount, user.Username, user.Password);
                    }
                    catch (InvalidOperationException)
                    {
                        SendErrorResponse(writer, HttpStatusCode.BadRequest, "Unable to purchase packs");
                        return;
                    }

                    _userServiceHandler.UpdateUser(user.Id, user);

                    string jsonResponse = SerializeToJson(new { message = "Packs bought successfully" });
                    writer.WriteLine("HTTP/1.1 200 OK");
                    writer.WriteLine("Content-Type: application/json");
                    writer.WriteLine($"Content-Length: {jsonResponse.Length}");
                    writer.WriteLine("X-Content-Type-Options: nosniff");
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
            var user = await AuthenticateFromToken(reader, writer);

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
                writer.WriteLine("X-Content-Type-Options: nosniff");
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
                var user = await AuthenticateFromToken(reader, writer);
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
                            if (index >= 0 && index < user.Inventory.OwnedCards.Count)
                            {
                                cardIndices.Add(index);
                            }
                        }
                    }

                    if (cardIndices.Count > 20)
                    {
                        SendErrorResponse(writer, HttpStatusCode.BadRequest, "Too many cards selected");
                        return;
                    }

                    if (cardIndices != null && cardIndices.Count > 0)
                    {
                        int[] cardPositions = cardIndices.Select(index => index).ToArray();
                        _userServiceHandler.AddCardToDeckHTTPVersion(user.Id, user.Username, user.Password, cardPositions);
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
                Console.WriteLine($"Error in HandleAddCardToDeckAsync: {ex.Message}");
                SendErrorResponse(writer, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }

        private void SendResponse(StreamWriter writer, string content, string contentType, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            writer.WriteLine($"HTTP/1.1 {(int)statusCode} {statusCode}");
            writer.WriteLine("Content-Type: " + contentType);
            writer.WriteLine("Content-Length: " + content.Length);
            writer.WriteLine("X-Content-Type-Options: nosniff");
            writer.WriteLine("X-Frame-Options: DENY");
            writer.WriteLine("X-XSS-Protection: 1; mode=block");
            writer.WriteLine("Strict-Transport-Security: max-age=31536000; includeSubDomains");
            writer.WriteLine("Content-Security-Policy: default-src 'self'");
            writer.WriteLine();
            writer.Write(content);
            writer.Flush();
        }

        private User DeserializeUser(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                MaxDepth = 32
            };
            return JsonSerializer.Deserialize<User>(json, options);
        }

        private string SerializeToJson(object obj)
        {
            var options = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                MaxDepth = 32
            };
            return JsonSerializer.Serialize(obj, options);
        }

        private bool IsValidInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // SQL Injection Schutz
            string[] blackList = { "'", "\"", "--", ";", "/*", "*/", "xp_", "exec", "execute", "script", "<", ">", "drop", "insert", "delete", "update", "union", "select" };
            foreach (var item in blackList)
            {
                if (input.ToLower().Contains(item.ToLower()))
                {
                    return false;
                }
            }

            if (input.Length > MAX_USERNAME_LENGTH)
            {
                return false;
            }

            // Nur alphanumerische Zeichen, Unterstriche und Bindestriche erlauben
            return Regex.IsMatch(input, @"^[a-zA-Z0-9_-]+$");
        }

        private bool IsValidPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            // Mindestlänge für Passwort
            if (password.Length < 8)
                return false;

            if (password.Length > MAX_PASSWORD_LENGTH)
                return false;

            // Passwort sollte mindestens einen Buchstaben und eine Zahl enthalten
            bool hasLetter = password.Any(char.IsLetter);
            bool hasDigit = password.Any(char.IsDigit);

            return hasLetter && hasDigit;
        }

        private bool IsJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();
            return (input.StartsWith("{") && input.EndsWith("}")) ||
                   (input.StartsWith("[") && input.EndsWith("]"));
        }

        // Neue Methode: Token-basierte Authentifizierung
        private async Task<User?> AuthenticateFromToken(StreamReader reader, StreamWriter writer)
        {
            if (!writer.BaseStream.CanWrite)
            {
                Console.WriteLine("Error");
                return null;
            }

            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            reader.DiscardBufferedData();

            string? line;
            string? token = null;

            while ((line = await reader.ReadLineAsync()) != null && line != "")
            {
                if (line.StartsWith("Cookie:"))
                {
                    var cookies = line.Substring("Cookie:".Length).Trim().Split(';');
                    foreach (var cookie in cookies)
                    {
                        var trimmedCookie = cookie.Trim();
                        if (trimmedCookie.StartsWith("authToken="))
                        {
                            token = trimmedCookie.Substring("authToken=".Length);
                            break;
                        }
                    }
                }
            }

            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            reader.DiscardBufferedData();

            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            int? userId = ValidateToken(token);
            if (userId.HasValue)
            {
                return _userServiceHandler.GetUserById(userId.Value);
            }

            return null;
        }

        private async Task<string> ReadRequestBodyAsync(StreamReader reader, StreamWriter writer)
        {
            string? requestLine = await reader.ReadLineAsync();
            if (requestLine == null)
            {
                Console.WriteLine("No request line received.");
                SendErrorResponse(writer, HttpStatusCode.BadRequest, "Invalid request");
                return string.Empty;
            }

            int contentLength = 0;
            string? line;
            while ((line = await reader.ReadLineAsync()) != null && line != "")
            {
                if (line.StartsWith("Content-Length:"))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out contentLength))
                    {
                        if (contentLength > MAX_REQUEST_SIZE)
                        {
                            SendErrorResponse(writer, HttpStatusCode.BadRequest, "Request too large");
                            return string.Empty;
                        }
                    }
                }
            }

            if (contentLength <= 0)
            {
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

        private void SendErrorResponse(StreamWriter writer, HttpStatusCode statusCode)
        {
            writer.WriteLine($"HTTP/1.1 {(int)statusCode} {statusCode}");
            writer.WriteLine("Content-Length: 0");
            writer.WriteLine("X-Content-Type-Options: nosniff");
            writer.WriteLine("X-Frame-Options: DENY");
            writer.WriteLine();
            writer.Flush();
        }

        private async Task HandleAddTradingDealAsync(StreamReader request, StreamWriter response)
        {
            var user = await AuthenticateFromToken(request, response);
            if (user != null)
            {
                string requestBody = await ReadRequestBodyAsync(request, response);
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        MaxDepth = 32,
                        Converters = { new TradingLogicJsonConverter() }
                    };

                    var deal = JsonSerializer.Deserialize<TradingLogic>(requestBody, options);
                    if (deal != null)
                    {
                        deal.UserId = user.Id;
                        try
                        {
                            _userServiceHandler.AddTradingDeal(deal, user);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Unexpected error: {ex.Message}");
                            SendErrorResponse(response, HttpStatusCode.InternalServerError, "Unable to create trading deal");
                            return;
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
                    SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid JSON format");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error: {ex.Message}");
                    SendErrorResponse(response, HttpStatusCode.InternalServerError, "Internal server error");
                }
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Invalid user");
            }
        }

        private async Task HandleExecuteTradeAsync(StreamReader request, StreamWriter response, string path)
        {
            var user = await AuthenticateFromToken(request, response);
            if (user != null)
            {
                string dealIdString = path.Substring(10);
                if (Guid.TryParse(dealIdString, out Guid dealId))
                {
                    string requestBody = await ReadRequestBodyAsync(request, response);

                    if (string.IsNullOrWhiteSpace(requestBody))
                    {
                        SendErrorResponse(response, HttpStatusCode.BadRequest, "Missing card ID");
                        return;
                    }

                    if (Guid.TryParse(requestBody.Trim('"'), out Guid cardId))
                    {
                        if (user.Inventory.OwnedCards.Any(c => c.ID == cardId))
                        {
                            try
                            {
                                _userServiceHandler.ExecuteTrade(dealId, cardId, user.Id);
                                SendResponse(response, "Trade executed successfully", "application/text", HttpStatusCode.OK);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Trade execution error: {ex.Message}");
                                SendErrorResponse(response, HttpStatusCode.InternalServerError, "Unable to execute trade");
                            }
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
            var user = await AuthenticateFromToken(request, response);
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
            var user = await AuthenticateFromToken(request, response);
            if (user != null && user.Username == "admin")
            {
                string requestBody = await ReadRequestBodyAsync(request, response);
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        MaxDepth = 32
                    };

                    var cardDataList = JsonSerializer.Deserialize<List<CardData>>(requestBody, options);
                    if (cardDataList != null)
                    {
                        // Limitiere Anzahl der Karten pro Request
                        if (cardDataList.Count > 100)
                        {
                            SendErrorResponse(response, HttpStatusCode.BadRequest, "Too many cards in request");
                            return;
                        }

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
                    SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid JSON format");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error: {ex.Message}");
                    SendErrorResponse(response, HttpStatusCode.InternalServerError, "Internal server error");
                }
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Unauthorized user");
            }
        }

        private async Task HandleGetUserByUsernameAsync(StreamReader request, StreamWriter response, string path)
        {
            string username = Uri.UnescapeDataString(path.Substring(7));

            var user = await AuthenticateFromToken(request, response);

            if (user != null && user.Username == username)
            {
                var sanitizedUser = new
                {
                    user.Id,
                    user.Username,
                    user.Name,
                    user.Bio,
                    user.Image,
                    ELO = user.Inventory?.ELO
                };
                string jsonResponse = SerializeToJson(sanitizedUser);
                SendResponse(response, jsonResponse, "application/json");
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.Unauthorized, "Unauthorized");
            }
        }

        private async Task HandleUpdateUserAsync(StreamReader request, StreamWriter response, string path)
        {
            var user = await AuthenticateFromToken(request, response);
            if (user != null)
            {
                string username = Uri.UnescapeDataString(path.Substring(7));
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
                                string name = nameElement.GetString();
                                if (!string.IsNullOrWhiteSpace(name) && name.Length <= 50)
                                {
                                    user.Name = name;
                                }
                            }

                            if (root.TryGetProperty("Bio", out JsonElement bioElement))
                            {
                                string bio = bioElement.GetString();
                                if (bio != null && bio.Length <= 500)
                                {
                                    user.Bio = bio;
                                }
                            }

                            if (root.TryGetProperty("Image", out JsonElement imageElement))
                            {
                                string image = imageElement.GetString();
                                if (image != null && image.Length <= 200)
                                {
                                    user.Image = image;
                                }
                            }
                        }

                        _userServiceHandler.UpdateUser(user.Id, user);
                        SendResponse(response, "User updated successfully", "application/json", HttpStatusCode.OK);
                    }
                    catch (JsonException)
                    {
                        SendErrorResponse(response, HttpStatusCode.BadRequest, "Invalid JSON format");
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
                string[] allowedMethods = { "GET", "POST", "PUT", "DELETE" };
                if (!allowedMethods.Contains(method.ToUpper()))
                {
                    response.WriteLine("HTTP/1.1 405 Method Not Allowed");
                    response.WriteLine("Content-Length: 0");
                    response.WriteLine();
                    response.Flush();
                    return;
                }

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
                SendErrorResponse(response, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }
    }
}