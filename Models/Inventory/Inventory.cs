using System.Text.Json.Serialization;

namespace SemesterProjekt1
{
    public class Inventory
    {
        public List<Card> OwnedCards { get; set; }
        public List<Card> JustOpened { get; set; }
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
            this.ELO = 100;
            this.UserID = UserID;
        }

        [JsonConstructor]
        public Inventory(List<Card> ownedCards, Deck deck, List<CardPack> cardPacks, int money, int userID, int ELO)
        {
            this.OwnedCards = ownedCards ?? new List<Card>();
            this.Deck = deck ?? new Deck();
            this.CardPacks = cardPacks ?? new List<CardPack>();
            this.Money = money;
            this.ELO = ELO;
            this.UserID = userID;
            
        }

        public void AddCardToOwnedCards(Card card)
        {
            if (!OwnedCards.Any(c => c.ID == card.ID))
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
            CardPacks.Add(new CardPack(this.UserID));
        }

        public void AddCardPack(CardPack cardPack, int amount)
        {
            if (amount <= 0) throw new ArgumentException("Amount must be greater than zero.");
            if (this.Money < 5 * amount) throw new InvalidOperationException("Not enough money to buy the card packs.");

            while (amount > 0 && this.Money >= 5 * amount)
            {
                CardPacks.Add(new CardPack(this.UserID));
                Console.WriteLine(this.Money);
                this.Money -= 5;
                Console.WriteLine(this.Money);
                amount--;
            }
        }

        public void OpenCardPack(CardPack cardPack)
        {
            JustOpened = new List<Card>();
            var newCards = cardPack.OpenCardPack(UserID);
            foreach (var card in newCards)
            {
                AddCardToOwnedCards(card);
                JustOpened.Add(card);
            }
        }

        public void JustOpenedClear()
        {
            JustOpened.Clear();
        }
    }
}