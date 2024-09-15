using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            Converters = { new UserConverter() },
            IncludeFields = true
        };
        return JsonSerializer.Deserialize<List<User>>(json, options) ?? new List<User>();
    }

    public void SaveUsers(List<User> users)
    {
        string json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}

public class UserConverter : JsonConverter<User>
{
    public override User Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        int id = default;
        string name = string.Empty;
        string password = string.Empty;
        Inventory inventory = default;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return new User(id, name, password, inventory);

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            string propertyName = reader.GetString() ?? throw new JsonException();
            reader.Read();

            switch (propertyName)
            {
                case nameof(User.Id):
                    id = reader.GetInt32();
                    break;
                case nameof(User.Name):
                    name = reader.GetString() ?? string.Empty;
                    break;
                case nameof(User.Password):
                    password = reader.GetString() ?? string.Empty;
                    break;
                case nameof(User.Inventory):
                    inventory = JsonSerializer.Deserialize<Inventory>(ref reader, options) ?? new Inventory(id);
                    break;
                default:
                    throw new JsonException();
            }
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, User value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(User.Id), value.Id);
        writer.WriteString(nameof(User.Name), value.Name);
        writer.WriteString(nameof(User.Password), value.Password);
        writer.WritePropertyName(nameof(User.Inventory));
        JsonSerializer.Serialize(writer, value.Inventory, options);
        writer.WriteEndObject();
    }
}
