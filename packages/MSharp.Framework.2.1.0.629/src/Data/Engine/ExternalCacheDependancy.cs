using System;
using System.IO;
using System.Threading;

namespace MSharp.Framework.Data
{

    static class ExternalCacheDependancy
    {
        static FileSystemWatcher Watcher;
        static object SyncLock = new object();
        //   static bool ownsMutex;
        // static Mutex syncMutex ;//= new Mutex(false, typeof(ExternalCacheDependancy).FullName, out OwnsMutex,
        //   new MutexSecurity(typeof(ExternalCacheDependancy).FullName, AccessControlSections.All));
        //static ExternalCacheDependancy()
        //{
        //    // Create a string representing the current user.
        //   // var user = Environment.UserDomainName + "\\" + Environment.UserName;
        //    var user = "Network Service";

        //    var security = new MutexSecurity();
        //    security.AddAccessRule(new MutexAccessRule(user, MutexRights.Modify | MutexRights.Synchronize, AccessControlType.Allow));

        //    syncMutex = new Mutex(false, typeof(ExternalCacheDependancy).FullName, out ownsMutex, security);
        //}

        /// <summary>
        /// Creates a watcher on the current cache.
        /// </summary>
        internal static void CreateDependancy(string filePath)
        {
            Watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath))
            {
                Filter = Path.GetFileName(filePath),
                EnableRaisingEvents = true
            };

            Watcher.Created += watcher_Changed;
        }

        static void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            for (int retries = 10; retries > 0; retries--)
                try
                {
                    lock (SyncLock)
                    {
                        if (File.Exists(e.FullPath))
                            if (File.ReadAllText(e.FullPath) == AppDomain.CurrentDomain.BaseDirectory)
                                return;

                    }
                }
                catch { Thread.Sleep(200); }

            Database.Refresh();
        }

        internal static void UpdateSyncFile(string filePath)
        {
            var newContent = AppDomain.CurrentDomain.BaseDirectory;

            var file = filePath.AsFile();

            //try
            //{
            // syncMutex.WaitOne();

            lock (SyncLock)
            {
                file.Delete(harshly: true);
                file.WriteAllText(newContent);
            }

            //}
            //finally
            //{
            //    syncMutex.ReleaseMutex();
            //}
        }
    }
}