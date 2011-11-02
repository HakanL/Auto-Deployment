using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

using Cometd.Client;
using Cometd.Client.Transport;
using Cometd.Bayeux;
using Cometd.Bayeux.Client;
using Cometd.Common;


namespace PushDeployment
{
    public class MsgListener : IMessageListener
    {
        private string GetString(IDictionary<string, object> data, string key)
        {
            if (data.ContainsKey(key))
                return data[key].ToString();

            return null;
        }

        public void onMessage(IClientSessionChannel channel, IMessage message)
        {
            var data = message.DataAsDictionary;
//            Console.WriteLine(message.ToString());

            string command = GetString(data, "command");
            if (command == "status")
            {
                string computer = GetString(data, "computer");
                string status = GetString(data, "status");

                Console.WriteLine(string.Format("{0} - {1}", computer, status));
            }
        }
    }


    public class Program
    {
        private static NameValueCollection GetArguments(string[] args)
        {
            var arguments = new NameValueCollection();

            string key = string.Empty;
            foreach (var arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    if (!string.IsNullOrEmpty(key))
                        arguments.Add(key.ToLower(), null);

                    key = arg.Substring(1);
                }
                else
                {
                    arguments.Add(key.ToLower(), arg);

                    key = string.Empty;
                }
            }

            if (!string.IsNullOrEmpty(key))
                arguments.Add(key.ToLower(), null);

            return arguments;
        }


        public static void Main(string[] args)
        {
            try
            {
                var arguments = GetArguments(args);

                var cometUrl = "http://comet.apphb.com/comet.axd";      // Default
                if (arguments.GetValues("cometurl").Any())
                    cometUrl = arguments.GetValues("cometurl")[0];

                var channel = string.Empty;
                if (arguments.GetValues("channel").Any())
                    channel = arguments.GetValues("channel")[0];

                if (!channel.StartsWith("/"))
                    channel = "/" + channel;

                if (string.IsNullOrEmpty(channel))
                    throw new ArgumentException("Missing channel");

                if (arguments.GetValues(string.Empty) == null || !arguments.GetValues(string.Empty).Any())
                    throw new ArgumentException("Missing data (key=value)");

                var transports = new List<ClientTransport>();
                transports.Add(new LongPollingTransport(null));

                var client = new BayeuxClient(cometUrl, transports);

                client.handshake();
                client.waitFor(10000, new List<BayeuxClient.State>() { BayeuxClient.State.CONNECTED });
                if (!client.Connected)
                {
                    throw new InvalidOperationException("Failed to connect to comet server");
                }

                bool listen = false;

                StringBuilder jsonData = new StringBuilder();
                foreach (var arg in arguments.GetValues(string.Empty))
                {
                    var kvp = arg.Split('=');
                    if (kvp.Length > 1)
                    {
                        var key = kvp[0];
                        var value = string.Join("=", kvp, 1, kvp.Length - 1);

                        Console.WriteLine(string.Format("Sending notify for data: {0}={1}", key, value));
                        if (jsonData.Length > 0)
                            jsonData.Append(',');
                        jsonData.AppendFormat("\"{0}\":\"{1}\"", key, value);
                    }
                    else
                    {
                        if (arg.Equals("listen", StringComparison.OrdinalIgnoreCase))
                            listen = true;
                    }
                }

                var chn = client.getChannel(channel);

                var msgListener = new MsgListener();
                if (listen)
                {
                    chn.subscribe(msgListener);
                }

                if (jsonData.Length > 0)
                {
                    System.Threading.Thread.Sleep(500);
                    chn.publish('{' + jsonData.ToString() + '}');
                }

                if (listen)
                {
                    Console.WriteLine("Hit <enter> to exit");
                    Console.ReadLine();

                    chn.unsubscribe(msgListener);
                }
                else
                    // Sleep for a second since there's no way to wait for the message to be delivered
                    System.Threading.Thread.Sleep(1000);

                client.disconnect();
                client.waitFor(2000, new List<BayeuxClient.State>() { BayeuxClient.State.DISCONNECTED });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                Environment.Exit(255);
            }
        }
    }
}
