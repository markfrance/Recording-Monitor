namespace MSharp.Framework.Services
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.IO;
    using System.Linq;
    using MSharp.Framework.Data;

    public class TestDatabaseGenerator
    {
        const string TEMP_DATABASES_LOCATION_KEY = "Temp.Databases.Location";
        const string MSHARP_META_DIRECTORY_KEY = "M#.Meta.Location";

        static object SyncLock = new object();
        static object ProcessSyncLock = new object();

        readonly string ConnectionString;
        SqlServerManager MasterDatabaseAgent;
        string TempDatabaseName;
        string ReferenceDatabaseName;

        FileInfo ReferenceMDFFile, ReferenceLDFFile;
        DirectoryInfo TempBackupsRoot, ProjectTempRoot, MSharpMetaDirectory, CurrentHashDirectory;

        readonly bool IsTempDatabaseOptional;
        readonly bool MustRenew;



        /// <summary>
        /// Creates a new TestDatabaseGenerator instance.
        /// <param name="isTempDatabaseOptional">Determines whether use of the temp database is optional.
        /// When this class is used in a Unit Test project, then it must be set to false.
        /// For Website project, it must be set to true.</param>
        /// <param name="mustRenew">Specifies whether the temp database must be recreated on application start up even if it looks valid already.</param>
        /// </summary>
        public TestDatabaseGenerator(bool isTempDatabaseOptional, bool mustRenew)
        {
            ConnectionString = Config.GetConnectionString("AppDatabase");

            IsTempDatabaseOptional = isTempDatabaseOptional;

            MustRenew = mustRenew;
        }


        FileInfo[] GetCreateDbFiles()
        {
            if (MSharpMetaDirectory == null)
                LoadMSharpMetaDirectory();

            var tableScripts = MSharpMetaDirectory.GetSubDirectory("Current").GetSubDirectory("Tables").GetFilesOrEmpty("*.sql");

            var potentialSources = new List<FileInfo>();

            // Create tables:
            potentialSources.Add(MSharpMetaDirectory.GetSubDirectory("Current").GetFile("@Create.Database.sql"));
            potentialSources.AddRange(tableScripts.Except(x => x.Name.ToLower().EndsWithAny(".fk.sql", ".data.sql")));

            // Insert data:
            potentialSources.Add(MSharpMetaDirectory.GetSubDirectory("Current").GetFile("@Create.Database.Data.sql"));
            potentialSources.AddRange(tableScripts.Where(x => x.Name.ToLower().EndsWith(".data.sql")));
            potentialSources.AddRange(MSharpMetaDirectory.GetSubDirectory("Current").GetSubDirectory("Data").GetFilesOrEmpty("*.sql"));

            potentialSources.Add(MSharpMetaDirectory.GetSubDirectory("Manual", onlyWhenExists: false).GetFile("Customize.Database.sql"));

            // Add foreign keys
            potentialSources.Add(MSharpMetaDirectory.GetSubDirectory("Current").GetFile("@Create.Database.ForeignKeys.sql"));
            potentialSources.AddRange(tableScripts.Where(x => x.Name.ToLower().EndsWith(".fk.sql")));

            var sources = potentialSources.Where(f => f.Exists()).ToArray();

            if (sources.None())
                throw new Exception("No SQL creation script file was found. I checked:\r\n" + potentialSources.ToLinesString());

            return sources;
        }

        Dictionary<FileInfo, string> GetExecutableCreateDbScripts()
        {
            var sources = GetCreateDbFiles();

            var result = new Dictionary<FileInfo, string>();

            foreach (var file in sources)
            {
                var script = file.ReadAllText();

                // The first few lines contain #DATABASE.NAME# which should be replaced.
                script = script.ToLines().Select((line, index) =>
                {
                    if (index < 10)
                    {
                        return line
                            .Replace("#DATABASE.NAME#", ReferenceDatabaseName)
                            .Replace("#STORAGE.PATH#", CurrentHashDirectory.FullName);
                    }

                    return line;
                }).ToLinesString();

                if (file.Name.Lacks("Create.Database.sql", caseSensitive: false))
                {
                    script = "USE [" + ReferenceDatabaseName + "];\r\nGO\r\n" + script;
                }

                result.Add(file, script);
            }

            return result;
        }

        internal string GetCurrentDatabaseCreationHash()
        {
            var createScript = GetCreateDbFiles().Select(x => x.ReadAllText()).ToLinesString();

            return createScript.ToSimplifiedSHA1Hash();
        }

        void CreateDatabaseFromScripts()
        {
            MasterDatabaseAgent.DeleteDatabase(ReferenceDatabaseName);

            var newDatabaseAgent = MasterDatabaseAgent.CloneFor(ReferenceDatabaseName);

            foreach (var file in GetExecutableCreateDbScripts())
            {
                try
                {
                    MasterDatabaseAgent.ExecuteSql(file.Value);
                }
                catch (Exception ex)
                {
                    throw new Exception("Could not execute sql file '" + file.Key.FullName + "' becuase '" + ex.Message + "'", ex);
                }
            }
        }

        public void CloneReferenceDatabaseToTemp()
        {
            // Make sure if it exists in database already, it's deleted first.
            MasterDatabaseAgent.DeleteDatabase(TempDatabaseName);

            var directory = ProjectTempRoot.GetOrCreateSubDirectory("Current");

            var newMDFPath = directory.GetFile(TempDatabaseName + ".mdf");
            var newLDFPath = directory.GetFile(TempDatabaseName + "_log.ldf");

            try
            {
                ReferenceMDFFile.CopyTo(newMDFPath);
                ReferenceLDFFile.CopyTo(newLDFPath);
            }
            catch (IOException ex)
            {
                if (ex.InnerException != null && ex.InnerException is UnauthorizedAccessException)
                    throw new Exception("Consider setting the IIS Application Pool identity to LocalSystem.", ex);

                throw;
            }

            var script = "CREATE DATABASE [{0}] ON (FILENAME = '{1}'), (FILENAME = '{2}') FOR ATTACH"
                .FormatWith(TempDatabaseName, newMDFPath.FullName, newLDFPath.FullName);

            try
            {
                MasterDatabaseAgent.ExecuteSql(script);
            }
            catch (SqlException ex)
            {
                throw new Exception("Could not attach the database from file " + newMDFPath.FullName + "." + Environment.NewLine +
                "Hint: Ensure SQL instance service has access to the folder. E.g. 'Local Service' may not have access to '{0}'" +
                newMDFPath.Directory.FullName, ex);
            }
        }

        internal void TryAccessNewTempDatabase()
        {
            Exception error = null;
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    DataAccessor.ReadData("SELECT TABLE_NAME FROM [{0}].INFORMATION_SCHEMA.TABLES".FormatWith(TempDatabaseName));
                    return;
                }
                catch (Exception ex)
                {
                    SqlConnection.ClearAllPools();
                    error = ex;
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(0.5));
                }
            }

            throw new Exception("Could not access the new database:" + error.Message, error);
        }



        public bool Process()
        {
            if (ConnectionString.IsEmpty()) return false;

            var builder = new SqlConnectionStringBuilder(ConnectionString);
            TempDatabaseName = builder.InitialCatalog.Or("").TrimStart("[").TrimEnd("]");

            if (TempDatabaseName.IsEmpty())
            {
                // None of my business.
                return false;
            }
            else if (!TempDatabaseName.ToLower().EndsWith(".temp") && IsTempDatabaseOptional)
            {
                // Optional and irrelevant
                return false;
            }

            EnsurePermissions();

            builder.InitialCatalog = "master";

            MasterDatabaseAgent = new SqlServerManager(builder.ToString());

            LoadTempDatabaseLocation();
            LoadMSharpMetaDirectory();

            if (!IsTempDatabaseOptional)
            {
                if (!IsExplicitlyTempDatabase())
                {
                    throw new Exception("For unit tests project the database name must end in '.Temp'.");
                }
            }

            if (!IsExplicitlyTempDatabase())
            {
                // Not Temp mode:
                return false;
            }

            return DoProcess();
        }

        /// <summary>
        /// Ensures the right permissions are configured.
        /// </summary>
        private void EnsurePermissions()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent()?.Name;

            var error = "\r\n\r\nRecommended action: If using IIS, update the Application Pool (Advanced Settings) and set Identity to LocalSystem.";

            if (identity.IsEmpty())
            {
                error = "Current IIS process model Identity not found!" + error;
                throw new Exception(error);
            }
            else
            {
                error = "Current IIS process model Identity: " + identity + error;
            }

            if (identity.ContainsAny(new[] { "IIS APPPOOL", "LOCAL SERVICE", "NETWORK SERVICE" }))
            {
                error = "In TDD mode full system access is needed in order to create temporary database files." + error;
                throw new Exception(error);
            }
        }

        void LoadTempDatabaseLocation()
        {
            var specifiedLocation = Config.Get(TEMP_DATABASES_LOCATION_KEY);

            if (specifiedLocation.IsEmpty())
            {
                throw new Exception("You must specify a valid path for AppSetting of '{0}'.".FormatWith(TEMP_DATABASES_LOCATION_KEY));
            }

            if (!specifiedLocation.AsDirectory().Exists())
            {
                // Try to build once:
                try
                {
                    Directory.CreateDirectory(specifiedLocation);
                }
                catch
                {
                    throw new Exception("Could not create the folder '{0}'. Ensure it exists and is accessible. Otherwise specify a different location in AppSetting of '{1}'."
                        .FormatWith(specifiedLocation, TEMP_DATABASES_LOCATION_KEY));
                }
            }

            TempBackupsRoot = specifiedLocation.AsDirectory();
            ProjectTempRoot = TempBackupsRoot.GetOrCreateSubDirectory(TempDatabaseName);
        }

        void LoadMSharpMetaDirectory()
        {
            // Explicitly specified?
            var specified = Config.Get(MSHARP_META_DIRECTORY_KEY);

            if (specified.HasValue())
            {
                if (!specified.AsDirectory().Exists())
                {
                    var error = "The path '{0}' does not exist. Specify the correct path of @M# folder via AppSetting of '{1}'.".FormatWith(specified, MSHARP_META_DIRECTORY_KEY);
                    error += "Maybe the current context user ({0}) does not have access to this network path?".FormatWith(Environment.UserName);
                    throw new Exception(error);
                }
                else
                {
                    // Already exists:
                    MSharpMetaDirectory = specified.AsDirectory();
                    return;
                }
            }
            else
            {
                // Not explicitly specified. Take a guess:

                var options = new[] { "../@M#", "../../@M#", "../../../@M#" };
                foreach (var option in options)
                {
                    var folder = AppDomain.CurrentDomain.GetPath(option).AsDirectory();

                    if (folder.Exists())
                    {
                        MSharpMetaDirectory = folder;
                        return;
                    }
                }

                throw new Exception("Please specify the path of @M# folder via AppSetting of '{0}'.".FormatWith(MSHARP_META_DIRECTORY_KEY));
            }
        }

        bool DoProcess()
        {
            var hash = GetCurrentDatabaseCreationHash().Replace("/", "-").Replace("\\", "-");

            lock (SyncLock)
            {
                ReferenceDatabaseName = TempDatabaseName + ".Ref";

                CurrentHashDirectory = ProjectTempRoot.GetOrCreateSubDirectory(hash);
                ReferenceMDFFile = CurrentHashDirectory.GetFile(ReferenceDatabaseName + ".mdf");
                ReferenceLDFFile = CurrentHashDirectory.GetFile(ReferenceDatabaseName + "_log.ldf");

                lock (ProcessSyncLock)
                {
                    var createdNewReference = CreateReferenceDatabase();

                    var tempDatabaseDoesntExist = !MasterDatabaseAgent.DatabaseExists(TempDatabaseName);

                    if (MustRenew || createdNewReference || tempDatabaseDoesntExist)
                    {
                        RefreshTempDataWorld();
                    }
                }

                return true;
            }
        }

        void RefreshTempDataWorld()
        {
            CloneReferenceDatabaseToTemp();

            SqlConnection.ClearAllPools();

            CopyFiles();

            // Do we really need this?
            TryAccessNewTempDatabase();

            //NextGuidNumbers = new Dictionary<Type, int>();
            //GuidEntity.NewGuidGenerator = GetNextGuid;
        }

        bool CreateReferenceDatabase()
        {
            if (ReferenceMDFFile.Exists() && ReferenceLDFFile.Exists())
            {
                return false;
            }

            var error = false;

            // create database + data
            try
            {
                CreateDatabaseFromScripts();
            }
            catch
            {
                error = true;
                throw;
            }
            finally
            {
                // Detach it
                MasterDatabaseAgent.DetachDatabase(ReferenceDatabaseName);

                if (error)
                {
                    ReferenceMDFFile.Delete(harshly: true);
                    ReferenceLDFFile.Delete(harshly: true);
                }
            }

            return true;
        }

        private bool IsExplicitlyTempDatabase()
        {
            return TempDatabaseName.ToLower().EndsWith(".temp");
        }

        public void CleanUp()
        {
            MasterDatabaseAgent.DeleteDatabase(TempDatabaseName);
        }

        void CopyFiles()
        {
            var copyActions = new List<Action>();

            foreach (
                var key in
                    new[]
                        {
                            Tuple.Create("Test.Files.Origin", "UploadFolder"),
                            Tuple.Create("Test.Files.Origin.Secure", "UploadFolder.Secure")
                        })
            {
                var source = Config.Get(key.Item1);
                if (source.IsEmpty()) continue;
                else source = AppDomain.CurrentDomain.GetPath(source);
                if (!Directory.Exists(source) || source.AsDirectory().GetDirectories().None())
                {
                    // No files to copy
                    continue;
                }

                var destination = Config.Get(key.Item2);
                if (destination.IsEmpty())
                    throw new Exception("Destination directory not configured in App.Config for key: " + key.Item2);
                else destination = AppDomain.CurrentDomain.GetPath(destination);

                if (!Directory.Exists(destination))
                {
                    if (new DirectoryInfo(source).IsEmpty()) continue;

                    Directory.CreateDirectory(destination);
                }

                new DirectoryInfo(destination).Clear();

                copyActions.Add(delegate { new DirectoryInfo(source).CopyTo(destination, overwrite: true); });
            }

            copyActions.Do(a => a?.Invoke());
        }

        // #region Deterministic GUID

        // private const int GUID_BASE_NUMBER = 99999;
        // private static Dictionary<Type, int> NextGuidNumbers = new Dictionary<Type, int>();

        //// private static object guidSyncLock = new object();

        // //public static Guid GetNextGuid(Type type)
        // //{
        // //    lock (guidSyncLock)
        // //    {
        // //        int nextNumber;
        // //        if (NextGuidNumbers.ContainsKey(type))
        // //        {
        // //            nextNumber = NextGuidNumbers[type] + 1;
        // //            NextGuidNumbers[type] = nextNumber;
        // //        }
        // //        else
        // //        {
        // //            nextNumber = type.GetHashCode();
        // //            NextGuidNumbers.Add(type, nextNumber);
        // //        }

        // //        var bytes = new byte[16];
        // //        BitConverter.GetBytes(nextNumber).CopyTo(bytes, 0);
        // //        return new Guid(bytes);
        // //    }
        // //}

        // #endregion
    }
}