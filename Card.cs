using System;
namespace SemesterProjekt1
{

    public abstract class Card : CardTypes
    {
        public string Name { get; }
        public int Damage { get; }
        public ElementType Element { get; }
        public CardType Type { get; }
        public Rarity RarityType { get; }
        public bool InDeck { get; set; }
        public int UserID { get; private set; }



        protected Card(string name, int damage, ElementType element, CardType type, Rarity rarityType, int userID)
        {
            Name = name;
            Damage = damage * (int)rarityType;
            Element = element;
            Type = type;
            RarityType = rarityType;
            UserID = userID;
        }

        public bool IsMonster()
        {
            return Type == CardType.Monster;
        }

        public override string ToString()
        {
            return $"Name: {Name}, Damage: {Damage}, Element: {Element}, Type: {Type}, UserID: {UserID}";
        }
    }

    public class MonsterCard : Card
    {
        public MonsterCard(string name, int damage, ElementType element, Rarity rarityType, int userID)
            : base(name, damage, element, CardType.Monster, rarityType, userID)
        {
        }
    }

    public class SpellCard : Card
    {
        public SpellCard(string name, int damage, ElementType element, Rarity rarityType, int userID)
            : base(name, damage, element, CardType.Spell, rarityType, userID)
        {
        }
    }

    

}