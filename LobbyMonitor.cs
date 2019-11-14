using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Windows.UI.Notifications;

namespace WH40KLobbyNotifier
{
    public class LobbyMonitor
    {
        public enum LobbyState
        {
            CLOSED,
            OPEN,
            FULL,
            GAME
        }

        private OperatingSystem os;
        private bool reading = false;
        private readonly int monitorInterval = 1000;
        private string logFilePath = "";
        private long streamPosition = 0;
        private Timer monitor;
        private int peers = 0;
        private int maxPeers = 0;
        private Dictionary<string, Regex> phrases = new Dictionary<string, Regex>
        {
            { "visible", new Regex(@"^Publishing Visible match") },
            { "start", new Regex(@"^APP -- Game Start") },
            { "stop", new Regex(@"^APP -- Game Stop") },
            { "exit", new Regex(@"^Datastore -- uninitialize complete") },
            { "mapchange", new Regex(@"^GetMaxFrameTimeFromProfile: players=([0-9]+) .*")},
            { "leave", new Regex(@"^Session::ProcessPeerMessages - Got DROPOUT from peer ([0-9]+)")},
            { "join", new Regex(@"^Host accepted Peer ([0-9]+) into the match at address list=WINaddr:\S*;, routes=") }
        };
        private LobbyState lobbyState = LobbyState.CLOSED;

        public event EventHandler MonitorStopped;

        public LobbyMonitor()
        {
            os = Environment.OSVersion;
            if (os.Platform == PlatformID.Win32NT)
            {
                logFilePath = Path.Combine(new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "My Games",
                    "dawn of war ii - retribution",
                    "LogFiles",
                    "warnings.txt"
                });
            }

            if (logFilePath.Length == 0)
            {
                Console.WriteLine("Platform not supported.");
                throw new PlatformNotSupportedException();
            }
            else
            {
                monitor = new Timer(MonitorTask, null, Timeout.Infinite, monitorInterval);
            }
        }

        private void MonitorTask(object state)
        {
            if (!reading)
            {
                reading = true;
                using (Stream stream = File.Open(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        stream.Seek(streamPosition, SeekOrigin.Begin);

                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Length > 15)
                            {
                                var match = line.Substring(15);

                                if (phrases["exit"].IsMatch(match))
                                {
                                    monitor.Change(Timeout.Infinite, monitorInterval);
                                    MonitorStopped?.Invoke(this, new EventArgs());
                                    Notify("Game has exited.");
                                    lobbyState = LobbyState.CLOSED;
                                }
                                else
                                {
                                    var peerNumber = 0;
                                    switch (lobbyState)
                                    {
                                        case LobbyState.CLOSED:
                                            if (phrases["visible"].IsMatch(match))
                                            {
                                                Notify("Monitoring active lobby!");
                                                lobbyState = LobbyState.OPEN;
                                                peers = 0;
                                            }
                                            break;
                                        case LobbyState.OPEN:
                                            if (phrases["start"].IsMatch(match))
                                            {
                                                Notify("Game has started.");
                                                lobbyState = LobbyState.GAME;
                                            }
                                            else if (phrases["mapchange"].IsMatch(match))
                                            {
                                                var result = phrases["mapchange"].Match(match);
                                                int.TryParse(result.Groups[1].Value, out maxPeers);
                                                Notify("Map has changed.");
                                                lobbyState = LobbyState.OPEN;
                                            }
                                            else if (phrases["join"].IsMatch(match))
                                            {
                                                var result = phrases["join"].Match(match);
                                                int.TryParse(result.Groups[1].Value, out peerNumber);
                                                peers += 1;
                                                Notify("Player joined!");

                                                if (peers >= maxPeers)
                                                {
                                                    Notify("Lobby has filled!");
                                                    lobbyState = LobbyState.FULL;
                                                }
                                                else
                                                {
                                                    lobbyState = LobbyState.OPEN;
                                                }
                                            }
                                            else if (phrases["leave"].IsMatch(match))
                                            {
                                                var result = phrases["leave"].Match(match);
                                                int.TryParse(result.Groups[1].Value, out peerNumber);
                                                peers -= 1;
                                                Notify("Player left!");
                                            }
                                            else if (phrases["stop"].IsMatch(match))
                                            {
                                                Notify("Lobby closed.");
                                                lobbyState = LobbyState.CLOSED;
                                            }
                                            break;
                                        case LobbyState.FULL:
                                            if (phrases["start"].IsMatch(match))
                                            {
                                                Notify("Game has started.");
                                                lobbyState = LobbyState.GAME;
                                            }
                                            else if (phrases["leave"].IsMatch(match))
                                            {
                                                var result = phrases["leave"].Match(match);
                                                int.TryParse(result.Groups[1].Value, out peerNumber);
                                                peers -= 1;
                                                Notify("Player left!");

                                                if (peers < maxPeers)
                                                {
                                                    lobbyState = LobbyState.OPEN;
                                                }
                                                else
                                                {
                                                    lobbyState = LobbyState.CLOSED;
                                                }
                                            }
                                            else if (phrases["stop"].IsMatch(match))
                                            {
                                                Notify("Lobby closed.");
                                                lobbyState = LobbyState.CLOSED;
                                            }
                                            break;
                                        case LobbyState.GAME:
                                            if (phrases["stop"].IsMatch(match))
                                            {
                                                Notify("Lobby closed.");
                                                lobbyState = LobbyState.CLOSED;
                                            }
                                            break;
                                    }
                                }
                            }
                            streamPosition = stream.Position;
                        }
                    }
                }
            }

            reading = false;
        }

        private void Notify(string text)
        {
            if (text != null && text.Length > 0)
            {
                Console.WriteLine(text);
                if (os.Platform == PlatformID.Win32NT && os.Version.Major >= 6 && os.Version.Minor >= 2)
                {
                    var template = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);

                    var textNodes = template.GetElementsByTagName("text");
                    textNodes.Item(0).InnerText = "Lobby Notifier";
                    textNodes.Item(1).InnerText = text;

                    var notifier = ToastNotificationManager.CreateToastNotifier("WH40KLobbyNotifier");
                    var notification = new ToastNotification(template);
                    notifier.Show(notification);
                }
            }
        }

        public void Start()
        {
            monitor.Change(0, monitorInterval);
        }

        public void Stop()
        {
            monitor.Change(Timeout.Infinite, monitorInterval);
        }
    }
}
