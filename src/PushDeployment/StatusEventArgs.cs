using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace PushDeployment
{
    public class StatusEventArgs : EventArgs
    {        
        public string Computer { get; set; }
        public string Message { get; set; }
        public Status Status { get; set; }

        public StatusEventArgs(string computer, Status status, string message)
        {
            this.Computer = computer;
            this.Status = status;
            this.Message = message;
        }
    }
}
