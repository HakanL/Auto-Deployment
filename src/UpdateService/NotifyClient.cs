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
        private Timer retryTimer;
        private bool firstConnect;


        public event EventHandler<UpdateAvailableEventArgs> UpdateAvailable;

        public NotifyClient(string cometURL, string channelId)
        {
            this.channelId = channelId;
            if (!this.channelId.StartsWith("/"))
                this.channelId = "/" + this.channelId;

            var transports = new List<ClientTransport>();
            transports.Add(new LongPollingTransport(null));

            retryTimer = new Timer(new TimerCallback(RetryTimerCallback), null, Timeout.Infinite, Timeout.Infinite);

            client = new BayeuxClient(cometURL, transports);

            firstConnect = true;
        }

        protected void RaiseUpdateAvailable(NameValueCollection nvc)
        {
            var handler = UpdateAvailable;
            if (handler != null)
                handler(this, new UpdateAvailableEventArgs(nvc));
        }

        private void RetryTimerCallback(object state)
        {
            if (!client.Handshook)
            {
                client.handshake();
                var status = client.waitFor(10000, new BayeuxClient.State[] { BayeuxClient.State.CONNECTED });
                if (status == BayeuxClient.State.CONNECTED)
                {
                    retryTimer.Change(Timeout.Infinite, Timeout.Infinite);

                    if (callEventChannel == null)
                    {
                        callEventChannel = client.getChannel(channelId);

                        callEventChannel.subscribe(this);
                    }

                    if (firstConnect)
                    {
                        firstConnect = false;

                        PublishStatus("Starting up");
                    }
                }
            }

        }

        public void Connect()
        {
            retryTimer.Change(0, 60000);
        }

        public void Disconnect()
        {
            retryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            client.disconnect();
        }


        public void Publish(string data)
        {
            if (callEventChannel == null)
                // Not connected yet
                return;

            callEventChannel.publish(data);
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
            if (channel.ChannelId.ToString().Equals(channelId))
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

            log.DebugFormat("Message on {0}   Data: {1}", message.ChannelId, message.Data);
        }
    }
}
