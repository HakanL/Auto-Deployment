using System;
using System.Collections.Generic;
using Cometd.Bayeux;
using Cometd.Bayeux.Client;

namespace PushDeployment
{
    public class MsgListener : IMessageListener
    {
        public event EventHandler<StatusEventArgs> StatusReceived;

        public void onMessage(IClientSessionChannel channel, IMessage message)
        {
            var data = message.DataAsDictionary;
#if DEBUG
            Console.WriteLine(message.ToString());
#endif

            string command = this.GetString(data, "command");
            switch (command)
            {
                case "status":
                case "statusOK":
                case "statusFAIL":
                    string computer = this.GetString(data, "computer");
                    string statusMessage = this.GetString(data, "status");

                    Console.WriteLine(string.Format("{0} - {1}", computer, statusMessage));

                    Status status;
                    if (command == "statusOK")
                        status = Status.Successful;
                    else if (command == "statusFAIL")
                        status = Status.Failure;
                    else
                        status = Status.Information;

                    var handler = StatusReceived;
                    if (handler != null)
                        handler(this, new StatusEventArgs(computer, status, statusMessage));
                    break;
            }
        }

        private string GetString(IDictionary<string, object> data, string key)
        {
            if (data.ContainsKey(key))
                return data[key].ToString();

            return null;
        }
    }
}