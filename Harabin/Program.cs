namespace SmakenziBot
{
    using System;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using BroodWar.Api;
    using BroodWar.Api.Client;
    using Harabin;

    public class Program
    {
        public static void Main()
        {
            Connect();
            WaitForMatchStart();
            Console.WriteLine("Starting match");
            Game.Write($"The map is {Game.MapName}, a {Game.StartLocations.Count} player map");

            var bot = new Bot();
            bot.OnGameStart();
            RunMainGameLoop(bot);

            Console.WriteLine("Game ended");
        }

        private static void Connect()
        {
            var currentIdentity = WindowsIdentity.GetCurrent();
            GuardNotNull(currentIdentity);
            GuardIsAdministrator(currentIdentity);

            Console.WriteLine("Connecting...");
            Reconnect();
        }

        private static void GuardNotNull(WindowsIdentity currentIdentity)
        {
            if (currentIdentity == null)
                throw new ArgumentNullException(nameof(currentIdentity), "Windows identity cannot be null!");
        }

        private static void GuardIsAdministrator(WindowsIdentity currentIdentity)
        {
            var isAdministrator = new WindowsPrincipal(currentIdentity).IsInRole(WindowsBuiltInRole.Administrator);
            if (!isAdministrator)
                throw new Exception("In order to use shared memory you must run this application as administrator!");
        }

        private static void WaitForMatchStart()
        {
            Console.WriteLine("Waiting to enter match");
            while (!Game.IsInGame) Update();
        }

        private static void RunMainGameLoop(Bot bot)
        {
            while (Game.IsInGame)
            {
                bot.OnFrame();
                Update();
            }
        }

        private static void Update()
        {
            Client.Update();
            if (Client.IsConnected) return;

            Console.WriteLine("Reconnecting...");
            Reconnect();
        }

        private static void Reconnect()
        {
            while (!Client.Connect()) Task.Delay(1000).Wait();
        }
    }
}