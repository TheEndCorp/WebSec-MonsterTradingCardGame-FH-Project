using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

using SemesterProjekt1;

[TestClass]
public class UserTests
{
    [TestMethod]
    public void Constructor_Testing()
    {
        int id = 1;
        string username = "testuser";
        string password = "password";

        User user = new User(id, username, password);

        Assert.AreEqual(id, user.Id);
        Assert.AreEqual(username, user.Username);
        Assert.AreEqual(password, user.Password);
        Assert.IsNotNull(user.Inventory);
        Assert.AreEqual(id, user.Inventory.UserID);
        Assert.AreEqual(string.Empty, user.Image);
        Assert.AreEqual(string.Empty, user.Bio);
        Assert.AreEqual(string.Empty, user.Name);
    }

    [TestMethod]
    public void JsonConstructor_Testing()
    {
        int id = 1;
        string username = "testuser";
        string password = "password";
        Inventory inventory = new Inventory(id);
        string bio = "test bio";
        string image = "test image";
        string name = "test name";

        User user = new User(id, username, password, inventory, bio, image, name);

        Assert.AreEqual(id, user.Id);
        Assert.AreEqual(username, user.Username);
        Assert.AreEqual(password, user.Password);
        Assert.AreEqual(inventory, user.Inventory);
        Assert.AreEqual(bio, user.Bio);
        Assert.AreEqual(image, user.Image);
        Assert.AreEqual(name, user.Name);
    }

    [TestMethod]
    public void GetNextAvailableId_ShouldSetNextId()
    {
        List<User> userList = new List<User>
            {
                new User(1, "user1", "password1"),
                new User(2, "user2", "password2")
            };
        User newUser = new User(0, "newuser", "newpassword");

        newUser.GetNextAvailableId(userList);

        Assert.AreEqual(3, newUser.Id);
        Assert.AreEqual(3, newUser.Inventory.UserID);
    }

    [TestMethod]
    public void Properties_ShouldGetAndSetValues()
    {
        User user = new User(1, "testuser", "password");
        string newUsername = "newusername";
        string newPassword = "newpassword";
        Inventory newInventory = new Inventory(2);
        string newBio = "new bio";
        string newImage = "new image";
        string newName = "new name";

        user.Username = newUsername;
        user.Password = newPassword;
        user.Inventory = newInventory;
        user.Bio = newBio;
        user.Image = newImage;
        user.Name = newName;

        Assert.AreEqual(newUsername, user.Username);
        Assert.AreEqual(newPassword, user.Password);
        Assert.AreEqual(newInventory, user.Inventory);
        Assert.AreEqual(newBio, user.Bio);
        Assert.AreEqual(newImage, user.Image);
        Assert.AreEqual(newName, user.Name);
    }
}

[TestClass]
public class CardTests
{
    [TestMethod]
    public void Constructor_Testing()
    {
        Guid id = Guid.NewGuid();
        string name = "testcard";
        int damage = 10;
        CardTypes.ElementType element = CardTypes.ElementType.Fire;
        CardTypes.CardType type = CardTypes.CardType.Monster;
        CardTypes.Rarity rarity = CardTypes.Rarity.Common;
        int userId = 0;

        Card card = new MonsterCard(id, name, damage, element, rarity, userId);

        Assert.AreEqual(id, card.ID);
        Assert.AreEqual(name, card.Name);
        Assert.AreEqual(damage, card.Damage);
        Assert.AreEqual(element, card.Element);
        Assert.AreEqual(type, card.Type);
        Assert.AreEqual(rarity, card.RarityType);
        Assert.AreEqual(userId, card.UserID);
    }

    [TestMethod]
    public void JsonConstructor_Testing()
    {
        Guid id = Guid.NewGuid();
        string name = "testcard";
        int damage = 10;
        CardTypes.ElementType element = CardTypes.ElementType.Fire;
        CardTypes.CardType type = CardTypes.CardType.Monster;
        CardTypes.Rarity rarity = CardTypes.Rarity.Common;
        bool inDeck = false;
        bool inTrade = false;
        int userId = 1;

        Card card = new MonsterCard(id, name, damage, element, rarity, inDeck, inTrade, userId);

        Assert.AreEqual(id, card.ID);
        Assert.AreEqual(name, card.Name);
        Assert.AreEqual(damage, card.Damage);
        Assert.AreEqual(element, card.Element);
        Assert.AreEqual(type, card.Type);
        Assert.AreEqual(rarity, card.RarityType);
        Assert.AreEqual(inDeck, card.InDeck);
        Assert.AreEqual(inTrade, card.InTrade);
        Assert.AreEqual(userId, card.UserID);
    }

    [TestMethod]
    public void IsMonster_ShouldReturnTrueForMonsterCard()
    {
        Card card = new MonsterCard(Guid.NewGuid(), "testcard", 10, CardTypes.ElementType.Fire, CardTypes.Rarity.Common, 1);

        bool result = card.IsMonster();

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ToString_ShouldReturnCorrectString()
    {
        Card card = new MonsterCard(Guid.NewGuid(), "testcard", 10, CardTypes.ElementType.Fire, CardTypes.Rarity.Common, 1);

        string result = card.ToString();

        Assert.IsTrue(result.Contains("testcard"));
    }
}

[TestClass]
public class InventoryTests
{
    [TestMethod]
    public void Constructor_Testing()
    {
        int userId = 1;

        Inventory inventory = new Inventory(userId);

        Assert.AreEqual(userId, inventory.UserID);
        Assert.AreEqual(20, inventory.Money);
        Assert.AreEqual(100, inventory.ELO);
        Assert.IsNotNull(inventory.OwnedCards);
        Assert.IsNotNull(inventory.Deck);
        Assert.IsNotNull(inventory.CardPacks);
    }

    [TestMethod]
    public void JsonConstructor_Testing()
    {
        List<Card> ownedCards = new List<Card>();
        Deck deck = new Deck();
        List<CardPack> cardPacks = new List<CardPack>();
        int money = 20;
        int userId = 1;
        int ELO = 100;

        Inventory inventory = new Inventory(ownedCards, deck, cardPacks, money, userId, ELO);

        Assert.AreEqual(userId, inventory.UserID);
        Assert.AreEqual(money, inventory.Money);
        Assert.AreEqual(ELO, inventory.ELO);
        Assert.AreEqual(ownedCards, inventory.OwnedCards);
        Assert.AreEqual(deck, inventory.Deck);
        Assert.AreEqual(cardPacks, inventory.CardPacks);
    }

    [TestMethod]
    public void AddCardToOwnedCards_ShouldAddCard()
    {
        Inventory inventory = new Inventory(1);
        Card card = new MonsterCard(Guid.NewGuid(), "testcard", 10, CardTypes.ElementType.Fire, CardTypes.Rarity.Common, 1);

        inventory.AddCardToOwnedCards(card);

        Assert.IsTrue(inventory.OwnedCards.Contains(card));
    }

    [TestMethod]
    public void AddCardPack_ShouldAddCardPack()
    {
        Inventory inventory = new Inventory(1);
        CardPack cardPack = new CardPack(1);

        inventory.CardPacks.Add(cardPack);

        Assert.IsTrue(inventory.CardPacks.Contains(cardPack));
    }

    [TestMethod]
    public void OpenCardPack_ShouldAddCardsToOwnedCards()
    {
        Inventory inventory = new Inventory(1);
        CardPack cardPack = new CardPack(1);
        cardPack.Cards = new List<Card>
            {
                new MonsterCard(Guid.NewGuid(), "testcard1", 10, CardTypes.ElementType.Fire, CardTypes.Rarity.Common, 1),
                new MonsterCard(Guid.NewGuid(), "testcard2", 10, CardTypes.ElementType.Fire, CardTypes.Rarity.Common, 1)
            };

        inventory.OpenCardPack(cardPack);

        Assert.AreEqual(2, inventory.OwnedCards.Count);
    }
}

[TestClass]
public class DeckTests
{
    [TestMethod]
    public void Constructor_Testing()
    {
        Deck deck = new Deck();

        Assert.IsNotNull(deck.Cards);
    }

    [TestMethod]
    public void JsonConstructor__Testing()
    {
        List<Card> cards = new List<Card>();

        Deck deck = new Deck(cards);

        Assert.AreEqual(cards, deck.Cards);
    }

    [TestMethod]
    public void AddCard_ShouldAddCardToDeck()
    {
        Deck deck = new Deck();
        Card card = new MonsterCard(Guid.NewGuid(), "testcard", 10, CardTypes.ElementType.Fire, CardTypes.Rarity.Common, 1);

        bool result = deck.AddCard(card);

        Assert.IsTrue(result);
        Assert.IsTrue(deck.Cards.Contains(card));
    }

    [TestMethod]
    public void RemoveCard_ShouldRemoveCardFromDeck()
    {
        Deck deck = new Deck();
        Card card = new MonsterCard(Guid.NewGuid(), "testcard", 10, CardTypes.ElementType.Fire, CardTypes.Rarity.Common, 1);
        deck.AddCard(card);

        deck.RemoveCard(card);

        Assert.IsFalse(deck.Cards.Contains(card));
    }
}

[TestClass]
public class CardPackTests
{
    [TestMethod]
    public void Constructor__Testing()
    {
        int userId = 1;

        CardPack cardPack = new CardPack(userId);

        Assert.AreEqual(userId, cardPack.UserID);
        Assert.AreEqual(5, cardPack.Price);
    }

    [TestMethod]
    public void JsonConstructor_Testing()
    {
        int userId = 1;
        CardTypes.Rarity rarity = CardTypes.Rarity.Common;

        CardPack cardPack = new CardPack(userId, rarity);

        Assert.AreEqual(userId, cardPack.UserID);
        Assert.AreEqual(rarity, cardPack.Rarity);
    }

    [TestMethod]
    public void OpenCardPack_ShouldGenerateCards()
    {
        CardPack cardPack = new CardPack(1);

        List<Card> cards = cardPack.OpenCardPack(1);

        Assert.IsNotNull(cards);
    }
}

[TestClass]
public class FightLogicTests
{
    [TestMethod]
    public void Constructor_Testing()
    {
        User user1 = new User(1, "user1", "password1");
        User user2 = new User(2, "user2", "password2");

        FightLogic fightLogic = new FightLogic(user1, user2);

        Assert.AreEqual(user1, fightLogic.User1);
        Assert.AreEqual(user2, fightLogic.User2);
        Assert.IsNotNull(fightLogic.battleLog);
    }

    [TestMethod]
    public void StartBattleAsync_ShouldReturnResult_WorkForSureee()
    {
        User user1 = new User(1, "user1", "password1");
        User user2 = new User(2, "user2", "password2");
        FightLogic fightLogic = new FightLogic(user1, user2);

        var result = fightLogic.StartBattleAsync().Result;

        Assert.IsNotNull(result);
        Assert.IsTrue(result.ContainsKey("winner"));
        Assert.IsTrue(result.ContainsKey("log"));
    }
}

[TestClass]
public class TradingLogicTests
{
    [TestMethod]
    public void Constructor_Testing()
    {
        Guid id = Guid.NewGuid();
        Guid cardToTrade = Guid.NewGuid();
        CardTypes.CardType type = CardTypes.CardType.Monster;
        int minimumDamage = 10;
        int userId = 1;

        TradingLogic tradingLogic = new TradingLogic(id, cardToTrade, (CardType)type, minimumDamage, userId);

        Assert.AreEqual(id, tradingLogic.Id);
        Assert.AreEqual(cardToTrade, tradingLogic.CardToTrade);
        Assert.AreEqual((CardType)type, tradingLogic.Type);
        Assert.AreEqual(minimumDamage, tradingLogic.MinimumDamage);
        Assert.AreEqual(userId, tradingLogic.UserId);
    }
}