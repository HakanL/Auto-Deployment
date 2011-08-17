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
        private BayeuxClient client;
        private IClientSessionChannel callEventChannel;
        private string channelId;


        public event EventHandler<UpdateAvailableEventArgs> UpdateAvailable;

        public NotifyClient(string cometURL, string channelId)
        {
            this.channelId = channelId;
            if (!this.channelId.StartsWith("/"))
                this.channelId = "/" + this.channelId;

            var transports = new List<ClientTransport>();
            transports.Add(new LongPollingTransport(null));

            client = new BayeuxClient(cometURL, transports);
        }

        protected void RaiseUpdateAvailable(NameValueCollection nvc)
        {
            var handler = UpdateAvailable;
            if (handler != null)
                handler(this, new UpdateAvailableEventArgs(nvc));
        }

        public void Connect()
        {
            // Handshaking with oauth2
            var handshakeAuth = new Dictionary<string, Object>();

            handshakeAuth.Add("authType", "oauth2");
//TODO            handshakeAuth.Add("oauth_token", "safasfsad");

            var ext = new Dictionary<string, Object>();

            ext.Add("ext", handshakeAuth);
            client.handshake(ext);

            // Subscribe and call 'Initialize' after successful login
            client.getChannel(Channel_Fields.META_HANDSHAKE).addListener(this);
        }

        public void Disconnect()
        {
            client.disconnect();
        }

        public void onMessage(IClientSessionChannel channel, IMessage message)
        {
            if (channel.ChannelId.ToString().Equals(Channel_Fields.META_HANDSHAKE))
            {
                if (message.Successful)
                {
                    // Connected!
                    callEventChannel = client.getChannel(channelId);

                    callEventChannel.unsubscribe(this);
                    callEventChannel.subscribe(this);
                }
                return;
            }

            if (channel.ChannelId.ToString().Equals(channelId))
            {
                // Our message
                var data = message.DataAsDictionary;

                NameValueCollection nvc = new NameValueCollection(data.Count);
                foreach (var kvp in data)
                    nvc.Add(kvp.Key, kvp.Value.ToString());

                RaiseUpdateAvailable(nvc);
            }
        }
    }
}
