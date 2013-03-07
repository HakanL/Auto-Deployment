using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Reflection;

using log4net;
using log4net.Config;


namespace UpdateService
{
    public partial class MainService : ServiceBase
    {
        // Create a logger for use in this class
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private NotifyClient notifyClient;
        private object upgradeLock = new object();


        public MainService()
        {
            InitializeComponent();

            SetupNotifyClient();

            notifyClient.PublishStatus("Starting up");
        }

        private void SetupNotifyClient()
        {
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
            lock (upgradeLock)
            {
                try
                {
                    log.Warn("UpdateAvailable!");

                    foreach (var key in e.Properties.AllKeys)
                        log.DebugFormat("Property: {0}={1}", key, string.Join(", ", e.Properties.GetValues(key)));

                    if (!string.IsNullOrEmpty(Properties.Settings.Default.Password))
                    {
                        // Check password
                        if (!Properties.Settings.Default.Password.Equals(e.Properties["password"]))
                            throw new InvalidOperationException("Invalid password");
                    }

                    notifyClient.PublishStatus("Upgrade started for build: " + e.Properties["build"]);

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

                    try
                    {
                        // Empty download folder
                        System.IO.Directory.Delete(downloadPath, true);
                    }
                    catch
                    {
                    }

                    if (!System.IO.Directory.Exists(downloadPath))
                        System.IO.Directory.CreateDirectory(downloadPath);

                    var destFiles = new List<string>();
                    // Download everything
                    var webClient = new System.Net.WebClient();
                    foreach (var downloadItem in Properties.Settings.Default.Downloads)
                    {
                        string downloadUrl = downloadItem.Trim();

                        if (!downloadUrl.Contains("/") || !downloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            continue;

                        downloadUrl = ReplaceProperties(downloadUrl, e.Properties);

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

                        log.InfoFormat("Downloading from {0}", downloadUrl);

                        webClient.DownloadFile(downloadUrl, destFile);
                        destFiles.Add(destFile);
                    }

                    notifyClient.PublishStatus("Download complete");

                    if (Properties.Settings.Default.ExtractDownloadedZip)
                    {
                        foreach (var destFile in destFiles)
                        {
                            if (System.IO.Path.GetExtension(destFile).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                notifyClient.PublishStatus("Extracting " + System.IO.Path.GetFileName(destFile));

                                using (var zipFile = Ionic.Zip.ZipFile.Read(destFile))
                                {
                                    zipFile.ExtractAll(System.IO.Path.GetDirectoryName(destFile), Ionic.Zip.ExtractExistingFileAction.OverwriteSilently);
                                }

                                // Delete the file
                                try
                                {
                                    System.IO.File.Delete(destFile);
                                }
                                catch
                                {
                                }
                            }
                        }
                    }

                    // Shut down notify client during upgrade
                    notifyClient.Disconnect();
                    notifyClient = null;

                    var updaters = Properties.Settings.Default.Updaters;
                    if (updaters == null)
                        updaters = new List<UpdaterConfig>();
                    if (!string.IsNullOrEmpty(Properties.Settings.Default.UpdaterExecutable))
                    {
                        updaters.Add(new UpdaterConfig
                        {
                            Executable = Properties.Settings.Default.UpdaterExecutable,
                            Parameters = Properties.Settings.Default.UpdaterParameters
                        });
                    }

                    var logLines = new List<string>();
                    bool errors = false;
                    foreach (var updater in updaters)
                    {
                        Process updaterProcess = new Process();
                        updaterProcess.StartInfo.WorkingDirectory = updaterPath;
                        updaterProcess.StartInfo.FileName = System.IO.Path.Combine(updaterPath, updater.Executable);

                        updaterProcess.StartInfo.Arguments = ReplaceProperties(updater.Parameters, e.Properties);

                        updaterProcess.StartInfo.UseShellExecute = false;
                        updaterProcess.StartInfo.RedirectStandardOutput = true;
                        updaterProcess.StartInfo.RedirectStandardError = true;

                        log.Info("Starting update process");
                        log.DebugFormat("Launch: {0} {1}", updaterProcess.StartInfo.FileName, updaterProcess.StartInfo.Arguments);


                        updaterProcess.OutputDataReceived += (recv_sender, recv_e) =>
                        {
                            if (!string.IsNullOrEmpty(recv_e.Data))
                            {
                                log.Info(recv_e.Data);
                                logLines.Add("#" + recv_e.Data);
                            }
                        };

                        updaterProcess.ErrorDataReceived += (recv_sender, recv_e) =>
                        {
                            if (!string.IsNullOrEmpty(recv_e.Data))
                            {
                                log.Error(recv_e.Data);
                                errors = true;
                                logLines.Add("*" + recv_e.Data);
                            }
                        };

                        updaterProcess.Start();

                        updaterProcess.BeginOutputReadLine();
                        updaterProcess.BeginErrorReadLine();

                        updaterProcess.WaitForExit();
                        if (updaterProcess.ExitCode != 0)
                            errors = true;

                        updaterProcess.Close();

                        if (errors)
                            // Stop execution of upgrades if errors detected
                            break;
                    }

                    log.Info("Processing done");

                    SetupNotifyClient();
                    notifyClient.Connect();

                    notifyClient.WaitForConnected(20000);

                    // Send last 10 log messages
                    foreach (var logLine in logLines.Skip(logLines.Count - 10))
                        notifyClient.PublishStatus(logLine);

                    notifyClient.WaitForEmptyQueue();

                    if (errors)
                        notifyClient.PublishStatus("Upgrade with errors!!!", false);
                    else
                        notifyClient.PublishStatus("Upgrade done!", true);
                }
                catch (Exception ex)
                {
                    notifyClient.PublishStatus("Exception: " + ex.Message, false);
                }
            }
        }

        protected override void OnStart(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            log.Info("Starting up");
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
