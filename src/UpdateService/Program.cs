using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;


namespace UpdateService
{
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main(string[] args)
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
			{ 
				new MainService() 
			};

            if (args.Length > 0 && args[0] == "-c")
            {
                // Run in debug
                ((MainService)ServicesToRun[0]).DebugStart();
                System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
            }

            ServiceBase.Run(ServicesToRun);
        }
    }
}
