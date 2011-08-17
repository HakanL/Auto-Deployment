using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Reflection;


namespace UpdateService
{
    public partial class MainService : ServiceBase
    {
        private NotifyClient notifyClient;


        public MainService()
        {
            InitializeComponent();

            notifyClient = new NotifyClient(Properties.Settings.Default.CometURL, Properties.Settings.Default.CometChannel);

            notifyClient.UpdateAvailable += new EventHandler<UpdateAvailableEventArgs>(notifyClient_UpdateAvailable);
        }

        private string ReplaceProperties(string input, System.Collections.Specialized.NameValueCollection nvc)
        {
            string arg = input;
            foreach (var key in nvc.AllKeys)
            {
                arg = arg.Replace("{" + key + "}", nvc[key]);
            }

            return arg;
        }

        private void notifyClient_UpdateAvailable(object sender, UpdateAvailableEventArgs e)
        {
            try
            {
                string updaterPath;
                if (System.IO.Path.IsPathRooted(Properties.Settings.Default.UpdaterFolder))
                    updaterPath = Properties.Settings.Default.UpdaterFolder;
                else
                    updaterPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                        Properties.Settings.Default.UpdaterFolder);

                string downloadPath;
                if (System.IO.Path.IsPathRooted(Properties.Settings.Default.DownloadFolder))
                    downloadPath = Properties.Settings.Default.DownloadFolder;
                else
                    downloadPath = System.IO.Path.Combine(updaterPath, Properties.Settings.Default.DownloadFolder);

                if (!System.IO.Directory.Exists(downloadPath))
                    System.IO.Directory.CreateDirectory(downloadPath);

                // Download everything
                var webClient = new System.Net.WebClient();
                foreach (var downloadItem in Properties.Settings.Default.Downloads)
                {
                    if (!downloadItem.Contains("/"))
                        continue;

                    var downloadUrl = ReplaceProperties(downloadItem, e.Properties);

                    string filename = downloadUrl.Substring(downloadUrl.LastIndexOf('/') + 1);
                    string destFile = System.IO.Path.Combine(downloadPath, filename);

                    var uri = new Uri(downloadUrl);

                    if (string.IsNullOrEmpty(uri.UserInfo))
                        webClient.Credentials = null;
                    else
                    {
                        var credInfo = uri.UserInfo.Split(':');
                        if (credInfo.Length == 2)
                            webClient.Credentials = new System.Net.NetworkCredential(credInfo[0], credInfo[1]);

                        downloadUrl = uri.Scheme + "://" + uri.Host + uri.PathAndQuery;
                    }

                    webClient.DownloadFile(downloadUrl, destFile);
                }

                Process updaterProcess = new Process();
                updaterProcess.StartInfo.WorkingDirectory = updaterPath;
                updaterProcess.StartInfo.FileName = Properties.Settings.Default.UpdaterExecutable;

                updaterProcess.StartInfo.Arguments = ReplaceProperties(Properties.Settings.Default.UpdaterParameters, e.Properties);
                updaterProcess.Start();
            }
            catch
            {
            }
        }

        protected override void OnStart(string[] args)
        {
            notifyClient.Connect();
        }

        protected override void OnStop()
        {
            notifyClient.Disconnect();
        }

        internal void DebugStart()
        {
            OnStart(null);
        }
    }
}
