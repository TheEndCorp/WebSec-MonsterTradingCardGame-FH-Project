using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SemesterProjekt1
{

    public class User
    {
        private int _id;
        private string _name;
        private string _password;
        private Inventory _Inventory;





        public User(int id, string name, string password)
        {
            this._id = id;
            this._name = name;
            this._password = password;
            this._Inventory = new Inventory(this._id);
        }
        [JsonConstructor]
        public User(int id, string name, string password, Inventory inventory)
        {
            this._id = id;
            this._name = name;
            this._password = password;
            this._Inventory = inventory ?? new Inventory(this._id);

        }


        ~User()
        {
            Console.WriteLine($"User {_name}, {_password} wird zerstört.");
        }

        public int Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; }
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


    }

}