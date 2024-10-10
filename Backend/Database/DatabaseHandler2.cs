
using System;
using System.Collections.Generic;
using SQLitePCL;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using static SemesterProjekt1.CardTypes;

namespace SemesterProjekt1
{
    public class DatabaseHandler2
    {
        private readonly string _connectionString = "Data Source=database.db;";

        public DatabaseHandler2()
        {
            if (!File.Exists("database.db"))
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                }
                InitializeDatabase();
            }
        }

        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                string createUsersTable = @"
                            CREATE TABLE Users (
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
                            CREATE TABLE Cards (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
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
                            CREATE TABLE CardPacks (
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
        }

        public List<User> LoadUsers()
        {
            var users = new List<User>();
            using (var connection = new SqliteConnection(_connectionString))
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
            return users;
        }

        private Inventory LoadInventory(int userId)
        {
            var inventory = new Inventory(userId);
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                string selectCards = "SELECT * FROM Cards WHERE UserID = @UserID;";
                using (var command = new SqliteCommand(selectCards, connection))
                {
                    command.Parameters.AddWithValue("@UserID", userId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string name = reader.GetString(1);
                            int damage = reader.GetInt32(2);
                            ElementType element = (ElementType)reader.GetInt32(3);
                            CardType type = (CardType)reader.GetInt32(4);
                            Rarity rarityType = (Rarity)reader.GetInt32(5);
                            bool inDeck = reader.GetBoolean(6);

                            Card card;
                            if (type == CardType.Spell)
                            {
                                card = new SpellCard(name, damage, (CardTypes.ElementType)element, (CardTypes.Rarity)rarityType, inDeck, userId);
                            }
                            else
                            {
                                card = new MonsterCard(name, damage, (CardTypes.ElementType)element, (CardTypes.Rarity)rarityType, inDeck, userId);
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
            return inventory;
        }

        public void SaveUsers(List<User> users)
        {
            using (var connection = new SqliteConnection(_connectionString))
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

        private void SaveInventory(Inventory inventory, SqliteConnection connection, SqliteTransaction transaction)
        {
            foreach (var card in inventory.OwnedCards)
            {
                if (CardExists(card.Name, card.UserID, connection, transaction))
                {
                    string updateCard = @"
                            UPDATE Cards
                            SET Damage = @Damage, Element = @Element, Type = @Type, RarityType = @RarityType, InDeck = @InDeck
                            WHERE Name = @Name AND UserID = @UserID;";
                    using (var command = new SqliteCommand(updateCard, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Damage", card.Damage);
                        command.Parameters.AddWithValue("@Element", (int)card.Element);
                        command.Parameters.AddWithValue("@Type", (int)card.Type);
                        command.Parameters.AddWithValue("@RarityType", (int)card.RarityType);
                        command.Parameters.AddWithValue("@InDeck", card.InDeck);
                        command.Parameters.AddWithValue("@Name", card.Name);
                        command.Parameters.AddWithValue("@UserID", card.UserID);
                        command.ExecuteNonQuery();
                    }
                }
                else
                {
                    string insertCard = @"
                            INSERT INTO Cards (Name, Damage, Element, Type, RarityType, InDeck, UserID)
                            VALUES (@Name, @Damage, @Element, @Type, @RarityType, @InDeck, @UserID);";
                    using (var command = new SqliteCommand(insertCard, connection, transaction))
                    {
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

        private bool CardExists(string name, int userId, SqliteConnection connection, SqliteTransaction transaction)
        {
            string checkCardExists = "SELECT COUNT(1) FROM Cards WHERE Name = @Name AND UserID = @UserID;";
            using (var command = new SqliteCommand(checkCardExists, connection, transaction))
            {
                command.Parameters.AddWithValue("@Name", name);
                command.Parameters.AddWithValue("@UserID", userId);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }
    }
}
