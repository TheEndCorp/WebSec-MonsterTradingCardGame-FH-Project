using System.Text.Json.Serialization;

namespace SemesterProjekt1
{
    public abstract class Card : CardTypes
    {
        public long ID { get; set; }
        public string Name { get; set; }
        public int Damage { get; set; }
        public ElementType Element { get; set; }
        public CardType Type { get; set; }
        public Rarity RarityType { get; set; }
        public bool InDeck { get; set; }
        public bool InTrade { get; set; }
        public int UserID { get; set; }

        public Card()
        {
            ID = 0;
            Name = string.Empty;
            Damage = 0;
            Element = ElementType.Normal;
            Type = CardType.Monster;
            RarityType = Rarity.Common;
            InDeck = false;
            InTrade = false;
            UserID = 0;
        }

        public Card(long ID, string name, int damage, ElementType element, CardType type, Rarity rarityType, int userID)
        {
            this.ID = ID;
            this.Name = name;
            this.Damage = damage * (int)rarityType;
            this.Element = element;
            this.Type = type;
            this.RarityType = rarityType;
            this.UserID = userID;
            this.InDeck = false;
            this.InTrade = false;
        }

        [JsonConstructor]
        public Card(long ID, string name, int damage, ElementType element, CardType type, Rarity rarityType, bool inDeck, bool inTrade, int userID)
        {
            this.ID = ID;
            this.Name = name;
            this.Damage = damage;
            this.Element = element;
            this.Type = type;
            this.RarityType = rarityType;
            this.UserID = userID;
            this.InTrade = inTrade;
            if (InTrade == true && inDeck == true) this.InDeck = false;
            //this.InDeck = inDeck;
        }

        public bool IsMonster()
        {
            return Type == CardType.Monster;
        }

        public override string ToString()
        {
            return $"Name: {Name}, Damage: {Damage}, Element: {Element}, Type: {Type}, UserID: {UserID}, InTrade:{InTrade}";
        }
    }

    public class MonsterCard : Card
    {
        [JsonConstructor]
        public MonsterCard(long ID, string name, int damage, ElementType element, Rarity rarityType, bool inDeck, bool inTrade, int userID)
            : base(ID, name, damage, element, CardType.Monster, rarityType, inDeck, inTrade, userID)
        {
        }

        public MonsterCard(long ID, string name, int damage, ElementType element, Rarity rarityType, int userID)
    : base(ID, name, damage, element, CardType.Monster, rarityType, userID)
        {
        }
    }

    public class SpellCard : Card
    {
        [JsonConstructor]
        public SpellCard(long ID, string name, int damage, ElementType element, Rarity rarityType, bool inDeck, bool inTrade, int userID)
            : base(ID, name, damage, element, CardType.Spell, rarityType, inDeck, inTrade, userID)
        {
        }

        public SpellCard(long ID, string name, int damage, ElementType element, Rarity rarityType, int userID)
    : base(ID, name, damage, element, CardType.Spell, rarityType, userID)
        {
        }
    }
}