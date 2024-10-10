using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Security.Principal;

namespace SemesterProjekt1
{
    class Program
    {
        static async Task Main()
        {
            /*
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
            } */

            static bool IsAdministrator()
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }



            if (!IsAdministrator())
            {
                Console.WriteLine("Die Anwendung wird nicht im Administratormodus ausgeführt. Verwende 'localhost' als lokale IP-Adresse.");
            }


            string localIPAddress = IsAdministrator() ? GetLocalIPAddress() : "localhost";
            using HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://{localIPAddress}:10001/");

            listener.Start();
            Console.WriteLine("Webstart...");
            Process.Start(new ProcessStartInfo
            {
                FileName = $"http://{localIPAddress}:10001/users",
                UseShellExecute = true
            });

            UserServiceRequest requester = new UserServiceRequest();

            // Zeige die Prozess-ID an
            Console.WriteLine($"Prozess-ID: {Process.GetCurrentProcess().Id}");

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(requester, context));
                DisplayThreadPoolInfo();
            }
        }

        private static async Task HandleRequestAsync(UserServiceRequest requester, HttpListenerContext context)
        {
            await requester.HandleRequestAsync(context);
            await Task.Run(() => requester._userServiceHandler._databaseHandler.SaveUsers(requester._userServiceHandler._users));
            Console.WriteLine("Action");
        }

        private static void DisplayThreadPoolInfo()
        {
            ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
            ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionPortThreads);

            Debug.WriteLine($"Verfügbare Worker-Threads: {workerThreads}");
            Debug.WriteLine($"Verfügbare IO-Completion-Threads: {completionPortThreads}");
            Debug.WriteLine($"Maximale Worker-Threads: {maxWorkerThreads}");
            Debug.WriteLine($"Maximale IO-Completion-Threads: {maxCompletionPortThreads}");
            Debug.WriteLine($"Minimale Worker-Threads: {minWorkerThreads}");
            Debug.WriteLine($"Minimale IO-Completion-Threads: {minCompletionPortThreads}");
        }

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("Keine IPv4-Adresse im lokalen Netzwerk gefunden.");
        }
    }
}