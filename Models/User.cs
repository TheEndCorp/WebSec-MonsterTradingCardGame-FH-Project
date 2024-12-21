using System.Text.Json.Serialization;

namespace SemesterProjekt1
{
    public class User
    {
        private int _id;

        private string _username;

        private string _password;

        private Inventory _Inventory;

        public int Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public string Username
        {
            get { return _username; }
            set { _username = value; }
        }

        public string Password
        {
            get { return _password; }
            set { _password = value; }
        }

        public Inventory Inventory
        {
            get { return _Inventory; }
            set { _Inventory = value; }
        }

        ~User()
        {
            Console.WriteLine($"User {_username}, {_password} wird zerstört.");
        }

        public User(int id, string username, string password)
        {
            this._id = id;
            this._username = username;
            this._password = password;
            this._Inventory = new Inventory(this._id);
        }

        [JsonConstructor]
        public User(int id, string username, string password, Inventory inventory)
        {
            this._id = id;
            this._username = username;
            this._password = password;
            this._Inventory = inventory ?? new Inventory(this._id);
        }
    }
}