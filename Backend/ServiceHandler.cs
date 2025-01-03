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
        public List<CardPack> _admingeneratedcardpacks;

        public UserServiceHandler()
        {
            _databaseHandler = new DatabaseHandler2();
            _users = _databaseHandler.LoadUsers();
            _lobby = new List<User>();
            _tradingDeals = _databaseHandler.LoadTrades();
            _admingeneratedcardpacks = new List<CardPack>();

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
            string[] blackList = { "'", "\"", "--", ";", "/*", "*/", "xp_" };
            foreach (var item in blackList)
            {
                if (input.Contains(item))
                {
                    return false;
                }
            }

            if (input.Length > 50)
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
            user.GetNextAvailableId(_users);
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
                throw new InvalidOperationException("Not Valid Input.");
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
                _users[_users.FindIndex(u => u.Id == updatedUser.Id)] = updatedUser;
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
                    if (_admingeneratedcardpacks != null && _admingeneratedcardpacks.Count > 0)
                    {
                        int packsToTake = Math.Min(amount, _admingeneratedcardpacks.Count);
                        for (int i = 0; i < packsToTake; i++)
                        {
                            user.Inventory.AddCardPack(_admingeneratedcardpacks[i], 1);
                        }
                        amount -= packsToTake;
                        _admingeneratedcardpacks.RemoveRange(0, packsToTake);
                        OpenCardPack(userId, username, password);
                    }
                    else if (amount > 0)
                    {
                        user.Inventory.AddCardPack(amount);
                    }

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

        public List<Card> OpenCardPack(int userId, string username, string password)
        {
            var user = _users.Find(p => p.Id == userId && p.Username == username && p.Password == password);
            if (user != null && AuthenticateUser(username, password) != null)
            {
                if (user.Inventory.CardPacks.Count > 0)
                {
                    user.Inventory.OpenCardPack(user.Inventory.CardPacks[0]);
                    user.Inventory.CardPacks.Remove(user.Inventory.CardPacks[0]);
                    List<Card> Liste = new List<Card>(user.Inventory.JustOpened);
                    user.Inventory.JustOpenedClear();

                    _databaseHandler.UpdateUser(user);
                    _users[_users.FindIndex(u => u.Id == user.Id)] = user;

                    return Liste;
                }
            }
            return null;
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
                        if (cardsList[pos].InTrade == true)
                        {
                            throw new InvalidOperationException("Card is in trade.");
                        }
                        else
                        {
                            user.Inventory.Deck.AddCard(cardsList[pos]);
                            user.Inventory.OwnedCards[pos].InDeck = true;
                        }
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

        public void AddTradingDeal(TradingLogic deal, User user)
        {
            var cardToTrade = user.Inventory.OwnedCards.FirstOrDefault(c => c.ID == deal.CardToTrade);
            if (cardToTrade != null)
            {
                if (cardToTrade.InTrade)
                {
                    throw new InvalidOperationException("Card is already in trade.");
                }

                _tradingDeals.Add(deal);
                user.Inventory.OwnedCards.First(c => c.ID == cardToTrade.ID).InTrade = true;

                user.Inventory.Deck.Cards.Remove(cardToTrade);
                user.Inventory.OwnedCards.First(c => c.ID == cardToTrade.ID).InDeck = false;

                if (user.Inventory.Deck.Cards.Count < 4)
                {
                    foreach (var card in user.Inventory.Deck.Cards)
                    {
                        card.InDeck = false;
                    }
                    user.Inventory.Deck.Cards.Clear();
                }

                _databaseHandler.UpdateUser(user);
                _databaseHandler.SaveTrade(_tradingDeals);
            }
            else
            {
                throw new InvalidOperationException("User does not own the card they want to trade.");
            }
        }

        public void DeleteTradingDeal(Guid id)
        {
            var deal = _tradingDeals.FirstOrDefault(d => d.Id == id);
            if (deal != null)
            {
                _tradingDeals.Remove(deal);
                var user = _users.FirstOrDefault(u => u.Id == deal.UserId);
                if (user != null)
                {
                    var card = user.Inventory.OwnedCards.FirstOrDefault(c => c.ID == deal.CardToTrade);
                    if (card != null)
                    {
                        card.InTrade = false;
                    }
                }
                try { _databaseHandler.SaveTrade(_tradingDeals); }
                catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); throw; }
                if (user != null)
                {
                    _databaseHandler.UpdateUser(user);
                }
            }
        }

        public TradingLogic GetTradingDealById(Guid id)
        {
            return _tradingDeals.FirstOrDefault(d => d.Id == id);
        }

        public void ExecuteTrade(Guid dealId, Guid cardId, int userId)
        {
            var deal = GetTradingDealById(dealId);
            if (deal != null)
            {
                if (deal.UserId == userId)
                {
                    throw new InvalidOperationException("You can't trade with yourself.");
                }

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

                        owner.Inventory.OwnedCards.First(c => c.ID == card.ID).InTrade = false;
                        user.Inventory.OwnedCards.First(c => c.ID == cardToTrade.ID).InTrade = false;

                        owner.Inventory.OwnedCards.First(c => c.ID == card.ID).UserID = owner.Id;
                        user.Inventory.OwnedCards.First(c => c.ID == cardToTrade.ID).UserID = user.Id;

                        DeleteTradingDeal(dealId);
                        _databaseHandler.UpdateUser(owner);
                        _databaseHandler.UpdateUser(user);
                        _databaseHandler.SaveTrade(_tradingDeals);
                    }
                }
            }
        }

        public void CreatePack(List<Card> cards)
        {
            this._admingeneratedcardpacks.Add(new CardPack(cards));
            Console.WriteLine(_admingeneratedcardpacks.Count);
        }
    }
}