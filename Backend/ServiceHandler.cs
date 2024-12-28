using System.Net;

namespace SemesterProjekt1
{
    public class UserServiceHandler
    {
        private List<User> _lobby;
        public List<User> _users;
        public List<Card> _cards;
        public DatabaseHandler2 _databaseHandler;
        public List<TradingLogic> _tradingDeals;

        public UserServiceHandler()
        {
            _databaseHandler = new DatabaseHandler2();
            _users = _databaseHandler.LoadUsers();
            _lobby = new List<User>();
            _tradingDeals = new List<TradingLogic>();

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
                    new User(0, "Ender", "123"),
                    new User(1, "John", "test"),
                    new User(2, "Jane", "test"),
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
            _databaseHandler.SaveUsers(_users);
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
                _users[_users.FindIndex(u => u.Id == user.Id)] = user;
            }
        }

        public void UpdateUserInventory(int id, User updatedUser)
        {
            var user = GetUserById(id);
            if (user != null)
            {
                _databaseHandler.UpdateUser(updatedUser);
                _users[_users.FindIndex(u => u.Id == user.Id)] = updatedUser;
            }
        }

        public void UpdateUserInventory(User updatedUser)
        {
            var user = GetUserById(updatedUser.Id);
            if (user != null)
            {
                _databaseHandler.UpdateUser(updatedUser);
                _users[_users.FindIndex(u => u.Id == updatedUser.Id)] = updatedUser;
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
                    _users[_users.FindIndex(u => u.Id == user.Id)] = user;
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
                    _users[_users.FindIndex(u => u.Id == user.Id)] = user;
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
                _users[_users.FindIndex(u => u.Id == user.Id)] = user;
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
                    _users[_users.FindIndex(u => u.Id == user.Id)] = user;
                }
            }
            return user;
        }

        public List<TradingLogic> GetAllTradingDeals()
        {
            return _tradingDeals;
        }

        public void AddTradingDeal(TradingLogic deal)
        {
            _tradingDeals.Add(deal);
        }

        public void DeleteTradingDeal(Guid id)
        {
            var deal = _tradingDeals.FirstOrDefault(d => d.Id == id);
            if (deal != null)
            {
                _tradingDeals.Remove(deal);
            }
        }

        public TradingLogic GetTradingDealById(Guid id)
        {
            return _tradingDeals.FirstOrDefault(d => d.Id == id);
        }

        public void ExecuteTrade(Guid dealId, long cardId, int userId)
        {
            var deal = GetTradingDealById(dealId);
            if (deal != null)
            {
                var user = GetUserById(userId);
                var card = user.Inventory.OwnedCards.FirstOrDefault(c => c.ID == cardId);

                if (card != null && card.Damage >= deal.MinimumDamage && card.Type == (CardTypes.CardType)deal.Type)
                {
                    var owner = GetUserById(deal.UserId);
                    var cardToTrade = owner.Inventory.OwnedCards.FirstOrDefault(c => c.ID == deal.CardToTrade);

                    if (cardToTrade != null)
                    {
                        owner.Inventory.OwnedCards.Remove(cardToTrade);
                        user.Inventory.OwnedCards.Remove(card);

                        owner.Inventory.OwnedCards.Add(card);
                        user.Inventory.OwnedCards.Add(cardToTrade);

                        DeleteTradingDeal(dealId);
                    }
                }
            }
        }
    }
}