using System.Net.NetworkInformation;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace SemesterProjekt1
{
    public class User
    {
        private int _id;

        private string _username;

        private string _password;

        private Inventory _Inventory;

        private string _bio;

        private string _image;

        private string _name;

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

        public string Bio
        {
            get { return _bio; }
            set { _bio = value; }
        }

        public string Image
        {
            get { return _image; }
            set { _image = value; }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; }
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
            this._image = string.Empty;
            this._bio = string.Empty;
            this._name = string.Empty;
        }

        [JsonConstructor]
        public User(int id, string username, string password, Inventory inventory, string bio, string image, string name)
        {
            this._id = id;
            this._username = username;
            this._password = password;
            this._Inventory = inventory ?? new Inventory(this._id);
            this._image = image ?? string.Empty;
            this._bio = bio ?? string.Empty;
            this._name = name ?? string.Empty;

        }

        public void GetNextAvailableId(List<User> userlist)
        {
            this.Id = userlist.Any() ? userlist.Max(u => u.Id) + 1 : 1;
        }
    }
}