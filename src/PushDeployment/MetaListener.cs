using System;
using Cometd.Bayeux;
using Cometd.Bayeux.Client;

namespace PushDeployment
{
    public class MetaListener : IMessageListener
    {
        public void onMessage(IClientSessionChannel channel, IMessage message)
        {
#if DEBUG
            Console.WriteLine("META: " + message.ToString());
#endif
        }
    }
}