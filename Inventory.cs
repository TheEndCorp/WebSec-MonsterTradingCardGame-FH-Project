using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public class Inventory
{
    public List<Card> OwnedCards { get;  set; }
    public Deck Deck { get;  set; }
    public List<CardPack> CardPacks { get;  set; }
    public int Money { get;  set; }
    public int UserID { get; set; }


    public Inventory(int UserID)
    {
        OwnedCards = new List<Card>();
        Deck = new Deck();
        CardPacks = new List<CardPack>();
        Money = 20;
        this.UserID = UserID;
    }

    [JsonConstructor]
    public Inventory(List<Card> ownedCards, Deck deck, List<CardPack> cardPacks, int money, int userID)
    {
        OwnedCards = ownedCards ?? new List<Card>();
        Deck = deck ?? new Deck();
        CardPacks = cardPacks ?? new List<CardPack>();
        Money = money;
        UserID = userID;
    }



    public void AddCardToOwnedCards(Card card)
    {
        if (!OwnedCards.Any(c => c.Name == card.Name))
        {
            OwnedCards.Add(card);
        }
        else
        {
            Console.WriteLine("Card already owned.");
        }
    }

    public void AddCardPack(CardPack cardPack)
    {
        CardPacks.Add(cardPack);
    }

    public void AddCardPack(CardPack cardPack,int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        else if(amount > 1)
        {
            for (int i = 0; i < amount; i++)
            {
                CardPacks.Add(cardPack);
            }
        }
        while (amount >= 1 && Money >= 5)
        {
            CardPacks.Add(cardPack);
            Money -= 5;
            amount--;
        }
    }



    public void OpenCardPack(CardPack cardPack)
    {
        var newCards = cardPack.OpenCardPack(UserID);
        foreach (var card in newCards)
        {
            AddCardToOwnedCards(card);
        }
    }
}

public class Deck
{
    private const int MaxCards = 20;
    public List<Card> Cards { get; set; }

    public Deck()
    {
        Cards = new List<Card>();
    }

    public bool AddCard(Card card)
    {
        if (Cards.Count >= MaxCards)
        {
            Console.WriteLine("Deck is full.");
            return false;
        }

        if (Cards.Any(c => c.Name == card.Name))
        {
            Console.WriteLine("Card already in deck.");
            return false;
        }

        Cards.Add(card);
        return true;
    }

    public void RemoveCard(Card card)
    {
        Cards.Remove(card);
    }
}
