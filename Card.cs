using System;


public abstract class  CardTypes { 

public enum ElementType
{
    Water,
    Fire,
    Normal
}

public enum CardType
{
    Monster,
    Spell
}

public enum Rarity
{
    Common = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4,
    God = 5
}

 public static string[] MonsterNames = { "Goblin", "Orc", "Dragon", "Knight", "Elf", "Dwarf", "Giant", "Kraken", "Wizard", "FireElf" };
 public static string[] SpellNames = { "Fireball", "Heal", "Lightning", "WaterBall" };


}



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

public class CardPack : CardTypes
{
    private Rarity _Rarity;
    private int _UserID;
    private int _Price = 5;



    private Rarity GenerateRandomRarity()
    {
        var rarities = Enum.GetValues(typeof(Rarity));
        return (Rarity)(rarities.GetValue(random.Next(rarities.Length)) ?? Rarity.Common);
    }


    public CardPack(int userID)
    {
        _UserID = userID;
        _Rarity = GenerateRandomRarity();
    }
    

public List<Card> OpenCardPack(int Userid)
    {
        var cards = GenerateCards(5 * ((int)_Rarity/2),Userid);
        return cards;

    }


    private List<Card> GenerateCards(int numberOfCards,int Userid)
    {
        var cards = new List<Card>();
        for (int i = 0; i < numberOfCards; i++)
        {
            cards.Add(GenerateRandomCard(Userid));
        }
        return cards;
    }

    private static Random random = new Random();

    private Card GenerateRandomCard(int userID)
    {



        
        var element = (ElementType)random.Next(0, Enum.GetValues(typeof(ElementType)).Length);
        var type = (CardType)random.Next(0, Enum.GetValues(typeof(CardType)).Length);
        var rarity = (Rarity)random.Next(1, Enum.GetValues(typeof(Rarity)).Length);
        int damage = random.Next(1, 10);

        if (type == CardType.Monster)
        {
            string name = MonsterNames[random.Next(0, MonsterNames.Length)];
            if (name == "FireElf")
            {
                element = ElementType.Fire;
            }

            return new MonsterCard(name, damage, element, rarity, userID);
        }
        else
        {
            string name = SpellNames[random.Next(0, SpellNames.Length)];

            return new SpellCard(name, damage, element, rarity, userID);
        }
    }
}



