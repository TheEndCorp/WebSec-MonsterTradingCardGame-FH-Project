using Microsoft.Data.Sqlite;
using Npgsql;

namespace SemesterProjekt1
{
    public class DatabaseHandler2
    {
        private readonly string _sqliteConnectionString = "Data Source=database.db;";
        private readonly string _postgresConnectionString = "Host=localhost;Port=10002;Username=postgres;Password=postgres;Database=postgres";
        private bool _usePostgres;

        public DatabaseHandler2()
        {
            _usePostgres = TryConnectToPostgres();

            if (_usePostgres)
            {
                using (var connection = new NpgsqlConnection(_postgresConnectionString))
                {
                    connection.Open();
                    InitializePostgresDatabase(connection);
                }
            }
            else
            {
                if (!File.Exists("database.db"))
                {
                    using (var connection = new SqliteConnection(_sqliteConnectionString))
                    {
                        connection.Open();
                    }
                }
                InitializeDatabase();
            }
        }

        private bool TryConnectToPostgres()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_postgresConnectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch
            {
                Console.WriteLine("Failed to connect to Postgres. Falling back to SQLite.");
                return false;
            }
        }

        private void InitializeDatabase()
        {
            if (_usePostgres)
            {
                using (var connection = new NpgsqlConnection(_postgresConnectionString))
                {
                    connection.Open();
                    InitializePostgresDatabase(connection);
                }
            }
            else
            {
                using (var connection = new SqliteConnection(_sqliteConnectionString))
                {
                    connection.Open();
                    InitializeSqliteDatabase(connection);
                }
            }
        }

        private void InitializePostgresDatabase(NpgsqlConnection connection)
        {
            string createUsersTable = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id SERIAL PRIMARY KEY,
                        Username TEXT NOT NULL,
                        Password TEXT NOT NULL,
                        Money INTEGER,
                        ELO INTEGER
                    );";
            using (var command = new NpgsqlCommand(createUsersTable, connection))
            {
                command.ExecuteNonQuery();
            }

            string createCardsTable = @"
                    CREATE TABLE IF NOT EXISTS Cards (
                        Id SERIAL PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Damage INTEGER NOT NULL,
                        Element INTEGER NOT NULL,
                        Type INTEGER NOT NULL,
                        RarityType INTEGER NOT NULL,
                        InDeck BOOLEAN NOT NULL,
                        UserID INTEGER NOT NULL,
                        FOREIGN KEY(UserID) REFERENCES Users(Id)
                    );";
            using (var command = new NpgsqlCommand(createCardsTable, connection))
            {
                command.ExecuteNonQuery();
            }

            string createCardPacksTable = @"
                    CREATE TABLE IF NOT EXISTS CardPacks (
                        Id SERIAL PRIMARY KEY,
                        UserID INTEGER NOT NULL,
                        Rarity INTEGER NOT NULL,
                        FOREIGN KEY(UserID) REFERENCES Users(Id)
                    );";
            using (var command = new NpgsqlCommand(createCardPacksTable, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private void InitializeSqliteDatabase(SqliteConnection connection)
        {
            string createUsersTable = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY,
                        Username TEXT NOT NULL,
                        Password TEXT NOT NULL,
                        Money INTEGER,
                        ELO INTEGER
                    );";
            using (var command = new SqliteCommand(createUsersTable, connection))
            {
                command.ExecuteNonQuery();
            }

            string createCardsTable = @"
                    CREATE TABLE IF NOT EXISTS Cards (
                        Id INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Damage INTEGER NOT NULL,
                        Element INTEGER NOT NULL,
                        Type INTEGER NOT NULL,
                        RarityType INTEGER NOT NULL,
                        InDeck BOOLEAN NOT NULL,
                        UserID INTEGER NOT NULL,
                        FOREIGN KEY(UserID) REFERENCES Users(Id)
                    );";
            using (var command = new SqliteCommand(createCardsTable, connection))
            {
                command.ExecuteNonQuery();
            }

            string createCardPacksTable = @"
                    CREATE TABLE IF NOT EXISTS CardPacks (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserID INTEGER NOT NULL,
                        Rarity INTEGER NOT NULL,
                        FOREIGN KEY(UserID) REFERENCES Users(Id)
                    );";
            using (var command = new SqliteCommand(createCardPacksTable, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private Inventory LoadInventory(int userId)
        {
            var inventory = new Inventory(userId);
            if (_usePostgres)
            {
                using (var connection = new NpgsqlConnection(_postgresConnectionString))
                {
                    connection.Open();
                    string selectInventory = "SELECT Money, ELO FROM Users WHERE Id = @UserID;";
                    using (var command = new NpgsqlCommand(selectInventory, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                inventory.Money = reader.GetInt32(0);
                                inventory.ELO = reader.GetInt32(1);
                            }
                        }
                    }

                    string selectCards = "SELECT * FROM Cards WHERE UserID = @UserID;";
                    using (var command = new NpgsqlCommand(selectCards, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                long id = reader.GetInt64(0);
                                string name = reader.GetString(1);
                                int damage = reader.GetInt32(2);
                                ElementType element = (ElementType)reader.GetInt32(3);
                                CardType type = (CardType)reader.GetInt32(4);
                                Rarity rarityType = (Rarity)reader.GetInt32(5);
                                bool inDeck = reader.GetBoolean(6);

                                Card card;
                                if (type == CardType.Spell)
                                {
                                    card = new SpellCard(id, name, damage, (CardTypes.ElementType)element, (CardTypes.Rarity)rarityType, inDeck, userId);
                                }
                                else
                                {
                                    card = new MonsterCard(id, name, damage, (CardTypes.ElementType)element, (CardTypes.Rarity)rarityType, inDeck, userId);
                                }

                                inventory.AddCardToOwnedCards(card);
                                if (inDeck)
                                {
                                    inventory.Deck.AddCard(card);
                                }
                            }
                        }
                    }

                    string selectCardPacks = "SELECT * FROM CardPacks WHERE UserID = @UserID;";
                    using (var command = new NpgsqlCommand(selectCardPacks, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Rarity rarity = (Rarity)reader.GetInt32(2);
                                inventory.AddCardPack(new CardPack(userId, (CardTypes.Rarity)rarity));
                            }
                        }
                    }
                }
            }
            else
            {
                using (var connection = new SqliteConnection(_sqliteConnectionString))
                {
                    connection.Open();
                    string selectInventory = "SELECT Money, ELO FROM Users WHERE Id = @UserID;";
                    using (var command = new SqliteCommand(selectInventory, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                inventory.Money = reader.GetInt32(0);
                                inventory.ELO = reader.GetInt32(1);
                            }
                        }
                    }

                    string selectCards = "SELECT * FROM Cards WHERE UserID = @UserID;";
                    using (var command = new SqliteCommand(selectCards, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                long id = reader.GetInt64(0);
                                string name = reader.GetString(1);
                                int damage = reader.GetInt32(2);
                                ElementType element = (ElementType)reader.GetInt32(3);
                                CardType type = (CardType)reader.GetInt32(4);
                                Rarity rarityType = (Rarity)reader.GetInt32(5);
                                bool inDeck = reader.GetBoolean(6);

                                Card card;
                                if (type == CardType.Spell)
                                {
                                    card = new SpellCard(id, name, damage, (CardTypes.ElementType)element, (CardTypes.Rarity)rarityType, inDeck, userId);
                                }
                                else
                                {
                                    card = new MonsterCard(id, name, damage, (CardTypes.ElementType)element, (CardTypes.Rarity)rarityType, inDeck, userId);
                                }

                                inventory.AddCardToOwnedCards(card);
                                if (inDeck)
                                {
                                    inventory.Deck.AddCard(card);
                                }
                            }
                        }
                    }

                    string selectCardPacks = "SELECT * FROM CardPacks WHERE UserID = @UserID;";
                    using (var command = new SqliteCommand(selectCardPacks, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Rarity rarity = (Rarity)reader.GetInt32(2);
                                inventory.AddCardPack(new CardPack(userId, (CardTypes.Rarity)rarity));
                            }
                        }
                    }
                }
            }
            return inventory;
        }

        private void SaveInventory(Inventory inventory, SqliteConnection connection, SqliteTransaction transaction)
        {
            foreach (var card in inventory.OwnedCards)
            {
                if (CardExists(card.ID, connection, transaction))
                {
                    string updateCard = @"
                    UPDATE Cards
                    SET Name = @Name, Damage = @Damage, Element = @Element, Type = @Type, RarityType = @RarityType, InDeck = @InDeck, UserID = @UserID
                    WHERE Id = @Id;";
                    using (var command = new SqliteCommand(updateCard, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Id", card.ID);
                        command.Parameters.AddWithValue("@Name", card.Name);
                        command.Parameters.AddWithValue("@Damage", card.Damage);
                        command.Parameters.AddWithValue("@Element", (int)card.Element);
                        command.Parameters.AddWithValue("@Type", (int)card.Type);
                        command.Parameters.AddWithValue("@RarityType", (int)card.RarityType);
                        command.Parameters.AddWithValue("@InDeck", card.InDeck);
                        command.Parameters.AddWithValue("@UserID", card.UserID);
                        command.ExecuteNonQuery();
                    }
                }
                else
                {
                    int n = 1;
                    while (CardExists(card.ID, connection, transaction))
                    {
                        card.ID += 1 + (n * n);
                        n++;
                    }

                    string insertCard = @"
                    INSERT INTO Cards (Id, Name, Damage, Element, Type, RarityType, InDeck, UserID)
                    VALUES (@Id, @Name, @Damage, @Element, @Type, @RarityType, @InDeck, @UserID);";
                    using (var command = new SqliteCommand(insertCard, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Id", card.ID);
                        command.Parameters.AddWithValue("@Name", card.Name);
                        command.Parameters.AddWithValue("@Damage", card.Damage);
                        command.Parameters.AddWithValue("@Element", (int)card.Element);
                        command.Parameters.AddWithValue("@Type", (int)card.Type);
                        command.Parameters.AddWithValue("@RarityType", (int)card.RarityType);
                        command.Parameters.AddWithValue("@InDeck", card.InDeck);
                        command.Parameters.AddWithValue("@UserID", card.UserID);
                        command.ExecuteNonQuery();
                    }
                }
            }

            // Delete existing CardPacks for the user
            string deleteCardPacks = "DELETE FROM CardPacks WHERE UserID = @UserID;";
            using (var command = new SqliteCommand(deleteCardPacks, connection, transaction))
            {
                command.Parameters.AddWithValue("@UserID", inventory.UserID);
                command.ExecuteNonQuery();
            }

            // Insert new CardPacks
            foreach (var cardPack in inventory.CardPacks)
            {
                string insertCardPack = @"
                INSERT INTO CardPacks (UserID, Rarity)
                VALUES (@UserID, @Rarity);";
                using (var command = new SqliteCommand(insertCardPack, connection, transaction))
                {
                    command.Parameters.AddWithValue("@UserID", cardPack.UserID);
                    command.Parameters.AddWithValue("@Rarity", (int)cardPack.Rarity);
                    command.ExecuteNonQuery();
                }
            }
        }

        private void SaveInventory(Inventory inventory, NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            foreach (var card in inventory.OwnedCards)
            {
                if (CardExists(card.ID, connection, transaction))
                {
                    string updateCard = @"
                    UPDATE Cards
                    SET Name = @Name, Damage = @Damage, Element = @Element, Type = @Type, RarityType = @RarityType, InDeck = @InDeck, UserID = @UserID
                    WHERE Id = @Id;";
                    using (var command = new NpgsqlCommand(updateCard, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Id", card.ID);
                        command.Parameters.AddWithValue("@Name", card.Name);
                        command.Parameters.AddWithValue("@Damage", card.Damage);
                        command.Parameters.AddWithValue("@Element", (int)card.Element);
                        command.Parameters.AddWithValue("@Type", (int)card.Type);
                        command.Parameters.AddWithValue("@RarityType", (int)card.RarityType);
                        command.Parameters.AddWithValue("@InDeck", card.InDeck);
                        command.Parameters.AddWithValue("@UserID", card.UserID);
                        command.ExecuteNonQuery();
                    }
                }
                else
                {
                    int n = 1;
                    while (CardExists(card.ID, connection, transaction))
                    {
                        card.ID += 1 + (n * n);
                        n++;
                    }

                    string insertCard = @"
                    INSERT INTO Cards (Name, Damage, Element, Type, RarityType, InDeck, UserID)
                    VALUES (@Name, @Damage, @Element, @Type, @RarityType, @InDeck, @UserID)
                    RETURNING Id;";
                    using (var command = new NpgsqlCommand(insertCard, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name", card.Name);
                        command.Parameters.AddWithValue("@Damage", card.Damage);
                        command.Parameters.AddWithValue("@Element", (int)card.Element);
                        command.Parameters.AddWithValue("@Type", (int)card.Type);
                        command.Parameters.AddWithValue("@RarityType", (int)card.RarityType);
                        command.Parameters.AddWithValue("@InDeck", card.InDeck);
                        command.Parameters.AddWithValue("@UserID", card.UserID);
                        card.ID = (long)command.ExecuteScalar();
                    }
                }
            }

            // Delete existing CardPacks for the user
            string deleteCardPacks = "DELETE FROM CardPacks WHERE UserID = @UserID;";
            using (var command = new NpgsqlCommand(deleteCardPacks, connection, transaction))
            {
                command.Parameters.AddWithValue("@UserID", inventory.UserID);
                command.ExecuteNonQuery();
            }

            // Insert new CardPacks
            foreach (var cardPack in inventory.CardPacks)
            {
                string insertCardPack = @"
                INSERT INTO CardPacks (UserID, Rarity)
                VALUES (@UserID, @Rarity);";
                using (var command = new NpgsqlCommand(insertCardPack, connection, transaction))
                {
                    command.Parameters.AddWithValue("@UserID", cardPack.UserID);
                    command.Parameters.AddWithValue("@Rarity", (int)cardPack.Rarity);
                    command.ExecuteNonQuery();
                }
            }
        }

        private bool CardExists(long id, SqliteConnection connection, SqliteTransaction transaction)
        {
            string checkCardExists = "SELECT COUNT(1) FROM Cards WHERE Id = @Id;";
            using (var command = new SqliteCommand(checkCardExists, connection, transaction))
            {
                command.Parameters.AddWithValue("@Id", id);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private bool CardExists(long id, NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            string checkCardExists = "SELECT COUNT(1) FROM Cards WHERE Id = @Id;";
            using (var command = new NpgsqlCommand(checkCardExists, connection, transaction))
            {
                command.Parameters.AddWithValue("@Id", id);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        public List<User> LoadUsers()
        {
            var users = new List<User>();
            if (_usePostgres)
            {
                using (var connection = new NpgsqlConnection(_postgresConnectionString))
                {
                    connection.Open();
                    string selectUsers = "SELECT * FROM Users;";
                    using (var command = new NpgsqlCommand(selectUsers, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string username = reader.GetString(1);
                            string password = reader.GetString(2);

                            var inventory = LoadInventory(id);
                            users.Add(new User(id, username, password, inventory));
                        }
                    }
                }
            }
            else
            {
                using (var connection = new SqliteConnection(_sqliteConnectionString))
                {
                    connection.Open();
                    string selectUsers = "SELECT * FROM Users;";
                    using (var command = new SqliteCommand(selectUsers, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string username = reader.GetString(1);
                            string password = reader.GetString(2);

                            var inventory = LoadInventory(id);
                            users.Add(new User(id, username, password, inventory));
                        }
                    }
                }
            }
            return users;
        }

        public void SaveUsers(List<User> users)
        {
            if (_usePostgres)
            {
                using (var connection = new NpgsqlConnection(_postgresConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        foreach (var user in users)
                        {
                            string insertOrUpdateUser = @"
                                    INSERT INTO Users (Id, Username, Password, Money, Elo)
                                    VALUES (@Id, @Username, @Password, @Money, @Elo)
                                    ON CONFLICT (Id) DO UPDATE
                                    SET Username = EXCLUDED.Username,
                                        Password = EXCLUDED.Password,
                                        Money = EXCLUDED.Money,
                                        Elo = EXCLUDED.Elo;";
                            using (var command = new NpgsqlCommand(insertOrUpdateUser, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@Id", user.Id);
                                command.Parameters.AddWithValue("@Username", user.Username);
                                command.Parameters.AddWithValue("@Password", user.Password);
                                command.Parameters.AddWithValue("@Money", user.Inventory.Money);
                                command.Parameters.AddWithValue("@Elo", user.Inventory.ELO);
                                command.ExecuteNonQuery();
                            }

                            SaveInventory(user.Inventory, connection, transaction);
                        }
                        transaction.Commit();
                    }
                }
            }
            else
            {
                using (var connection = new SqliteConnection(_sqliteConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        foreach (var user in users)
                        {
                            string insertOrUpdateUser = @"
                                    INSERT OR REPLACE INTO Users (Id, Username, Password, Money, Elo)
                                    VALUES (@Id, @Username, @Password, @Money, @Elo);";
                            using (var command = new SqliteCommand(insertOrUpdateUser, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@Id", user.Id);
                                command.Parameters.AddWithValue("@Username", user.Username);
                                command.Parameters.AddWithValue("@Password", user.Password);
                                command.Parameters.AddWithValue("@Money", user.Inventory.Money);
                                command.Parameters.AddWithValue("@Elo", user.Inventory.ELO);
                                command.ExecuteNonQuery();
                            }

                            SaveInventory(user.Inventory, connection, transaction);
                        }
                        transaction.Commit();
                    }
                }
            }
        }

        public void UpdateUser(User user)
        {
            if (_usePostgres)
            {
                using (var connection = new NpgsqlConnection(_postgresConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        string insertOrUpdateUser = @"
                                INSERT INTO Users (Id, Username, Password, Money, Elo)
                                VALUES (@Id, @Username, @Password, @Money, @Elo)
                                ON CONFLICT (Id) DO UPDATE
                                SET Username = EXCLUDED.Username,
                                    Password = EXCLUDED.Password,
                                    Money = EXCLUDED.Money,
                                    Elo = EXCLUDED.Elo;";
                        using (var command = new NpgsqlCommand(insertOrUpdateUser, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@Id", user.Id);
                            command.Parameters.AddWithValue("@Username", user.Username);
                            command.Parameters.AddWithValue("@Password", user.Password);
                            command.Parameters.AddWithValue("@Money", user.Inventory.Money);
                            command.Parameters.AddWithValue("@Elo", user.Inventory.ELO);
                            command.ExecuteNonQuery();
                        }

                        SaveInventory(user.Inventory, connection, transaction);
                        transaction.Commit();
                    }
                }
            }
            else
            {
                using (var connection = new SqliteConnection(_sqliteConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        string insertOrUpdateUser = @"
                                INSERT OR REPLACE INTO Users (Id, Username, Password, Money, Elo)
                                VALUES (@Id, @Username, @Password, @Money, @Elo);";
                        using (var command = new SqliteCommand(insertOrUpdateUser, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@Id", user.Id);
                            command.Parameters.AddWithValue("@Username", user.Username);
                            command.Parameters.AddWithValue("@Password", user.Password);
                            command.Parameters.AddWithValue("@Money", user.Inventory.Money);
                            command.Parameters.AddWithValue("@Elo", user.Inventory.ELO);
                            command.ExecuteNonQuery();
                        }

                        SaveInventory(user.Inventory, connection, transaction);
                        transaction.Commit();
                    }
                }
            }
        }

        public User LoadUserById(int userId)
        {
            User user = null;
            if (_usePostgres)
            {
                using (var connection = new NpgsqlConnection(_postgresConnectionString))
                {
                    connection.Open();
                    string selectUser = "SELECT * FROM Users WHERE Id = @Id;";
                    using (var command = new NpgsqlCommand(selectUser, connection))
                    {
                        command.Parameters.AddWithValue("@Id", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string username = reader.GetString(1);
                                string password = reader.GetString(2);
                                var inventory = LoadInventory(userId);
                                user = new User(userId, username, password, inventory);
                            }
                        }
                    }
                }
            }
            else
            {
                using (var connection = new SqliteConnection(_sqliteConnectionString))
                {
                    connection.Open();
                    string selectUser = "SELECT * FROM Users WHERE Id = @Id;";
                    using (var command = new SqliteCommand(selectUser, connection))
                    {
                        command.Parameters.AddWithValue("@Id", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string username = reader.GetString(1);
                                string password = reader.GetString(2);
                                var inventory = LoadInventory(userId);
                                user = new User(userId, username, password, inventory);
                            }
                        }
                    }
                }
            }
            return user;
        }
    }
}