using System;
using System.Collections.Generic;
using System.Text;
using Cometd.Bayeux;
using Cometd.Client;
using Cometd.Client.Transport;

namespace PushDeployment
{
    public class Program
    {
        private static Dictionary<string, bool?> computerStatus;
        private static DateTime lastStatus;

        public static void Main(string[] args)
        {
            try
            {
                var arguments = new CommandLine.Utility.Arguments(args, true);

                string cometUrl = arguments.GetValueOrDefault("cometurl", "http://comet.apphb.com/comet.axd");

                string channel = arguments.GetValueOrDefault("channel");
                if (!channel.StartsWith("/"))
                    channel = "/" + channel;

                if (string.IsNullOrEmpty(channel))
                    throw new ArgumentException("Missing channel");

                var transports = new List<ClientTransport>();
                transports.Add(new LongPollingTransport(null));

                var client = new BayeuxClient(cometUrl, transports);

                client.getChannel(Channel_Fields.META + "/**")
                    .addListener(new MetaListener());

                client.handshake();
                client.waitFor(10000, new List<BayeuxClient.State>() { BayeuxClient.State.CONNECTED });
                if (!client.Connected)
                {
                    throw new InvalidOperationException("Failed to connect to comet server");
                }

                computerStatus = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);

                bool listen = false;
                HashSet<string> waitForCompletion = null;
                int waitTimeoutSeconds = 30;

                StringBuilder jsonData = new StringBuilder();
                foreach (var kvp in arguments)
                {
                    switch (kvp.Key)
                    {
                        case "channel":
                        case "cometurl":
                            // Ignore
                            break;

                        case "listen":
                            listen = true;
                            break;
                        case "wait":
                            waitForCompletion = new HashSet<string>();
                            foreach (var server in kvp.Value.Split(','))
                                waitForCompletion.Add(server.Trim());
                            break;
                        case "waitsec":
                            waitTimeoutSeconds = int.Parse(kvp.Value);
                            break;
                        default:
                            Console.WriteLine(string.Format("Sending notify for data: {0}={1}", kvp.Key, kvp.Value));
                            if (jsonData.Length > 0)
                                jsonData.Append(',');
                            jsonData.AppendFormat("\"{0}\":\"{1}\"", kvp.Key, kvp.Value);
                            break;
                    }
                }

                var chn = client.getChannel(channel);

                var msgListener = new MsgListener();
                msgListener.StatusReceived += new EventHandler<StatusEventArgs>(MsgListener_StatusReceived);
                if (listen || waitForCompletion != null)
                {
                    chn.subscribe(msgListener);
                }

                client.waitForEmptySendQueue(1000);

                if (jsonData.Length > 0)
                {
                    chn.publish('{' + jsonData.ToString() + '}');
                    Console.WriteLine("Sending data: " + jsonData.ToString());
                }

                bool? overallResult = null;
                if (waitForCompletion != null)
                {
                    lastStatus = DateTime.Now;

                    while ((DateTime.Now - lastStatus).TotalSeconds <= waitTimeoutSeconds)
                    {
                        // Check if all servers have reported in
                        int failed = 0;
                        int succeeded = 0;
                        foreach (var server in waitForCompletion)
                        {
                            bool? status;
                            if (computerStatus.TryGetValue(server, out status))
                            {
                                if (status.HasValue)
                                {
                                    if (status.Value)
                                        succeeded++;
                                    else
                                        failed++;
                                }
                            }
                        }

                        if (failed + succeeded == waitForCompletion.Count)
                        {
                            // Done
                            overallResult = failed == 0;
                            break;
                        }

                        System.Threading.Thread.Sleep(100);
                    }

                    chn.unsubscribe(msgListener);
                }

                if (listen)
                {
                    Console.WriteLine("Hit <enter> to exit");
                    Console.ReadLine();

                    chn.unsubscribe(msgListener);
                }

                Console.WriteLine("Waiting for queue to be sent");
                client.waitForEmptySendQueue(1000);

                Console.WriteLine("Disconnecting");
                client.disconnect();
                client.waitFor(2000, new List<BayeuxClient.State>() { BayeuxClient.State.DISCONNECTED });

                if (waitForCompletion != null)
                {
                    foreach (var server in waitForCompletion)
                    {
                        bool? status;
                        computerStatus.TryGetValue(server, out status);

                        if (!status.HasValue)
                            Console.WriteLine(string.Format("{0} - no response", server));
                        else if(status.Value)
                            Console.WriteLine(string.Format("{0} - successful", server));
                        else
                            Console.WriteLine(string.Format("{1} - failure", server));
                    }

                    if (!overallResult.HasValue)
                    {
                        Console.WriteLine("Time out");
                        Environment.Exit(1);
                    }
                    else
                    {
                        if (overallResult.Value)
                        {
                            Console.WriteLine("Successful!");
                            Environment.Exit(0);
                        }
                        else
                        {
                            Console.WriteLine("Failure");
                            Environment.Exit(200);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                Environment.Exit(255);
            }
        }

        private static void MsgListener_StatusReceived(object sender, StatusEventArgs e)
        {
            lastStatus = DateTime.Now;

            switch (e.Status)
            {
                case Status.Successful:
                    computerStatus[e.Computer] = true;
                    break;
                case Status.Failure:
                    computerStatus[e.Computer] = false;
                    break;
                default:
                    if (!computerStatus.ContainsKey(e.Computer))
                        computerStatus[e.Computer] = null;
                    break;
            }
        }
    }
}