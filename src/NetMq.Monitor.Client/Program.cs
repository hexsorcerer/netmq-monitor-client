using System;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;

namespace DealerSocketTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var dealerSocket = new DealerSocket();
            var poller = new NetMQPoller();
            var endpoint = "tcp://192.168.2.128:49152";

            dealerSocket.Options.TcpKeepalive = true;
            dealerSocket.Options.TcpKeepaliveInterval = TimeSpan.FromMilliseconds(1000);

            poller.Add(dealerSocket);
            poller.RunAsync();

            var monitor = new NetMQMonitor(dealerSocket, endpoint);
            monitor.Connected += OnConnected;
            monitor.Disconnected += OnDisconnected;
            monitor.Timeout = TimeSpan.FromMilliseconds(100);
            var monitorTask = Task.Factory.StartNew(monitor.Start);

            NetMQMessage message = new NetMQMessage();
            while (true)
            {
                PrintMenu();
                Console.WriteLine("Enter your selection: ");
                var selection = int.Parse(Console.ReadLine());
                Console.WriteLine();

                switch(selection)
                {
                    case 1:
                        dealerSocket.Connect(endpoint);
                        Console.WriteLine($"Dealer socket connected to {endpoint}");
                        break;
                    case 2:
                        dealerSocket.Disconnect(endpoint);
                        Console.WriteLine($"Dealer socket disconnected from {endpoint}");
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
                        throw new ArgumentOutOfRangeException();
                }
            };
        }

        private static void PrintMenu()
        {
            Console.WriteLine("Select an option:");
            Console.WriteLine();
            Console.WriteLine("1: Connect");
            Console.WriteLine("2: Disconnect");
            Console.WriteLine("3: Send a message");
            Console.WriteLine("4: Wait for a message");
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
            Console.WriteLine("Connected");
        }

        private static void OnDisconnected(object sender, NetMQMonitorSocketEventArgs e)
        {
            Console.WriteLine("Disconnected");
        }
    }
}
