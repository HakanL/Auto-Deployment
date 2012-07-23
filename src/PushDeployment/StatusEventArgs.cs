using System;

namespace PushDeployment
{
    public class StatusEventArgs : EventArgs
    {
        public StatusEventArgs(string computer, Status status, string message)
        {
            this.Computer = computer;
            this.Status = status;
            this.Message = message;
        }

        public string Computer { get; set; }

        public string Message { get; set; }

        public Status Status { get; set; }
    }
}