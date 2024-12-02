using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemesterProjekt1
{
    public class DatabaseHandler
    {
        private readonly string _filePath = "persons.json";

        public List<User> LoadUser()
        {
            if (!File.Exists(_filePath))
            {
                return new List<User>();
            }

            string json = File.ReadAllText(_filePath);
            var options = new JsonSerializerOptions
            {
                Converters = { new UserConverter(), new CardConverter() },
                IncludeFields = true,
                PropertyNameCaseInsensitive = true
            };
            try
            {
                var users = JsonSerializer.Deserialize<List<User>>(json, options) ?? new List<User>();
                foreach (var user in users)
                {
                    foreach (var card in user.Inventory.OwnedCards)
                    {
                        if (card.InDeck)
                        {
                            user.Inventory.Deck.AddCard(card);
                        }
                    }
                }
                return users;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Failed to deserialize users.", ex);
            }
        }

        public void SaveUsers(List<User> users)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new CardConverter() }
            };

            string json = JsonSerializer.Serialize(users, options);
            File.WriteAllText(_filePath, json);
        }
    }

    public class UserConverter : JsonConverter<User>
    {
        public override User Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject token");

            int id = default;
            string username = string.Empty;
            string password = string.Empty;
            Inventory inventory = default;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new User(id, username, password, inventory);

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected PropertyName token");

                var propertyName = reader.GetString() ?? throw new JsonException("Property name is null");
                reader.Read();

                switch (propertyName)
                {
                    case nameof(User.Id):
                        id = reader.GetInt32();
                        break;

                    case nameof(User.Username):
                        username = reader.GetString() ?? string.Empty;
                        break;

                    case nameof(User.Password):
                        password = reader.GetString() ?? string.Empty;
                        break;

                    case nameof(User.Inventory):
                        inventory = JsonSerializer.Deserialize<Inventory>(ref reader, options) ?? new Inventory(id);
                        break;

                    default:
                        throw new JsonException($"Unknown property: {propertyName}");
                }
            }

            throw new JsonException("Incomplete JSON data");
        }

        public override void Write(Utf8JsonWriter writer, User value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber(nameof(User.Id), value.Id);
            writer.WriteString(nameof(User.Username), value.Username);
            writer.WriteString(nameof(User.Password), value.Password);
            writer.WritePropertyName(nameof(User.Inventory));
            JsonSerializer.Serialize(writer, value.Inventory, options);
            writer.WriteEndObject();
        }
    }

    public class CardConverter : JsonConverter<Card>
    {
        public override Card Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                JsonElement root = doc.RootElement;

                long id = root.GetProperty(nameof(Card.ID)).GetInt64();
                string name = root.GetProperty(nameof(Card.Name)).GetString();
                int damage = root.GetProperty(nameof(Card.Damage)).GetInt32();
                ElementType element = (ElementType)root.GetProperty(nameof(Card.Element)).GetInt32();
                CardType type = (CardType)root.GetProperty(nameof(Card.Type)).GetInt32();
                Rarity rarityType = (Rarity)root.GetProperty(nameof(Card.RarityType)).GetInt32();
                bool inDeck = root.GetProperty(nameof(Card.InDeck)).GetBoolean();
                int userID = root.GetProperty(nameof(Card.UserID)).GetInt32();

                if (type == CardType.Spell)
                {
                    return new SpellCard(id, name, damage, (CardTypes.ElementType)element, (CardTypes.Rarity)rarityType, inDeck, userID);
                }
                else
                {
                    return new MonsterCard(id, name, damage, (CardTypes.ElementType)element, (CardTypes.Rarity)rarityType, inDeck, userID);
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, Card value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber(nameof(Card.ID), value.ID);
            writer.WriteString(nameof(Card.Name), value.Name);
            writer.WriteNumber(nameof(Card.Damage), value.Damage);
            writer.WriteNumber(nameof(Card.Element), (int)value.Element);
            writer.WriteNumber(nameof(Card.Type), (int)value.Type);
            writer.WriteNumber(nameof(Card.RarityType), (int)value.RarityType);
            writer.WriteBoolean(nameof(Card.InDeck), value.InDeck);
            writer.WriteNumber(nameof(Card.UserID), value.UserID);
            writer.WriteEndObject();
        }
    }
}