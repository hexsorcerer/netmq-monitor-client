using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;

namespace DealerSocketTest
{
    class Program
    {
        static void Main(string[] args)
        {
            IConfigurationRoot configuration = GetConfiguration();

            var dealerSocket = new DealerSocket();
            SetTcpKeepalive(dealerSocket, configuration);
            SetTcpKeepaliveInterval(dealerSocket, configuration);

            var poller = new NetMQPoller();
            RunPoller(poller, dealerSocket);

            var monitorEndpoint = "inproc://monitor";
            var monitor = new NetMQMonitor(dealerSocket, monitorEndpoint);
            SetMonitorOptions(monitor);
            _ = Task.Factory.StartNew(monitor.Start);

            var tcpEndpoint = GetTcpEndpoint(configuration);
            NetMQMessage message = new NetMQMessage();
            while (true)
            {
                PrintMenu();
                Console.WriteLine("Enter your selection: ");
                int.TryParse(Console.ReadLine(), out int selection);
                Console.WriteLine();

                switch(selection)
                {
                    case 1:
                        dealerSocket.Connect(tcpEndpoint);
                        Console.WriteLine(
                            $"Dealer socket connected to {tcpEndpoint}");
                        Console.WriteLine();
                        break;
                    case 2:
                        dealerSocket.Disconnect(tcpEndpoint);
                        Console.WriteLine(
                            $"Dealer socket disconnected from {tcpEndpoint}");
                        Console.WriteLine();
                        break;
                    case 3:
                        SendMessage(dealerSocket);
                        break;
                    case 4:
                        message = WaitForMessage(dealerSocket);
                        var itr = message.GetEnumerator();
                        Console.WriteLine(
                            string.Format("{0} greeting frame", itr.MoveNext() ? "found" : "not found"));
                        Console.WriteLine(
                            string.Format("Message: {0}", itr.Current.ConvertToString()));
                        break;
                    default:
                        return;
                }
            };
        }

        private static IConfigurationRoot GetConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(
                    "appsettings.json",
                    optional: true,
                    reloadOnChange: true)
                .Build();
        }

        private static void SetTcpKeepalive(
            DealerSocket dealerSocket,
            IConfigurationRoot configuration)
        {
            var tcpKeepalive = bool.Parse(configuration["TcpKeepalive"]);
            dealerSocket.Options.TcpKeepalive = tcpKeepalive;
        }

        private static void SetTcpKeepaliveInterval(
            DealerSocket dealerSocket,
            IConfigurationRoot configuration)
        {
            var tcpKeepaliveInterval =
                int.Parse(configuration["TcpKeepaliveIntervalInMs"]);
            dealerSocket.Options.TcpKeepaliveInterval = TimeSpan
                .FromMilliseconds(tcpKeepaliveInterval);
        }

        private static void RunPoller(NetMQPoller poller, DealerSocket dealerSocket)
        {
            poller.Add(dealerSocket);
            poller.RunAsync();
        }

        private static string GetTcpEndpoint(IConfigurationRoot configuration)
        {
            var ipAddress = configuration["IpAddress"];
            var port = configuration["Port"];
            return $"tcp://{ipAddress}:{port}";
        }

        private static void SetMonitorOptions(NetMQMonitor monitor)
        {
            monitor.Connected += OnConnected;
            monitor.Disconnected += OnDisconnected;
            monitor.Timeout = TimeSpan.FromMilliseconds(100);
        }

        private static void PrintMenu()
        {
            Console.WriteLine("Select an option:");
            Console.WriteLine();
            Console.WriteLine("1: Connect");
            Console.WriteLine("2: Disconnect");
            Console.WriteLine("3: Send a message");
            Console.WriteLine("4: Wait for a message");
            Console.WriteLine("Any other option to quit");
            Console.WriteLine();
        }

        private static void SendMessage(DealerSocket socket)
        {
            var message = new NetMQMessage();
            Console.WriteLine("Type the message you want to send: ");
            var body = Console.ReadLine();
            message.Append(body);

            socket.SendMultipartMessage(message);
            Console.WriteLine();
            Console.WriteLine("Sent message to router socket");
            Console.WriteLine();
        }

        private static NetMQMessage WaitForMessage(DealerSocket socket)
        {
            Console.WriteLine("Waiting on message from router socket");
            return socket.ReceiveMultipartMessage();
        }

        private static void OnConnected(object sender, NetMQMonitorSocketEventArgs e)
        {
            Console.WriteLine("Monitor saw a Connect event");
        }

        private static void OnDisconnected(object sender, NetMQMonitorSocketEventArgs e)
        {
            Console.WriteLine("Monitor saw a Disconnected event");
        }
    }
}
