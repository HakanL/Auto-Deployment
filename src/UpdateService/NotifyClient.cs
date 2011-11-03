using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Text;

using Cometd.Client;
using Cometd.Client.Transport;
using Cometd.Bayeux;
using Cometd.Bayeux.Client;
using Cometd.Common;

using log4net;


namespace UpdateService
{
    public class UpdateAvailableEventArgs : EventArgs
    {
        private NameValueCollection nvc;

        public UpdateAvailableEventArgs(NameValueCollection nvc)
        {
            this.nvc = nvc;
        }

        public NameValueCollection Properties
        {
            get
            {
                return nvc;
            }
        }
    }


    public class NotifyClient : IMessageListener
    {
        // Create a logger for use in this class
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private BayeuxClient client;
        private IClientSessionChannel callEventChannel;
        private string channelId;
        private bool subscribed;


        public event EventHandler<UpdateAvailableEventArgs> UpdateAvailable;

        public NotifyClient(string cometURL, string channelId)
        {
            this.channelId = channelId;
            if (!this.channelId.StartsWith("/"))
                this.channelId = "/" + this.channelId;

            var transports = new List<ClientTransport>();
            transports.Add(new LongPollingTransport(null));

            client = new BayeuxClient(cometURL, transports);

            client.getChannel(Channel_Fields.META + "/**")
                .addListener(this);

            callEventChannel = client.getChannel(this.channelId);
        }

        protected void RaiseUpdateAvailable(NameValueCollection nvc)
        {
            var handler = UpdateAvailable;
            if (handler != null)
                handler(this, new UpdateAvailableEventArgs(nvc));
        }

        public void Connect()
        {
            client.handshake();
        }

        public bool WaitForConnected(int timeoutMS)
        {
            if (client.Connected)
                return true;

            var status = client.waitFor(timeoutMS, new BayeuxClient.State[] { BayeuxClient.State.CONNECTED });
            if (status != BayeuxClient.State.CONNECTED)
                return false;

            return true;
        }

        public void Disconnect()
        {
            client.waitForEmptySendQueue(5000);
            callEventChannel.unsubscribe();

            client.waitForEmptySendQueue(5000);
            client.disconnect();
            client.waitFor(1000, new BayeuxClient.State[] { BayeuxClient.State.DISCONNECTED });
        }


        public void Publish(string data)
        {
            callEventChannel.publish(data);

            log.Debug("Sending data: " + data);
        }


        public void PublishStatus(string status)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("\"command\":\"status\",");
            sb.AppendFormat("\"computer\":\"{0}\",", Environment.MachineName);
            sb.AppendFormat("\"status\":\"{0}\"", status);

            Publish('{' + sb.ToString() + '}');
        }


        public void onMessage(IClientSessionChannel channel, IMessage message)
        {
            if (message.Channel.Equals(Channel_Fields.META_HANDSHAKE))
            {
                subscribed = false;
            }

            if (message.Channel.Equals(Channel_Fields.META_CONNECT) && message.Successful)
            {
                if (!subscribed)
                {
                    // Subscribe
                    callEventChannel.subscribe(this);
                    subscribed = true;
                }
            }

            if (message.ChannelId.ToString().Equals(channelId))
            {
                // Our message
                var data = message.DataAsDictionary;

                NameValueCollection nvc = new NameValueCollection(data.Count);
                foreach (var kvp in data)
                    nvc.Add(kvp.Key, kvp.Value.ToString());

                string command = nvc["command"];
                if (string.IsNullOrEmpty(command) || command == "status")
                    return;

                log.InfoFormat("Received command = {0}", command);

                string computer = nvc["computer"];
                if (!string.IsNullOrEmpty(computer) && !computer.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                    return;

                switch (command)
                {
                    case "deploy":
                        RaiseUpdateAvailable(nvc);
                        break;
                    case "ping":
                        PublishStatus("Pong");
                        break;
                }

                return;
            }

            try
            {
                log.Debug("Message on " + message.ChannelId.ToString() + "   Data: " + message.ToString());
            }
            catch
            {
            }
        }
    }
}
