using System.Net;

namespace SemesterProjekt1
{
    public class UserServiceHandler
    {
        private List<User> _lobby;
        public List<User> _users;
        public List<Card> _cards;
        public DatabaseHandler2 _databaseHandler;

        public UserServiceHandler()
        {
            _databaseHandler = new DatabaseHandler2();
            _users = _databaseHandler.LoadUsers();
            _lobby = new List<User>();

            if (_users.Count == 0)
            {
                InitializeDefaultUsers();
                _databaseHandler.SaveUsers(_users);
            }
        }

        private void InitializeDefaultUsers()
        {
            _users = new List<User>
                {
                    new User(1, "Ender", "123"),
                    new User(2, "John", "test"),
                    new User(3, "Jane", "test"),
                };
        }

        private void SendResponse(HttpListenerResponse response, string content, string contentType)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            response.ContentType = contentType;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
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
            if (input.Length > 50) // Beispielgrenze, anpassen nach Bedarf
            {
                return false;
            }

            return true;
        }

        public List<User> GetAllUsers()
        {
            return _users;
        }

        public List<Card> GetAllCards()
        {
            List<Card> allCards = new List<Card>();

            foreach (var user in _users)
            {
                allCards.AddRange(user.Inventory.OwnedCards);
            }

            return allCards;
        }

        public User GetUserById(int id)
        {
            return _users.Find(p => p.Id == id);
        }

        public void AddUser(User user)
        {
            if (_users.Exists(u => u.Username == user.Username))
            {
                throw new InvalidOperationException("Ein Benutzer mit diesem Benutzernamen existiert bereits.");
            }

            if (!IsValidInput(user.Username) || !IsValidInput(user.Password))
            {
                throw new InvalidOperationException("Pffff Buffer Overload Detected.");
            }

            _users.Add(user);
            _databaseHandler.UpdateUser(user);
        }

        public User GetUserByName(string username)
        {
            return _users.Find(p => p.Username == username);
        }

        public void UpdateUser(int id, User updatedUser)
        {
            var user = GetUserById(id);
            if (!IsValidInput(user.Username) || !IsValidInput(user.Password))
            {
                throw new InvalidOperationException("Ein Benutzer mit diesem Benutzernamen existiert bereits.");
            }
            if (user != null)
            {
                user.Username = updatedUser.Username;
                user.Password = updatedUser.Password;
                _databaseHandler.UpdateUser(user);
                updatedUser = _databaseHandler.LoadUserById(user.Id);
                _users[_users.FindIndex(u => u.Id == user.Id)] = updatedUser;
            }
        }

        public void DeleteUser(int id)
        {
            var user = GetUserById(id);
            if (user != null)
            {
                _users.Remove(user);
                _databaseHandler.SaveUsers(_users);
            }
        }

        public User AuthenticateUser(string username, string password)
        {
            return _users.Find(u => u.Username == username && u.Password == password);
        }

        public User BuyPacks(int userId, int amount, string username, string password)
        {
            var user = GetUserById(userId);
            if (user != null && AuthenticateUser(username, password) != null)
            {
                try
                {
                    user.Inventory.AddCardPack(new CardPack(userId), amount);
                    _databaseHandler.UpdateUser(user);
                    var updatedUser = _databaseHandler.LoadUserById(user.Id);
                    _users[_users.FindIndex(u => u.Id == user.Id)] = updatedUser;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return null;
                }
            }
            return user;
        }

        public User OpenCardPack(int userId, string username, string password)
        {
            var user = _users.Find(p => p.Id == userId && p.Username == username && p.Password == password);
            if (user != null && AuthenticateUser(username, password) != null)
            {
                if (user.Inventory.CardPacks.Count > 0)
                {
                    var cardPack = user.Inventory.CardPacks[0];
                    user.Inventory.OpenCardPack(cardPack);
                    user.Inventory.CardPacks.RemoveAt(0);
                    _databaseHandler.UpdateUser(user);
                    var updatedUser = _databaseHandler.LoadUserById(user.Id);
                    _users[_users.FindIndex(u => u.Id == user.Id)] = updatedUser;
                }
            }
            return user;
        }

        public void SaveDeck(int userId, List<Card> deck)
        {
            var user = GetUserById(userId);
            if (user != null)
            {
                user.Inventory.Deck.Cards = deck;
                _databaseHandler.UpdateUser(user);
                var updatedUser = _databaseHandler.LoadUserById(user.Id);
                _users[_users.FindIndex(u => u.Id == user.Id)] = updatedUser;
            }
            else
            {
                throw new InvalidOperationException($"User with ID {userId} not found.");
            }
        }

        public User AddCardToDeck(int userId, string username, string password, int[] cardpos)
        {
            var user = _users.Find(p => p.Id == userId && p.Username == username && p.Password == password);
            if (user != null && AuthenticateUser(username, password) != null)
            {
                if (user.Inventory.OwnedCards.Count > 0)
                {
                    var cardsList = user.Inventory.OwnedCards;
                    foreach (var pos in cardpos)
                    {
                        user.Inventory.Deck.AddCard(cardsList[pos]);
                        user.Inventory.OwnedCards[pos].InDeck = true;
                    }
                    _databaseHandler.UpdateUser(user);
                    var updatedUser = _databaseHandler.LoadUserById(user.Id);
                    _users[_users.FindIndex(u => u.Id == user.Id)] = updatedUser;
                }
            }
            return user;
        }
    }
}