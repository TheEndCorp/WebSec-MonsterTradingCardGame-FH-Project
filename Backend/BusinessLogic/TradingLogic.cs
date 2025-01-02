using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemesterProjekt1
{
    public class TradingLogic
    {
        public Guid Id { get; set; }
        public Guid CardToTrade { get; set; }
        public CardType Type { get; set; }
        public int MinimumDamage { get; set; }
        public int UserId { get; set; }

        public TradingLogic(Guid id, Guid cardToTrade, CardType type, int minimumDamage, int userId)
        {
            Id = id;
            CardToTrade = cardToTrade;
            Type = type;
            MinimumDamage = minimumDamage;
            UserId = userId;
        }
    }

    public class TradingLogicJsonConverter : JsonConverter<TradingLogic>
    {
        public override TradingLogic Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Guid id = Guid.Empty;
            Guid cardToTrade = Guid.Empty;
            CardType type = CardType.Monster;
            int minimumDamage = 0;
            int userId = 0;

            try
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return new TradingLogic(id, cardToTrade, type, minimumDamage, userId);
                    }

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string propertyName = reader.GetString();
                        reader.Read();

                        switch (propertyName)
                        {
                            case "Id":
                                id = reader.GetGuid();
                                break;

                            case "CardToTrade":

                                var cardtoTradeSting = reader.GetString();
                                if (!Guid.TryParse(cardtoTradeSting, out cardToTrade))
                                {
                                    throw new JsonException($"Invalid value for CardToTrade: {cardtoTradeSting}");
                                }

                                break;

                            case "Type":

                                var TypeString = reader.GetString();
                                if (TypeString.Equals("monster", StringComparison.OrdinalIgnoreCase)) type = CardType.Monster;
                                else if (TypeString.Equals("magic", StringComparison.OrdinalIgnoreCase)) type = CardType.Spell;
                                else if (TypeString.Equals("spell", StringComparison.OrdinalIgnoreCase)) type = CardType.Spell;
                                else throw new JsonException($"Unexpected property: {propertyName}");

                                break;

                            case "MinimumDamage":
                                minimumDamage = reader.GetInt32();
                                break;

                            case "UserId":
                                userId = reader.GetInt32();
                                break;

                            default:
                                throw new JsonException($"Unexpected property: {propertyName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing TradingLogic: {ex.Message}");
                throw;
            }

            throw new JsonException("Invalid JSON format for TradingLogic");
        }

        public override void Write(Utf8JsonWriter writer, TradingLogic value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("Id", value.Id);
            writer.WriteString("CardToTrade", value.CardToTrade.ToString());
            writer.WriteNumber("Type", (int)value.Type);
            writer.WriteNumber("MinimumDamage", value.MinimumDamage);
            writer.WriteNumber("UserId", value.UserId);
            writer.WriteEndObject();
        }
    }
}