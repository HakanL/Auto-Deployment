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
                    throw new ArgumentNullException("Missing channel");

                if (!arguments.GetValues(string.Empty).Any())
                    throw new ArgumentException("Missing data (key=value)");

                var transports = new List<ClientTransport>();
                transports.Add(new LongPollingTransport(null));

                var client = new BayeuxClient(cometUrl, transports);

                client.handshake(10000);
                if (!client.Connected)
                {
                    throw new InvalidOperationException("Failed to connect to comet server");
                    // Here handshake is successful
                }

                foreach (var arg in arguments.GetValues(string.Empty))
                {
                    var kvp = arg.Split('=');
                    if (kvp.Length > 1)
                    {
                        var key = kvp[0];
                        var value = string.Join("=", kvp, 1, kvp.Length - 1);

                        Console.WriteLine(string.Format("Sending notify for data: {0}={1}", key, value));
                        client.getChannel(channel).publish(string.Format("{{\"{0}\":\"{1}\"}}", key, value));
                    }
                }

                client.disconnect();
                client.waitFor(1000, new List<BayeuxClient.State>() { BayeuxClient.State.DISCONNECTED });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                Environment.Exit(255);
            }
        }
    }
}
