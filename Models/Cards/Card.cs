using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemesterProjekt1
{
    public abstract class Card : CardTypes
    {
        public Guid ID { get; set; }
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
            ID = Guid.Empty;
            Name = string.Empty;
            Damage = 0;
            Element = ElementType.Normal;
            Type = CardType.Monster;
            RarityType = Rarity.Common;
            InDeck = false;
            InTrade = false;
            UserID = 0;
        }

        public Card(Guid ID, string name, int damage, ElementType element, CardType type, Rarity rarityType, int userID)
        {
            this.ID = ID;
            this.Name = name;
            this.Damage = damage;
            this.Element = element;
            this.Type = type;
            this.RarityType = rarityType;
            this.InDeck = false;
            this.InTrade = false;
        }

        public Card(Guid ID, string name, int damage, ElementType element, CardType type, Rarity rarityType)
        {
            this.ID = ID;
            this.Name = name;
            this.Damage = damage * (int)rarityType;
            this.Element = element;
            this.Type = type;
            this.RarityType = rarityType;
            this.InDeck = false;
            this.InTrade = false;
        }

        [JsonConstructor]
        public Card(Guid ID, string name, int damage, ElementType element, CardType type, Rarity rarityType, bool inDeck, bool inTrade, int userID)
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
        public MonsterCard(Guid ID, string name, int damage, ElementType element, Rarity rarityType, bool inDeck, bool inTrade, int userID)
            : base(ID, name, damage, element, CardType.Monster, rarityType, inDeck, inTrade, userID)
        {
        }

        public MonsterCard(Guid ID, string name, int damage, ElementType element, Rarity rarityType, int userID)
    : base(ID, name, damage, element, CardType.Monster, rarityType, userID)
        {
        }

        public MonsterCard(Guid ID, string name, int damage, ElementType element, Rarity rarityType)
: base(ID, name, damage, element, CardType.Monster, rarityType)
        {
        }
    }

    public class SpellCard : Card
    {
        [JsonConstructor]
        public SpellCard(Guid ID, string name, int damage, ElementType element, Rarity rarityType, bool inDeck, bool inTrade, int userID)
            : base(ID, name, damage, element, CardType.Spell, rarityType, inDeck, inTrade, userID)
        {
        }

        public SpellCard(Guid ID, string name, int damage, ElementType element, Rarity rarityType, int userID)
    : base(ID, name, damage, element, CardType.Spell, rarityType, userID)
        {
        }

        public SpellCard(Guid ID, string name, int damage, ElementType element, Rarity rarityType)
: base(ID, name, damage, element, CardType.Spell, rarityType)
        {
        }
    }

    public class CardCreator9000 : Card
    {
        public static Card CreateCard(CardData data)
        {
            string name = data.Name;
            ElementType Element;
            Rarity rarity = Rarity.Created;
            CardType Type;

            if (name.Contains("Fire"))
            {
                Element = ElementType.Fire;
                if (name.Contains("Spell"))
                {
                    Type = CardType.Spell;
                }
                else
                {
                    name = name.Replace("Fire", string.Empty).Trim();
                    Type = CardType.Monster;
                }
            }
            else if (name.Contains("Water"))
            {
                Element = ElementType.Water;
                if (name.Contains("Spell"))
                {
                    Type = CardType.Spell;
                }
                else
                {
                    if (name.Contains("FireElf")) Type = CardType.Monster;
                    else
                    {
                        name = name.Replace("Fire", string.Empty).Trim();
                        Type = CardType.Monster;
                    }
                }
            }
            else
            {
                Element = ElementType.Normal;
                if (name.Contains("Spell"))
                {
                    Type = CardType.Spell;
                }
                else
                {
                    name = name.Replace("Normal", string.Empty).Trim();
                    Type = CardType.Monster;
                }
            }
            Console.WriteLine(data.Id);
            if (Type == CardType.Monster) return new MonsterCard(data.Id, name, (int)data.Damage, Element, rarity);
            else return new SpellCard(data.Id, name, (int)data.Damage, Element, rarity);
        }
    }

    public class CardData
    {
        public class CardConverter : JsonConverter<Card>
        {
            public override Card Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
                {
                    JsonElement root = doc.RootElement;
                    CardType type = (CardType)root.GetProperty("Type").GetInt32();

                    if (type == CardType.Monster)
                    {
                        return JsonSerializer.Deserialize<MonsterCard>(root.GetRawText(), options);
                    }
                    else if (type == CardType.Spell)
                    {
                        return JsonSerializer.Deserialize<SpellCard>(root.GetRawText(), options);
                    }
                    else
                    {
                        throw new NotSupportedException($"Card type {type} is not supported.");
                    }
                }
            }

            public override void Write(Utf8JsonWriter writer, Card value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
            }
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public double Damage { get; set; }
    }
}