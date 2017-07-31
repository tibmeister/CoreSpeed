using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreSpeed;
using CoreSpeed.Models;

namespace ConsoleClient
{
    public class Program
    {
        private static Settings settings;
        private static CoreSpeedClient client;
        private const string DefaultCountry = "United States";

        public static string DefaultCountry1 => DefaultCountry;

        public static void Main(string[] args)
        {
            client = new CoreSpeedClient();

            Console.WriteLine("Getting Config...");
            settings = client.GetSettings();

            var servers = SelectServers();
            var bestServer = SelectBestServer(servers);

            Console.WriteLine("Testing speed...");

            var downloadSpeed = client.TestDownloadSpeed(bestServer, settings.Download.ThreadsPerUrl);
            PrintSpeed("Download", downloadSpeed);

            var uploadSpeed = client.TestUploadSpeed(bestServer, settings.Upload.ThreadsPerUrl);
            PrintSpeed("Upload", uploadSpeed);

            Console.WriteLine("Press a key to exit.");
            Console.ReadKey();

        }

        private static Server SelectBestServer(IEnumerable<Server> servers)
        {
            Console.WriteLine();
            Console.WriteLine("Best server by latency:");
            var bestServer = servers.OrderBy(x => x.Latency).First();
            PrintServerDetails(bestServer);
            Console.WriteLine();
            return bestServer;
        }

        private static IEnumerable<Server> SelectServers()
        {
            Console.WriteLine();
            Console.WriteLine("Selecting best server by distance...");
            var servers = settings.Servers.Where(s => s.Country.Equals(DefaultCountry1)).Take(10).ToList();

            foreach (var server in servers)
            {
                server.Latency = client.TestServerLatency(server);
                PrintServerDetails(server);
            }
            return servers;
        }

        private static void PrintServerDetails(Server server) => Console.WriteLine($"Hosted by {server.Sponsor} ({server.Name}/{server.Country}), " +
                $"distance: {(int)server.Distance / 1000}km " +
                $"({Math.Round(ConvertDistance.ConvertKilometersToMiles((int)server.Distance / 1000), 2)}mi), " +
                $"latency: {server.Latency}ms");

        private static void PrintSpeed(string type, double speed)
        {
            if (speed > 1024)
            {
                Console.WriteLine($"{type} speed: {Math.Round(speed / 1024 / 1024, 2)} Mbps");
            }
            else
            {
                Console.WriteLine($"{type} speed: {Math.Round(speed / 1024, 2)} Kbps");
            }
        }
    }
}
