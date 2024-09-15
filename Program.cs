using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.IO;


class Program
{
    static void Main()
    {
        Console.WriteLine("Hallo, Welt!");
        User[] Team1 = new User[3];
        for (int i = 0; i < Team1.Length; i++)
        {
            Team1[i] = new User(i, "Person" + i, "50" + i);
        }

        foreach (var person in Team1)
        {
            Console.WriteLine($"{person.Name} {person.Password}");
        }

        Team1[0].Name = "NewName";
        Array.Clear(Team1, 2, 1);

        Console.WriteLine("\nAfter changing the name of the first person and marking the last person as dead:");
        foreach (var person in Team1)
        {
            if (person != null)
            {
                Console.WriteLine($"{person.Name} {person.Password}");
            }
        }


        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/");
        listener.Start();
        Console.WriteLine("Webstart...");
        Process.Start(new ProcessStartInfo
        {
            FileName = "http://localhost:8080/users",
            UseShellExecute = true
        });


        UserServiceRequest requester = new UserServiceRequest();

        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            requester.HandleRequest(context);
            requester._userServiceHandler._databaseHandler.SaveUsers(requester._userServiceHandler._users);
            Console.WriteLine("Action");


        }
    }
}


