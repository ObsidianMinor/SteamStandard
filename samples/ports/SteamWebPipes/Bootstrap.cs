﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Timers;
using Fleck;
using Newtonsoft.Json;
using Timer = System.Timers.Timer;
using System.Threading.Tasks;

namespace SteamWebPipes
{
    internal static class Bootstrap
    {
        private static int LastBroadcastConnectedUsers;
        private static List<IWebSocketConnection> ConnectedClients = new List<IWebSocketConnection>();
        public static string DatabaseConnectionString;

        private static void Main()
        {
            Console.Title = "SteamWebPipes";

            DatabaseConnectionString = ConfigurationManager.AppSettings["Database"];

            if (string.IsNullOrWhiteSpace(Bootstrap.DatabaseConnectionString))
            {
                Bootstrap.DatabaseConnectionString = null;

                Log("Database connectiong string is empty, will not try to get app names");
            }

            var cert = ConfigurationManager.AppSettings["X509Certificate"];

            var server = new WebSocketServer(ConfigurationManager.AppSettings["Location"]);
            server.SupportedSubProtocols = new[] { "steam-pics" };

            if (File.Exists(cert))
            {
                Log("Using certificate");
                server.Certificate = new X509Certificate2(cert);
            }

            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    ConnectedClients.Add(socket);

                    Log("Client #{2} connected: {0}:{1}", socket.ConnectionInfo.ClientIpAddress, socket.ConnectionInfo.ClientPort, ConnectedClients.Count);

                    socket.Send(JsonConvert.SerializeObject(new UsersOnlineEvent(ConnectedClients.Count)));
                };
                    
                socket.OnClose = () =>
                {
                    Log("Client #{2} disconnected: {0}:{1}", socket.ConnectionInfo.ClientIpAddress, socket.ConnectionInfo.ClientPort, ConnectedClients.Count);

                    ConnectedClients.Remove(socket);
                };
            });

            var steam = new Steam();

            if (File.Exists("last-changenumber.txt"))
            {
                steam.PreviousChangeNumber = uint.Parse(File.ReadAllText("last-changenumber.txt"));
            }

            CancellationTokenSource steamCancellation = new CancellationTokenSource();
            Task task = steam.StartAsync(steamCancellation.Token);

            var timer = new Timer();
            timer.Elapsed += TimerTick;
            timer.Interval = TimeSpan.FromSeconds(30).TotalMilliseconds;
            timer.Start();

            Console.CancelKeyPress += delegate
            {
                Console.WriteLine("Ctrl + C detected, shutting down.");
                File.WriteAllText("last-changenumber.txt", steam.PreviousChangeNumber.ToString());

                steam.IsRunning = false;
                steamCancellation.Cancel();
                task.GetAwaiter().GetResult();
                timer.Stop();

                foreach (var socket in ConnectedClients.ToList())
                {
                    socket.Close();
                }

                server.Dispose();
            };

            while(true)
            { }
        }

        private static void TimerTick(object sender, ElapsedEventArgs e)
        {
            var users = ConnectedClients.Count;

            if (users == 0 || users == LastBroadcastConnectedUsers)
            {
                return;
            }

            LastBroadcastConnectedUsers = users;

            Broadcast(new UsersOnlineEvent(users));
        }

        public static void Broadcast(AbstractEvent ev)
        {
            Broadcast(JsonConvert.SerializeObject(ev));
        }

        private static void Broadcast(string message)
        {
            for (int i = ConnectedClients.Count - 1; i >= 0; i--)
            {
                var socket = ConnectedClients[i];

                if (!socket.IsAvailable)
                {
                    Log("Removing dead client #{2}: {0}:{1}", socket.ConnectionInfo.ClientIpAddress, socket.ConnectionInfo.ClientPort, ConnectedClients.Count);

                    ConnectedClients.RemoveAt(i);

                    continue;
                }

                socket.Send(message);
            }
        }

        public static void Log(string format, params object[] args)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("R") + "] " + string.Format(format, args));
        }
    }
}
