namespace MSharp.Framework.Services.Testing
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Transactions;
    using System.Web;
    using System.Web.Script.Serialization;
    using Newtonsoft.Json;

    class Snapshot
    {
        const string TEMP_DATABASES_LOCATION_KEY = "Temp.Databases.Location";
        const string URL_FILE_NAME = "url.txt";
        static string DatabaseName = GetDatabaseName();
        string SnapshotName;
        bool IsInShareSnapshotMode;
        private static Mutex SnapshotRestoreLock;

        DirectoryInfo SnapshotsDirectory;

        public Snapshot(string name, bool isSharedSNapshotMode)
        {
            IsInShareSnapshotMode = isSharedSNapshotMode;
            SnapshotName = CreateSnapshotName(name);
            SnapshotsDirectory = GetSnapshotsRoot(IsInShareSnapshotMode).GetSubDirectory(SnapshotName, onlyWhenExists: false);
        }

        public void Create(HttpContext context)
        {
            if (IsSnapshotsDisabled) return;

            SetupDirecory();
            SnapshotDatabase();
            CreateSnapshotCookies(context);
            SaveUrl(context);
        }

        static bool IsSnapshotsDisabled => Config.Get<bool>("WebTestManager.DisableSnapshots");

        public bool Exists()
        {
            if (IsSnapshotsDisabled) return false;

            return SnapshotsDirectory.Exists();
        }

        public void Restore(HttpContext context)
        {
            if (!Exists())
                throw new DirectoryNotFoundException("Cannot find snapshot " + SnapshotName);

            var restoreDatabase = LocalTime.Now;
            RestoreDatabase();
            Debug.WriteLine("Total time for restoring including mutex: " + LocalTime.Now.Subtract(restoreDatabase).Milliseconds);

            var restoreCookies = LocalTime.Now;
            RestoreCookies(context);
            Debug.WriteLine("Total time for restoring cookies: " + LocalTime.Now.Subtract(restoreCookies).Milliseconds);

            var restoreUrl = LocalTime.Now;
            RestoreUrl(context);
            Debug.WriteLine("Total time for restoring url: " + LocalTime.Now.Subtract(restoreUrl).Milliseconds);
        }

        public static void RemoveSnapshots()
        {
            var sharedSnapshots = GetSnapshotsRoot(isSharedSnapshotMode: true);
            if (sharedSnapshots.Exists)
            {
                DeleteDirectory(sharedSnapshots);
                sharedSnapshots.EnsureExists();
            }

            var normalSnapshots = GetSnapshotsRoot(isSharedSnapshotMode: false);
            if (normalSnapshots.Exists)
            {
                DeleteDirectory(normalSnapshots);
                normalSnapshots.EnsureExists();
            }

            HttpContext.Current.Response.Redirect("~/");
        }

        public static void RemoveSnapshot(string name)
        {
            var snapshotName = CreateSnapshotName(name);

            var normalSnapshotDirectory = Path.Combine(GetSnapshotsRoot(isSharedSnapshotMode: false).FullName, snapshotName).AsDirectory();
            if (normalSnapshotDirectory.Exists)
                DeleteDirectory(normalSnapshotDirectory);

            var shardSnapshotDirectory = Path.Combine(GetSnapshotsRoot(isSharedSnapshotMode: true).FullName, snapshotName).AsDirectory();
            if (shardSnapshotDirectory.Exists)
                DeleteDirectory(shardSnapshotDirectory);

            HttpContext.Current.Response.Redirect("~/");
        }

        public static void DeleteDirectory(DirectoryInfo targetDirectory)
        {
            var files = targetDirectory.GetFiles();
            var dirs = targetDirectory.GetDirectories();

            foreach (var file in files)
            {
                file.Attributes = FileAttributes.Normal;
                file.Delete();
            }

            foreach (var dir in dirs)
            {
                DeleteDirectory(dir);
            }

            targetDirectory.Delete();
        }

        #region URL

        void SaveUrl(HttpContext context)
        {
            var url = HttpContext.Current.Request.Url.PathAndQuery;

            url = url.Substring(0, url.IndexOf("Web.Test.Command", StringComparison.OrdinalIgnoreCase) - 1);
            if (url.HasValue())
            {
                File.WriteAllText(SnapshotsDirectory.GetFile(URL_FILE_NAME).FullName, url);
                context.Response.Redirect(context.Request["url"]);
            }
        }

        private void RestoreUrl(HttpContext context)
        {
            var urlFile = SnapshotsDirectory.GetFile(URL_FILE_NAME);
            if (urlFile.Exists())
            {
                context.Response.Redirect(context.Request.Url.GetWebsiteRoot() + urlFile.ReadAllText().TrimStart("/"));
            }
        }
        #endregion

        #region Cookie
        void CreateSnapshotCookies(HttpContext context)
        {
            var json = JsonConvert.SerializeObject(
                    context.Request.GetCookies().Select(CookieStore.FromHttpCookie));

            GetCookiesFile().WriteAllText(json);
        }

        private void RestoreCookies(HttpContext context)
        {
            var cookiesFile = GetCookiesFile();

            if (!cookiesFile.Exists()) return;

            var cookies = JsonConvert.DeserializeObject<CookieStore[]>(cookiesFile.ReadAllText());

            context.Response.Cookies.Clear();
            foreach (var cookie in cookies)
            {
                context.Response.Cookies.Add(cookie.ToHttpCookie());
            }
        }

        FileInfo GetCookiesFile() => SnapshotsDirectory.GetFile("cookies.json");

        class CookieStore
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public bool Secure { get; set; }
            public bool HttpOnly { get; set; }
            public string Domain { get; set; }
            public DateTime Expires { get; set; }
            public string Value { get; set; }

            public static CookieStore FromHttpCookie(HttpCookie cookie)
            {
                return new CookieStore
                {
                    Name = cookie.Name,
                    Path = cookie.Path,
                    Secure = cookie.Secure,
                    HttpOnly = cookie.HttpOnly,
                    Domain = cookie.Domain,
                    Expires = cookie.Expires,
                    Value = cookie.Value
                };
            }

            public HttpCookie ToHttpCookie()
            {
                return new HttpCookie(this.Name, this.Value)
                {
                    Path = this.Path,
                    Secure = this.Secure,
                    HttpOnly = this.HttpOnly,
                    Domain = this.Domain,
                    Expires = this.Expires
                };
            }
        }

        #endregion

        #region DB
        private void SnapshotDatabase()
        {
            FileInfo[] files;

            SqlConnection.ClearAllPools();

            using (var connection = new SqlConnection(GetMasterConnectionString()))
            {
                connection.Open();
                files = GetPhysicalFiles(connection);

                TakeDatabaseOffline(connection);
                files.Do(f =>
                {
                    if (IsInShareSnapshotMode)
                    {
                        f.CopyTo(Path.Combine(SnapshotsDirectory.FullName, GetSnapshotFileName(f) + f.Extension));

                        // keep the snashptname of the database in a .origin file
                        File.WriteAllText(SnapshotsDirectory.GetFile(
                            GetSnapshotFileName(f) + f.Extension + ".origin").FullName,
                            f.FullName.ReplaceAll(DatabaseName, GetSnapshotFileName(f)));
                    }
                    else
                    {
                        f.CopyTo(SnapshotsDirectory);
                        // keep the original location of the database file in a .origin file
                        File.WriteAllText(SnapshotsDirectory.GetFile(f.Name + ".origin").FullName, f.FullName);
                    }
                });
                TakeDatabaseOnline(connection);
            }
        }

        string GetSnapshotFileName(FileInfo file)
        {
            return file.Name.Split('.').First() + ".Temp";

        }


        // TODO: create a connection string for MASTER
        void RestoreDatabase()
        {
            SnapshotRestoreLock = new Mutex(false, "SnapshotRestore");
            bool lockTaken = false;

            try
            {
                lockTaken = SnapshotRestoreLock.WaitOne();
                var restoreTime = LocalTime.Now;
                using (var connection = new SqlConnection(GetMasterConnectionString()))
                {
                    connection.Open();
                    var detachTime = LocalTime.Now;
                    DetachDatabase(connection);

                    Debug.WriteLine("Total time for detaching database: " + LocalTime.Now.Subtract(detachTime).Milliseconds);

                    FileInfo mdfFile = null, ldfFile = null;

                    var copyTime = LocalTime.Now;
                    // copy each database file to its old place
                    foreach (var originFile in SnapshotsDirectory.GetFiles("*.origin"))
                    {
                        originFile.IsReadOnly = true;

                        var destination = File.ReadAllText(originFile.FullName);
                        var source = originFile.FullName.TrimEnd(originFile.Extension).AsFile();

                        if (IsInShareSnapshotMode)
                        {
                            destination = destination.ReplaceAll(GetSnapshotFileName(originFile), DatabaseName);
                        }

                        if (destination.ToLower().EndsWith(".mdf"))
                            mdfFile = destination.AsFile();

                        if (destination.ToLower().EndsWith(".ldf"))
                            ldfFile = destination.AsFile();

                        source.CopyTo(destination, overwrite: true);
                        // shall we backup the existing one and in case of any error restore it?
                    }

                    Debug.WriteLine("Total time for copying database: " + LocalTime.Now.Subtract(copyTime).Milliseconds);

                    if (mdfFile == null)
                        throw new Exception("Cannot find any MDF file in snapshot directory " + SnapshotsDirectory.FullName);

                    if (ldfFile == null)
                        throw new Exception("Cannot find any LDF file in snapshot directory " + SnapshotsDirectory.FullName);
                    var attachTime = LocalTime.Now;
                    AttachDatabase(connection, mdfFile, ldfFile);
                    Debug.WriteLine("Total time for attaching database: " + LocalTime.Now.Subtract(attachTime).Milliseconds);
                    Database.Refresh();
                }
                Debug.WriteLine("Total time for restoreing database: " + LocalTime.Now.Subtract(restoreTime).Milliseconds);
            }
            finally
            {
                if (lockTaken == true)
                {
                    SnapshotRestoreLock.ReleaseMutex();
                }
            }
        }

        private void DetachDatabase(SqlConnection connection)
        {
            SqlConnection.ClearAllPools();

            using (var cmd = new SqlCommand(
                "USE Master; ALTER DATABASE [{0}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; ALTER DATABASE [{0}] SET MULTI_USER; exec sp_detach_db '{0}'"
                .FormatWith(DatabaseName), connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        void AttachDatabase(SqlConnection connection, FileInfo mdfFile, FileInfo ldfFile)
        {
            using (var cmd = new SqlCommand(
                "USE Master; CREATE DATABASE [{0}] ON (FILENAME = '{1}'), (FILENAME = '{2}') FOR ATTACH"
                .FormatWith(DatabaseName, mdfFile.FullName, ldfFile.FullName), connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        void TakeDatabaseOffline(SqlConnection connection)
        {
            SqlConnection.ClearAllPools();

            using (var cmd = new SqlCommand(
                "USE Master; ALTER DATABASE [{0}] SET OFFLINE WITH ROLLBACK IMMEDIATE;"
                .FormatWith(DatabaseName), connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        void TakeDatabaseOnline(SqlConnection connection)
        {
            using (var cmd = new SqlCommand(
                "USE Master; ALTER DATABASE [{0}] SET ONLINE;"
                .FormatWith(DatabaseName), connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        FileInfo[] GetPhysicalFiles(SqlConnection connection)
        {
            var files = new List<FileInfo>();

            using (var cmd = new SqlCommand(
                "USE Master; SELECT physical_name FROM sys.master_files where database_id = DB_ID('{0}')"
                .FormatWith(DatabaseName), connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    files.Add(Convert.ToString(reader[0]).AsFile());
                }
            }

            if (files.Count == 0)
                throw new Exception("Cannot find physical file name for database: " + DatabaseName);

            return files.ToArray();
        }
        #endregion

        void SetupDirecory()
        {
            // make sure it is empty
            if (SnapshotsDirectory.Exists())
            {
                SnapshotsDirectory.Delete(recursive: true);
            }

            SnapshotsDirectory.Create();
        }

        /// <summary>
        /// Gets the list of current snapshots on disk.
        /// </summary>
        public static List<string> GetList(bool isSharedSnapshotMode)
        {
            if (!GetSnapshotsRoot(isSharedSnapshotMode).Exists()) return null;

            return GetSnapshotsRoot(isSharedSnapshotMode).GetDirectories().Select(f => f.Name.Substring(0, f.Name.LastIndexOf('_'))).ToList();
        }

        static DirectoryInfo GetSnapshotsRoot(bool isSharedSnapshotMode)
        {
            if (isSharedSnapshotMode)
            {
                return Path.Combine(Config.Get(TEMP_DATABASES_LOCATION_KEY), DatabaseName.Split('.').First() + " SNAPSHOTS").AsDirectory();
            }
            else
            {
                return Path.Combine(Config.Get(TEMP_DATABASES_LOCATION_KEY), DatabaseName, "SNAPSHOTS").AsDirectory();
            }
        }

        static string GetMasterConnectionString()
        {
            var builder = new SqlConnectionStringBuilder(Config.GetConnectionString("AppDatabase"))
            {
                InitialCatalog = "master"
            };

            return builder.ToString();
        }

        static string GetDatabaseName()
        {
            return new SqlConnectionStringBuilder(Config.GetConnectionString("AppDatabase"))
                .InitialCatalog
                .Or("")
                .TrimStart("[")
                .TrimEnd("]");
        }


        static string CreateSnapshotName(string name)
        {
            var schemaHash = new TestDatabaseGenerator(false, false).GetCurrentDatabaseCreationHash();
            return "{0}_{1}".FormatWith(name, schemaHash).Except(Path.GetInvalidFileNameChars()).ToString("");
        }
    }
}