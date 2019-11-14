using System;
using System.Threading;

namespace WH40KLobbyNotifier
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var lobbyMonitor = new LobbyMonitor();
            lobbyMonitor.MonitorStopped += LobbyMonitor_MonitorStopped;
            lobbyMonitor.Start();
            while (true)
            {
                Thread.Sleep(int.MaxValue);
            }
        }

        private static void LobbyMonitor_MonitorStopped(object sender, EventArgs e)
        {
            (sender as LobbyMonitor).Start();
        }
    }
}
