using HerboldRacing;
using iRacingRPC.Configuration;
using NetDiscordRpc;
using NetDiscordRpc.RPC;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

#pragma warning disable CS8618

namespace iRacingRPC
{
    internal class iRacingRPC
    {
        public DiscordRPC? discord { get; private set; }
        public string carPosition { get; private set; }

        public static ConfigJson Config = new ConfigJson();
        private IRSDKSharper irsdkSharper;
        private bool isOnSessionInfoExecuted = false;

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

            while (true)
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

            string presenceDetails = "";
            string presenceState = "";

            int teamRace = 0;
            int telemetryCounter = 0;
            string trackName = "";
            string carName = "";
            int carClasses = 0;
            string totalPositions = "";
            string sessionType = "";
            int currentLapNumber = 0;
            int totalLaps = 0;
            string bestLap = "";
            int bestLapInt = 0;
            var button = new Button[]
            {
                new Button()
                {
                    Label = "Powered by iRacingRPC",
                    Url = "https://github.com/xKaelyn/iRacingRPC"
                }
            };

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

            irsdkSharper.OnException += OnException;
            irsdkSharper.OnConnected += OnConnected;
            irsdkSharper.OnDisconnected += OnDisconnected;
            irsdkSharper.OnSessionInfo += OnSessionInfo;
            irsdkSharper.OnTelemetryData += OnTelemetryData;
            irsdkSharper.OnStopped += OnStopped;

            // Fire the OnTelemetryData event every 60 seconds.
            irsdkSharper.UpdateInterval = 60;

            irsdkSharper.Start();

            void OnException(Exception exception)
            {
                Log.Fatal($"Exception captured: {exception.Message}");
            }

            void OnConnected()
            {
                Log.Information("iRacing detected. Good luck!");
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
                teamRace = irsdkSharper.Data.SessionInfo.WeekendInfo.TeamRacing;
                carClasses = irsdkSharper.Data.SessionInfo.WeekendInfo.NumCarClasses;
                sessionType = irsdkSharper.Data.SessionInfo.SessionInfo.Sessions[0].SessionType;
                var totalLapsString = irsdkSharper.Data.SessionInfo.SessionInfo.Sessions[0].SessionLaps;
                
                // Not working - can't parse the int given to any usable format
                bestLapInt = irsdkSharper.Data.GetInt("LapBestLapTime");
                TimeSpan bestLapTime = TimeSpan.FromTicks(bestLapInt);
                bestLap = bestLapTime.ToString(@"mm\:ss\.fff");
                Log.Information($"Best Lap: {bestLap}");


                if (totalLapsString != "unlimited")
                {
                    totalLaps = int.Parse(totalLapsString);
                }

                Log.Information($"Session Type: {sessionType}");

                Log.Information($"OnSessionInfo fired! | Track name: {trackName} | Car Name: {carName}");
                isOnSessionInfoExecuted = true;
            }

            void OnTelemetryData()
            {
                if (!isOnSessionInfoExecuted)
                {
                    Log.Warning("SessionInfo has not been fetched yet - this should resolve itself automatically in a few seconds.");
                }
                else
                {
                    // If the telemetry counter is at 0, show presence page one.
                    if (telemetryCounter == 0)
                    {
                        switch (GetCarClassPosition())
                        {
                            case "Not On Track":
                            discord.SetPresence(new RichPresence()
                            {
                                Details = $"{sessionType} | {trackName}",
                                State = $"Sitting in the Pit Lane",
                                Assets = new Assets()
                                {
                                    LargeImageKey = "iracing",
                                    LargeImageText = "iRacing",
                                    SmallImageKey = "iracing",
                                    SmallImageText = "iRacing",
                                },
                                Buttons = button
                            });
                            break;
                        }

                        if (sessionType == "Practice" || sessionType == "Qualify")
                        {
                            discord.SetPresence(new RichPresence()
                            {
                                Details = $"{sessionType} | {trackName}",
                                State = $"{carName} | P{GetCarClassPosition()} / P{totalPositions} | Lap ",
                                Assets = new Assets()
                                {
                                    LargeImageKey = "iracing",
                                    LargeImageText = "iRacing",
                                    SmallImageKey = "iracing",
                                    SmallImageText = "iRacing",
                                },
                                Buttons = button,
                                Party = new Party()
                                {
                                    ID = "ae488379-351d-4a4f-ad32-2b9b01c91657",
                                    Size = currentLapNumber,
                                    Max = totalLaps
                                }
                            });
                        }
                        else
                        {
                            currentLapNumber = irsdkSharper.Data.GetInt("Lap", GetPlayerCarIndex());
                            discord.SetPresence(new RichPresence()
                            {
                                Details = $"{sessionType} | {trackName}",
                                State = $"{carName} | P{GetCarClassPosition()} / P{totalPositions} | Lap ",
                                Assets = new Assets()
                                {
                                    LargeImageKey = "iracing",
                                    LargeImageText = "iRacing",
                                    SmallImageKey = "iracing",
                                    SmallImageText = "iRacing",
                                },
                                Buttons = button,
                                Party = new Party()
                                {
                                    ID = "ae488379-351d-4a4f-ad32-2b9b01c91657",
                                    Size = currentLapNumber,
                                    Max = totalLaps
                                }
                            });
                        }
                    }
                    // If the telemetry counter is at 20, show presence page two.
                    else if (telemetryCounter == 20)
                    {
                        Log.Information("Displaying Page 2");

                        discord.SetPresence(new RichPresence()
                        {
                            Details = $"{sessionType} | {trackName}",
                            State = $"Best Lap: {bestLap}",
                            Assets = new Assets()
                            {
                                LargeImageKey = "iracing",
                                LargeImageText = "iRacing",
                                SmallImageKey = "iracing",
                                SmallImageText = "iRacing",
                            },
                            Buttons = button
                        });
                    }
                    // Once the telemetry counter reaches 40, reset to 0
                    else if (telemetryCounter == 40)
                    {
                        telemetryCounter = -1;
                    }

                    Log.Information($"Telemetry Counter: {telemetryCounter}");
                    // Increment the telemetry counter.
                    telemetryCounter++;
                }
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

        // Get the current car position (includes check for multiple classes)
        public string GetCarClassPosition()
        {
            int carClasses = irsdkSharper.Data.SessionInfo.WeekendInfo.NumCarClasses;

            // Check if there are multiple car classes
            if (carClasses > 1)
            {
                // If there are multiple car classes, get the position of the player's car within its class
                carPosition = irsdkSharper.Data.GetInt("CarIdxClassPosition", GetPlayerCarIndex()).ToString();
            }
            else
            {
                // If there is only one car class, get the overall position of the player's car
                carPosition = irsdkSharper.Data.GetInt("CarIdxPosition", GetPlayerCarIndex()).ToString();
            }

            // Check if the car position is 0, indicating that the car is not on track
            if (carPosition == "0")
            {
                return "Not On Track";
            }

            return carPosition;
        }

        public void GetTeamRaceInfo(int teamRace)
        {
            // WIP - Not implemented yet
            if (teamRace == 1)
            {

            }
        }
    }
}