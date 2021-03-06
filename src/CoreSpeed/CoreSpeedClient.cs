﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreSpeed.Models;
using System.Net.Http;

namespace CoreSpeed
{
    public static class ConvertDistance
    {
        public static double ConvertMilesToKilometers(double miles) => miles * 1.609344;

        public static double ConvertKilometersToMiles(double kilometers) => kilometers * 0.621371192;
    }

    public class CoreSpeedClient
    {
        private const string ConfigUrl = "http://www.speedtest.net/speedtest-config.php";
        private const string ServersUrl = "http://www.speedtest.net/speedtest-servers.php";
        private readonly int[] downloadSizes = { 350, 500, 750, 1000, 1500, 2000 };//, 2500, 3000, 3500, 4000 };
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const int MaxUploadSize = 4;

        public Settings GetSettings()
        {
            using (var client = new CoreSpeedWebClient())
            {
                var settings = client.GetConfig<Settings>(ConfigUrl);
                var serversConfig = client.GetConfig<ServersList>(ServersUrl);

                serversConfig.CalculateDistances(settings.Client.GeoCoordinate);
                settings.Servers = serversConfig.Servers.OrderBy(s => s.Distance).ToList();

                return settings;
            }
        }

        /// <summary>
        /// Test latency (ping) to server
        /// </summary>
        /// <returns>Latency in milliseconds (ms)</returns>
        public int TestServerLatency(Server server, int retryCount = 3)
        {
            var latencyUri = CreateTestUrl(server, "latency.txt");
            var timer = new Stopwatch();

            using (var client = new CoreSpeedWebClient())
            {
                for (var i = 0; i < retryCount; i++)
                {
                    string testString;
                    try
                    {
                        timer.Start();
                        testString = client.GetWebRequest(latencyUri).Result;
                    }
                    catch (WebException)
                    {
                        continue;
                    }
                    finally
                    {
                        timer.Stop();
                    }

                    if (!testString.StartsWith("test=test"))
                    {
                        throw new InvalidOperationException("Server returned incorrect test string for latency.txt");
                    }
                }
            }

            return (int)timer.ElapsedMilliseconds / retryCount;
        }

        /// <summary>
        /// Test download speed to server
        /// </summary>
        /// <returns>Download speed in Kbps</returns>
        public double TestDownloadSpeed(Server server, int simultaniousDownloads = 2, int retryCount = 2)
        {
            var testData = GenerateDownloadUrls(server, retryCount);

            return TestSpeed(testData, async (client, url) =>
            {
                var data = await client.GetByteArrayAsync(url);

                return data.Length;
            }, simultaniousDownloads);
        }

        /// <summary>
        /// Test upload speed to server
        /// </summary>
        /// <returns>Upload speed in Kbps</returns>
        public double TestUploadSpeed(Server server, int simultaniousUploads = 2, int retryCount = 2)
        {
            var testData = GenerateUploadData(retryCount);

            return TestSpeed(testData, async (client, uploadData) =>
            {
                await client.PostAsync(server.Url, new StringContent(uploadData.ToString()));
                
                return uploadData[0].Length;
            }, simultaniousUploads);
        }


        private static double TestSpeed<T>(IEnumerable<T> testData, Func<HttpClient, T, Task<int>> doWork, int concurencyCount = 2)
        {
            var timer = new Stopwatch();
            var throttler = new SemaphoreSlim(concurencyCount);

            timer.Start();

            var downloadTasks = testData.Select(async data =>
            {
                await throttler.WaitAsync().ConfigureAwait(true);
                var client = new CoreSpeedWebClient();
                try
                {
                    var size = await doWork(client, data).ConfigureAwait(true);
                    return size;
                }
                finally
                {
                    client.Dispose();
                    throttler.Release();
                }
            }).ToArray();

            Task.Run(() => downloadTasks);

            timer.Stop();

            //double totalSize = downloadTasks.Sum(task => task.Result);
            double totalSize = downloadTasks.Sum(task => task.Result);
            
            return (totalSize * 8 / 1024) / ((double)timer.ElapsedMilliseconds / 1000);
        }

        private static IEnumerable<NameValueCollection> GenerateUploadData(int retryCount)
        {
            var random = new Random();
            var result = new List<NameValueCollection>();

            for (var sizeCounter = 1; sizeCounter < MaxUploadSize + 1; sizeCounter++)
            {
                var size = sizeCounter * 200 * 1024;
                var builder = new StringBuilder(size);

                for (var i = 0; i < size; ++i)
                    builder.Append(Chars[random.Next(Chars.Length)]);

                for (var i = 0; i < retryCount; i++)
                {
                    result.Add(new NameValueCollection { { string.Format("content{0}", sizeCounter), builder.ToString() } });
                }
            }

            return result;
        }

        private static string CreateTestUrl(Server server, string file)
        {
            return new Uri(new Uri(server.Url), ".").OriginalString + file;
        }

        private IEnumerable<string> GenerateDownloadUrls(Server server, int retryCount)
        {
            var downloadUriBase = CreateTestUrl(server, "random{0}x{0}.jpg?r={1}");
            foreach (var downloadSize in downloadSizes)
            {
                for (var i = 0; i < retryCount; i++)
                {
                    yield return string.Format(downloadUriBase, downloadSize, i);
                }
            }
        }
    }
}