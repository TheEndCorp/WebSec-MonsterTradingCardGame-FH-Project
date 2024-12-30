using System.Text.Json.Serialization;

namespace SemesterProjekt1
{
    public class CardPack : CardTypes
    {
        private static readonly ThreadLocal<Random> _random = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        public new Rarity Rarity { get; set; }
        public int UserID { get; set; }
        public int Price { get; }
        public List<Card> Cards { get; set; }

        public CardPack(int userID)
        {
            UserID = userID;
            Rarity = GenerateRandomRarity();
            Price = 5;
        }

        public CardPack(List<Card> Cards)
        {
            this.Rarity = 0;
            this.Price = 5;
            this.Cards = Cards;
        }

        [JsonConstructor]
        public CardPack(int userID, Rarity rarity)
        {
            UserID = userID;
            Rarity = rarity;
            Price = 5;
        }

        private Rarity GenerateRandomRarity()
        {
            var rarities = Enum.GetValues(typeof(Rarity));
            var randomIndex = _random?.Value?.Next(rarities.Length) ?? 0;
            return (Rarity)(rarities.GetValue(randomIndex) ?? Rarity.Common);
        }

        private List<Card> GenerateCards(int numberOfCards, int userId)
        {
            var cards = new List<Card>();
            for (int i = 0; i < numberOfCards; i++)
            {
                cards.Add(GenerateRandomCard(userId));
            }
            return cards;
        }

        private Card GenerateRandomCard(int userID)
        {
            Guid id = Guid.NewGuid();
            var element = (ElementType)(_random?.Value?.Next(0, Enum.GetValues(typeof(ElementType)).Length) ?? 0);
            var type = (CardType)(_random?.Value?.Next(0, Enum.GetValues(typeof(CardType)).Length) ?? 0);
            var rarity = (Rarity)(_random?.Value?.Next(1, Enum.GetValues(typeof(Rarity)).Length) ?? 1);
            int damage = _random?.Value?.Next(1, 10) ?? 1;

            if (type == CardType.Monster)
            {
                string name = MonsterNames[_random?.Value?.Next(0, MonsterNames.Length) ?? 0];
                if (name == "FireElf")
                {
                    element = ElementType.Fire;
                }

                return new MonsterCard(id, name, damage, element, rarity, userID);
            }
            else
            {
                string name = SpellNames[_random?.Value?.Next(0, SpellNames.Length) ?? 0];
                return new SpellCard(id, name, damage, element, rarity, userID);
            }
        }

        public List<Card> OpenCardPack(int userId)
        {
            if (this.Cards == null)
            {
                var cards = GenerateCards(5 * ((int)Rarity / 2), userId);
                return cards;
            }
            else
            {
                foreach (var card in this.Cards)
                {
                    card.UserID = userId;
                }
                return this.Cards;
            }
        }
    }
}