using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreSpeed;
using CoreSpeed.Models;
using McMaster.Extensions.CommandLineUtils;
using ahd.Graphite;

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
            bool singleServer = false;

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

            var graphite = app.Option("-g|--graphiteserver",
                $"Output the data to the specified graphite server",
                CommandOptionType.SingleValue);

            var graphitePrefix = app.Option("-x|--graphiteprefix",
                $"Prefix for graphite data, for instance a site or location name/code",
                CommandOptionType.SingleValue);

            var listServers = app.Option("-l|--list",
                $"List available servers and ID",
                CommandOptionType.NoValue);

            var serverID = app.Option("-i|--serverid",
                $"Use specific server based on ID gathered from the list option",
                CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                if (simple.HasValue() && delimited.HasValue())
                {
                    Console.WriteLine("simple and delimited modes cannot be run together\n");
                    return (20);
                }

                if (graphite.HasValue() && !graphitePrefix.HasValue())
                {
                    Console.WriteLine("you must supply a prefix when using a graphite server\n");
                    return (22);
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

                if (listServers.HasValue())
                {
                    simpleRun = true;
                    var listservers = SelectServers(simpleRun, ultraRun, servercount: 15);

                    Console.WriteLine("Printing top 15 servers");
                    var list = listservers.OrderBy(x => x.Latency).Select(
                        x => new Tuple<int, string, string, string>(x.Id, x.Sponsor, x.Name, x.Latency + "ms")
                        ).ToList();
                    list.ForEach(Console.WriteLine);
                    return (0);
                }

                settings.Download.ThreadsPerUrl = 8;
                settings.Upload.ThreadsPerUrl = 8;

                if (serverID.HasValue())
                {
                    singleServer = true;
                }

                var servers = SelectServers(simpleRun, ultraRun, singleServer);
                var bestServer = SelectBestServer(servers, simpleRun, ultraRun, singleServer);

                if (serverID.HasValue())
                {
                    bestServer = (Server)servers.Where(x => x.Id == int.Parse(serverID.Value())).FirstOrDefault();
                    Console.Write(simpleRun || ultraRun ? string.Empty : "Using server : " + bestServer.Sponsor + " (" + bestServer.Latency + "ms)\n");
                }

                Console.Write(simpleRun || ultraRun ? string.Empty : "Testing speed...\n");

                if (ultraRun)
                {
                    PrintLatency(bestServer.Latency, simpleRun, ultraRun, delimiter);
                }

                var downloadSpeed = client.TestDownloadSpeed(bestServer, settings.Download.ThreadsPerUrl);
                PrintSpeed("Download", downloadSpeed, simpleRun, ultraRun, delimiter);

                var uploadSpeed = client.TestUploadSpeed(bestServer, settings.Upload.ThreadsPerUrl);
                PrintSpeed("Upload", uploadSpeed, simpleRun, ultraRun, delimiter);

                // If we specify to save the data into graphite, then let's do that now
                if (graphite.HasValue())
                {
                    var timeStamp = DateTime.Now;

                    PushDataToGraphite(graphite.Value(), $"speedtest.{graphitePrefix}.dl", downloadSpeed, timeStamp);
                    PushDataToGraphite(graphite.Value(), $"speedtest.{graphitePrefix}.upl", uploadSpeed, timeStamp);
                    PushDataToGraphite(graphite.Value(), $"speedtest.{graphitePrefix}.ms", bestServer.Latency, timeStamp);
                }

                // If we are not in delimiter mode, i.e. running from a script, wait for user input before closing
                if (!ultraRun)
                {
                    Console.WriteLine("Press a key to exit.");
                    Console.ReadKey();
                }

                return (0);
            });

            app.Execute(args);
        }

        private static void PushDataToGraphite(string server, string series, double value, DateTime timeStamp)
        {
            var client = new GraphiteClient(server);
            client.Send(series, value, timeStamp);

            //var datapoints = new[]
            //{
            //    new Datapoint(series,value,DateTime.Now),
            //};

        }

        private static Server SelectBestServer(IEnumerable<Server> servers, bool simple, bool ultra, bool singleServer)
        {
            var bestServer = servers.OrderBy(x => x.Latency).First();

            if (!(simple || ultra || singleServer))
            {
                Console.WriteLine();
                Console.WriteLine("Best server by latency:");

                PrintServerDetails(bestServer);
                Console.WriteLine();
            }
            else if (!singleServer)
            {
                Console.Write(ultra ? $"{bestServer.Latency}" : $"Ping: {bestServer.Latency} ms\n");
            }

            return bestServer;
        }

        private static IEnumerable<Server> SelectServers(bool simple, bool ultra, bool singleServer = false, int servercount = 10)
        {
            Console.Write(simple || ultra || singleServer ? string.Empty : "Selecting best server by distance...\n\n");

            var servers = settings.Servers.Where(s => s.Country.Equals(DefaultCountry1)).Take(servercount).ToList();

            foreach (var server in servers)
            {
                try
                {
                    server.Latency = client.TestServerLatency(server);
                    if (!(simple || ultra || singleServer))
                    {
                        PrintServerDetails(server);
                    }
                }
                catch (Exception ex)
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

        private static void PrintLatency(int latency, bool simple, bool ultra, string delimeter)
        {
            Console.Write(simple ? $"Ping: {latency}ms\n" : (ultra ? $"{latency}" : $"Ping: {latency}ms\n"));
        }
    }
}
