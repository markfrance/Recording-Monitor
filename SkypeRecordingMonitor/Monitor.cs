using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Id3;
using System.Globalization;
using System.Configuration;

namespace SkypeRecordingMonitor
{
    public partial class Monitor : ServiceBase
    {
        public Monitor()
        {
            InitializeComponent();
        }

        public const string Name = "SkypeRecordingMonitor";
        private string DefaultPath = ConfigurationManager.AppSettings.Get("DefaultPath");

        private FileSystemWatcher Watcher = null;
        private string Path;

        protected override void OnStart(string[] args)
        {
            if (args.Any())
            {
                if (Directory.Exists(Path))
                    Path = args[0];
                else
                {
                    Path = DefaultPath;
                    LogEvent($"Specified directory doesn't exist, using {DefaultPath}");
                }
            }
            else
                Path = DefaultPath;

            Start();
        }

        public void Start()
        {
            InitialiseWatcher();

        }

        private void InitialiseWatcher()
        {
            var fileFilter = ConfigurationManager.AppSettings.Get("FileFilter");
            Watcher = new FileSystemWatcher(Path, fileFilter)
            {
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName
            };

            Watcher.Changed += Watcher_Changed;
            Watcher.Created += Watcher_Changed;

            Watcher.EnableRaisingEvents = true;

            LogEvent("Logging started");
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e) => PostMp3Data(e.FullPath).Wait();

        private static CallRecordInfo GetMp3Info(string fullPath)
        {
            var fileBytes = System.IO.File.ReadAllBytes(fullPath);

            var mp3 = new Mp3File(fullPath);

            var tag = mp3.GetTag(Id3TagFamily.FileStartTag);

            return new CallRecordInfo
            {
                Date = tag.Comments[0].Comment,
                SkypeID = tag.Album.Value,
                FileData = Convert.ToBase64String(fileBytes)
            };
        }

        static bool IsReady(string fullPath)
        {
            try
            {
                using (Stream stream = new FileStream(fullPath, FileMode.Open))
                {
                    return stream.Length > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        static async Task PostMp3Data(string fullPath)
        {
            //Wait until whole file is written
            while (!IsReady(fullPath))
            {
                //Wait
            }

            LogEvent(fullPath + " changed");

            var call = GetMp3Info(fullPath);

            using (var client = new HttpClient())
            {
                var baseAddress = ConfigurationManager.AppSettings.Get("BaseAddress");
                var apiCall = ConfigurationManager.AppSettings.Get("ApiCall");
                try
                {
                    client.BaseAddress = new Uri(baseAddress);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await client.PostAsJsonAsync(apiCall, call);

                    if (response.IsSuccessStatusCode)
                    {
                        var url = response.Headers.Location;
                    }
                }
                catch (Exception e)
                {
                    LogEvent(e.Message);
                }

            }
        }

        protected override void OnStop()
        {
            Watcher.EnableRaisingEvents = false;
            Watcher.Dispose();

            LogEvent("Monitoring stopped");
        }

        private static void LogEvent(string message)
        {
            message = $"{DateTime.Now} : {message}";

            EventLog.WriteEntry(Name, message);
        }
    }
}
