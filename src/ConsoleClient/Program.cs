using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreSpeed;
using CoreSpeed.Models;
using McMaster.Extensions.CommandLineUtils;

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
            string delimiter = ",";
            bool simpleRun = false;
            var app = new CommandLineApplication();
            bool ultraRun = false;

            app.Name = "ConsoleClient";
            app.Description = "Console Client for CoreSpeed";
            app.HelpOption("-?|-h|--help");

            var simple = app.Option("-s|--simple",
                "Run in simple mode",
                CommandOptionType.NoValue);

            var delimited = app.Option("-d|--delimited",
                $"Run in delimited mode, using the provided delimeter" +
                $"\nThe format is ping<delimeter>download speed<delimeter>upload speed",
                CommandOptionType.SingleValue);


            app.OnExecute(() =>
            {
                if (simple.HasValue() && delimited.HasValue())
                {
                    Console.WriteLine("simple and delimeted modes cannot be run together\n");
                    return (20);
                }

                if (simple.HasValue())
                {
                    simpleRun = true;
                }

                if (delimited.HasValue())
                {
                    delimiter = delimited.Value();
                    ultraRun = true;
                }

                client = new CoreSpeedClient();

                Console.Write(simpleRun ? string.Empty : (ultraRun ? string.Empty : "Getting Config..\n"));

                settings = client.GetSettings();

                settings.Download.ThreadsPerUrl = 8;
                settings.Upload.ThreadsPerUrl = 8;
                var servers = SelectServers(simpleRun, ultraRun);
                var bestServer = SelectBestServer(servers, simpleRun, ultraRun);

                Console.Write(simpleRun ? string.Empty : (ultraRun ? string.Empty : "Testing speed...\n"));

                var downloadSpeed = client.TestDownloadSpeed(bestServer, settings.Download.ThreadsPerUrl);
                PrintSpeed("Download", downloadSpeed, simpleRun, ultraRun, delimiter);

                var uploadSpeed = client.TestUploadSpeed(bestServer, settings.Upload.ThreadsPerUrl);
                PrintSpeed("Upload", uploadSpeed, simpleRun, ultraRun, delimiter);

                if (!ultraRun)
                {
                    Console.WriteLine("Press a key to exit.");
                    Console.ReadKey();
                }

                return (0);
            });

            app.Execute(args);
        }

        private static Server SelectBestServer(IEnumerable<Server> servers, bool simple, bool ultra)
        {
            var bestServer = servers.OrderBy(x => x.Latency).First();

            if (!(simple || ultra))
            {
                Console.WriteLine();
                Console.WriteLine("Best server by latency:");

                PrintServerDetails(bestServer);
                Console.WriteLine();
            }
            else
            {
                Console.Write(ultra ? $"{bestServer.Latency}" : $"Ping: {bestServer.Latency} ms\n");
            }

            return bestServer;
        }

        private static IEnumerable<Server> SelectServers(bool simple, bool ultra)
        {
            Console.Write(simple ? string.Empty : (ultra ? string.Empty : "Selecting best server by distance...\n\n"));
            var servers = settings.Servers.Where(s => s.Country.Equals(DefaultCountry1)).Take(10).ToList();

            foreach (var server in servers)
            {
                try
                {
                    server.Latency = client.TestServerLatency(server);
                    if (!(simple || ultra))
                    {
                        PrintServerDetails(server);
                    }
                }
                catch(Exception ex)
                {
                    server.Latency = 64445; //if we have a problem with this server, set the latency so high that it will never get selected.
                    //Console.WriteLine(ex.Message);
                }
            }
            return servers;
        }

        private static void PrintServerDetails(Server server) => Console.WriteLine($"Hosted by {server.Sponsor} ({server.Name}/{server.Country}), " +
                $"distance: {(int)server.Distance / 1000}km " +
                $"({Math.Round(ConvertDistance.ConvertKilometersToMiles((int)server.Distance / 1000), 2)}mi), " +
                $"latency: {server.Latency}ms");

        private static void PrintSpeed(string type, double speed, bool simple, bool ultra, string delimeter)
        {
            string speedText = "";

            if (speed > 1024)
            {
                speedText = ultra ? $"{delimeter}{Math.Round(speed / 1024 / 1024, 2)}" : $"{Math.Round(speed / 1024 / 1024, 2)} Mbps";
            }
            else
            {
                speedText = ultra ? $"{delimeter}{Math.Round(speed / 1024, 2)}" : $"{Math.Round(speed / 1024, 2)} Kbps";
            }

            Console.Write(simple ? $"{type}: {speedText}\n" : (ultra ? $"{speedText}" : $"{type} speed: {speedText}\n"));
        }
    }
}
