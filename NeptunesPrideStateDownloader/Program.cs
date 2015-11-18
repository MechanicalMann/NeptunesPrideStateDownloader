using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NeptunesPrideStateDownloader
{
    class Program
    {
        private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static readonly CookieClient _client = new CookieClient();

        static void Main(string[] args)
        {
            Console.WriteLine("NEPTUNE'S PRIDE STATE DOWNLOADER");
            Console.WriteLine();

            var user = ConfigurationManager.AppSettings["username"];
            var pass = ConfigurationManager.AppSettings["password"];
            var game = ConfigurationManager.AppSettings["gameNumber"];
            var download = ConfigurationManager.AppSettings["downloadDirectory"];
            int refresh;

            if (String.IsNullOrEmpty(user) || String.IsNullOrEmpty(pass) || String.IsNullOrEmpty(game) || String.IsNullOrEmpty(download) || !Int32.TryParse(ConfigurationManager.AppSettings["refreshSeconds"], out refresh))
            {
                Console.WriteLine("CONFIGURE YOUR SHIT");
                WriteDone();
                return;
            }

            Console.WriteLine("Attempting authentication...");
            
            var form = new NameValueCollection
            {
                { "type", "login" },
                { "alias", user },
                { "password", pass },
            };

            _client.UploadValues("http://np.ironhelmet.com/arequest/login", "POST", form);

            if (_client.CookieContainer.Count == 0)
            {
                Console.WriteLine("AUTHENTICATION FAILED");
                WriteDone();
                return;
            }

            Console.WriteLine("We're in!");

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                _cancellationTokenSource.Cancel();
                eventArgs.Cancel = true;
            };

            var dir = new DirectoryInfo(download);
            if (!dir.Exists)
                dir.Create();

            // Do the thing
            Console.WriteLine("Please be patient while we monitor your every move.");
            Task.WaitAll(GetStates(dir, game, refresh, _cancellationTokenSource.Token));

            WriteDone();
        }

        private static void WriteDone()
        {
            Console.WriteLine();
            Console.WriteLine("It's over!");
            Console.ReadKey();
        }

        private static async Task GetStates(DirectoryInfo downloadDir, string game, int refresh, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var gameParams = new NameValueCollection
                {
                    { "type", "order" },
                    { "order", "full_universe_report" },
                    { "version", "7" },
                    { "game_number", game },
                };
                var res = await _client.UploadValuesTaskAsync("http://np.ironhelmet.com/grequest/order", "POST", gameParams);
                var json = Encoding.UTF8.GetString(res);
                dynamic state = JsonConvert.DeserializeObject(json);

                // Thanks to Quantumplation for figuring out which tick was which
                long tick = state.report.tick, player = state.report.player_uid;

                var filename = $"gamestate_{player:00}_{tick:00000000}.json";

                var path = Path.Combine(downloadDir.FullName, filename);
                if (!File.Exists(path))
                {
                    Console.WriteLine($"Found new tick: {tick}, saving state.");
                    File.WriteAllText(path, JsonConvert.SerializeObject(state, Formatting.Indented));
                }
                ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(refresh));
            }
        }
    }
}
