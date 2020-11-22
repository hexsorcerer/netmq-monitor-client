/*
 * A sample application showing the usage of NetMQMonitor.
 *
 * I created this sample application because figuring out how to use the monitor
 * was like most things in software development...easy once you know how, but
 * not at all obvious if you don't.
 *
 * This provides me with a space to play around with it, learn it, know it,
 * master its' finer points...and also for anyone else who might be struggling
 * to figure this thing out.
 *
 * It was written to be easy to read, and not cluttered by a lot of things
 * that are neccessary but unrelated to the monitor itself (settings, etc.).
 *
 * References:
 * https://github.com/zeromq/netmq/blob/master/src/NetMQ.Tests/NetMQMonitorTests.cs
 * https://csharp.hotexamples.com/examples/-/NetMQMonitor/-/php-netmqmonitor-class-examples.html
 */

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
            /*
             * The configuration isn't strictly neccessary for this to work,
             * it's only here as a convenience.
             */
            IConfigurationRoot configuration = GetConfiguration();

            // create the client socket and set the options it needs
            var dealerSocket = new DealerSocket();
            SetTcpKeepalive(dealerSocket, configuration);
            SetTcpKeepaliveInterval(dealerSocket, configuration);

            // the poller is needed to know when a message has been received
            var poller = new NetMQPoller();
            RunPoller(poller, dealerSocket);

            /*
             * Here's where the (non-intuitive) magic happens. You might think
             * that the monitor endpoint is the endpoint that the monitored
             * socket is connected to (as I did originally), but that's not
             * actually correct.
             *
             * This is a new endpoint specifically for the monitor, and it
             * must be inproc (any other protocol will not work). Even though
             * we're monitoring a TCP connection, the monitor itself is inproc
             * and so must be attached to this.
             *
             * The #monitor can be seen used in the official project in the
             * tests for the monitor, found in the references listed above.
             * This doesn't seem to be required, and in my limited testing it
             * seems any name can be used as long as the protocol is correct.
             */
            var monitorEndpoint = "inproc://#monitor";
            var monitor = new NetMQMonitor(
                dealerSocket,
                monitorEndpoint,
                SocketEvents.All);
            SetMonitorOptions(monitor);
            var monitorTask = Task.Factory.StartNew(monitor.Start);

            /*
             * The rest of this is just a conveninence to work with so you can
             * see the events happening in real time.
             */
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

        /// <summary>
        /// Gets the configuration from appsettings.json. Settings can be
        /// accessed by treating the configuration as a dictionary, and using
        /// the JSON keys as the dictionary keys.
        /// </summary>
        /// <example>
        /// string setting = configuration["JsonKey"];
        /// </example>
        /// <remarks>
        /// Note that all settings are returned as strings. If you need a
        /// setting with some other type, it will need to be parsed accordingly.
        /// </remarks>
        /// <returns>
        /// A <see cref="IConfigurationRoot"/> containing the contents
        /// of the appsettings.json file.
        /// </returns>
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

        /// <summary>
        /// Reads the value of the TcpKeepalive setting from the appsettings
        /// file, and then writes it into the related option on the socket.
        /// </summary>
        /// <param name="dealerSocket">The socket to set the options for.</param>
        /// <param name="configuration">The appsettings containing the setting.</param>
        private static void SetTcpKeepalive(
            DealerSocket dealerSocket,
            IConfigurationRoot configuration)
        {
            var tcpKeepalive = bool.Parse(configuration["TcpKeepalive"]);
            dealerSocket.Options.TcpKeepalive = tcpKeepalive;
        }

        /// <summary>
        /// Reads the value of the TcpKeepaliveInterval setting from the
        /// appsettings file, and then writes it into the related option on the
        /// socket.
        /// </summary>
        /// <param name="dealerSocket">The socket to set the options for.</param>
        /// <param name="configuration">The appsettings containing the setting.</param>
        private static void SetTcpKeepaliveInterval(
            DealerSocket dealerSocket,
            IConfigurationRoot configuration)
        {
            var tcpKeepaliveInterval =
                int.Parse(configuration["TcpKeepaliveIntervalInMs"]);
            dealerSocket.Options.TcpKeepaliveInterval = TimeSpan
                .FromMilliseconds(tcpKeepaliveInterval);
        }

        /// <summary>
        /// Runs the poller on the given socket. This is how we know when a
        /// a message has arrived.
        /// </summary>
        /// <param name="poller">The poller to run on the socket.</param>
        /// <param name="dealerSocket">The socket to be polled.</param>
        private static void RunPoller(NetMQPoller poller, DealerSocket dealerSocket)
        {
            poller.Add(dealerSocket);
            poller.RunAsync();
        }

        /// <summary>
        /// Reads the IP address and port from the configuration file, and
        /// assembles them into a TCP endpoint that can be used by a socket.
        /// </summary>
        /// <param name="configuration">An <see cref="IConfigurationRoot"/>
        /// containing the appsettings.</param>
        /// <returns>An endpoint that can be used by a socket.</returns>
        private static string GetTcpEndpoint(IConfigurationRoot configuration)
        {
            var ipAddress = configuration["IpAddress"];
            var port = configuration["Port"];
            return $"tcp://{ipAddress}:{port}";
        }

        /// <summary>
        /// Configures the monitor.
        /// </summary>
        /// <param name="monitor">The monitor to be configured.</param>
        private static void SetMonitorOptions(NetMQMonitor monitor)
        {
            monitor.Connected += OnConnected;
            monitor.Disconnected += OnDisconnected;
            monitor.Timeout = TimeSpan.FromMilliseconds(100);
        }

        /// <summary>
        /// Displays a menu for the user through the console UI.
        /// </summary>
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

        /// <summary>
        /// Sends a message on the given socket.
        /// </summary>
        /// <param name="socket">The socket to send the message on.</param>
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

        /// <summary>
        /// Waits for a message on the given socket. Blocks until a message is
        /// received.
        /// </summary>
        /// <param name="socket">The socket waiting for a message.</param>
        /// <returns>The message that was received.</returns>
        private static NetMQMessage WaitForMessage(DealerSocket socket)
        {
            Console.WriteLine("Waiting on message from router socket");
            return socket.ReceiveMultipartMessage();
        }

        /// <summary>
        /// Writes to the standard output when a connected event is monitored.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private static void OnConnected(object sender, NetMQMonitorSocketEventArgs e)
        {
            Console.WriteLine("Monitor saw a Connect event");
        }

        /// <summary>
        /// Writes to the standard output when a disconnected event is monitored.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private static void OnDisconnected(object sender, NetMQMonitorSocketEventArgs e)
        {
            Console.WriteLine("Monitor saw a Disconnected event");
        }
    }
}
