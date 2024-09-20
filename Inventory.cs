using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;


namespace SemesterProjekt1
{

    public class Inventory
    {
        public List<Card> OwnedCards { get; set; }
        public Deck Deck { get; set; }
        public List<CardPack> CardPacks { get; set; }
        public int Money { get; set; }
        public int UserID { get; set; }
        public int ELO { get; set; }

        public Inventory(int UserID)
        {
            this.OwnedCards = new List<Card>();
            this.Deck = new Deck();
            this.CardPacks = new List<CardPack>();
            this.Money = 20;
            this.UserID = UserID;
        }

        [JsonConstructor]
        public Inventory(List<Card> ownedCards, Deck deck, List<CardPack> cardPacks, int money, int userID)
        {
            this.OwnedCards = ownedCards ?? new List<Card>();
            this.Deck = deck ?? new Deck();
            this.CardPacks = cardPacks ?? new List<CardPack>();
            this.Money = OwnedCards.Count == 0 ? 20 : money;
            this.UserID = userID;
        }

        [JsonConstructor]
        public Inventory(List<Card> ownedCards, Deck deck, List<CardPack> cardPacks, int money, int userID, int ELO)
        {
            this.OwnedCards = ownedCards ?? new List<Card>();
            this.Deck = deck ?? new Deck();
            this.CardPacks = cardPacks ?? new List<CardPack>();
            this.Money = OwnedCards.Count == 0 ? 20 : money;
            this.UserID = userID;
            this.ELO = ELO;
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

        public void AddCardPack(CardPack cardPack, int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            else if (amount > 1)
            {
                for (int i = 0; i < amount; i++)
                {
                    CardPacks.Add(cardPack);
                    Money -= 5;
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

    
}