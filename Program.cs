using NetDiscordRpc;
using iRacingRPC.Configuration;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Serilog;
using HerboldRacing;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Diagnostics;

namespace iRacingRPC
{
    internal class iRacingRPC
    {
        public DiscordRPC discord { get; private set; }
        public static ConfigJson Config = new ConfigJson();
        private IRSDKSharper irsdkSharper;

        static void Main(string[] args)
        {
            var iracing = new iRacingRPC();

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(theme: SystemConsoleTheme.Literate, restrictedToMinimumLevel: LogEventLevel.Information)
                .WriteTo.File("logs/log.txt", outputTemplate: "{Timestamp:dd MMM yyyy - hh:mm:ss tt} [{Level:u3}] {Message:lj}{NewLine}{Exception}", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Information)
                .MinimumLevel.Information()
                .CreateLogger();

            Log.Information("iRacingRPC | Version 1.0.0.0");
            Log.Information("Setting up..");

            while(true)
            {
                iracing.Initialize().GetAwaiter().GetResult();
                break;
            }
        }

        public async Task Initialize()
        {
            string json = await File.ReadAllTextAsync("assets/config/Configuration.json").ConfigureAwait(false);
            using (var fs = File.OpenRead("assets/config/Configuration.json")) Config = JsonConvert.DeserializeObject<ConfigJson>(json);

            var configJson = JsonConvert.DeserializeObject<ConfigJson>(json);

            Log.Information("Logger initialized.");
            Log.Information("If you have any problems, please raise a issue on GitHub and upload your log file in the logs folder.");

            if (configJson.AppId == "YOUR_APP_ID_HERE")
            {
                Log.Error("Please set your Discord App ID in assets/config/Configuration.json");
                return;
            }

            discord = new DiscordRPC(configJson.AppId);

            // Let's actually bring the Discord client online
            discord.Initialize();

            Log.Information("DiscordRPC initialized.");

            Log.Information("Program initialized. Setting up client..");

            irsdkSharper = new IRSDKSharper();

            string trackName = "";
            string carName = "";
            string carClassPosition = "";
            string totalPositions = "";
            var button = new NetDiscordRpc.RPC.Button[]
            {
                new NetDiscordRpc.RPC.Button()
                {
                    Label = "Powered by iRacingRPC",
                    Url = "https://github.com/xKaelyn/iRacingRPC"
                }
            };

            // hook up our event handlers
            irsdkSharper.OnException += OnException;
            irsdkSharper.OnConnected += OnConnected;
            irsdkSharper.OnDisconnected += OnDisconnected;
            irsdkSharper.OnSessionInfo += OnSessionInfo;
            irsdkSharper.OnTelemetryData += OnTelemetryData;
            irsdkSharper.OnStopped += OnStopped;

            // this means fire the OnTelemetryData event every 30 data frames (2 times a second)
            irsdkSharper.UpdateInterval = 30;

            irsdkSharper.Start();

            void OnException(Exception exception)
            {
                Log.Fatal($"Exception captured: {exception.Message}");
            }

            void OnConnected()
            {
                Log.Information("OnConnected() fired!");
            }

            void OnDisconnected()
            {
                Log.Information("OnDisconnected() fired!");
            }

            void OnSessionInfo()
            {
                trackName = irsdkSharper.Data.SessionInfo.WeekendInfo.TrackDisplayName;
                carName = irsdkSharper.Data.SessionInfo.DriverInfo.Drivers[GetPlayerCarIndex()].CarScreenName;
                totalPositions = irsdkSharper.Data.SessionInfo.DriverInfo.Drivers.Count.ToString();

                Log.Information($"OnSessionInfo fired! | Track name: {trackName} | Car Name: {carName}");
            }

            void OnTelemetryData()
            {
                carClassPosition = irsdkSharper.Data.GetInt("CarIdxClassPosition", GetPlayerCarIndex()).ToString();

                if (carClassPosition == "0")
                {
                    carClassPosition = "Not On Track";
                }

                Log.Information($"Position of driver: {carClassPosition} | Out of: {totalPositions}");
            }

            void OnStopped()
            {
                Log.Information("OnStopped() fired!");
            }

            await Task.Delay(-1).ConfigureAwait(false);
        }

        // Methods
        // Get the player car index
        public int GetPlayerCarIndex()
        {
            int playerId = irsdkSharper.Data.GetInt("PlayerCarIdx");
            return playerId;
        }
    }
}