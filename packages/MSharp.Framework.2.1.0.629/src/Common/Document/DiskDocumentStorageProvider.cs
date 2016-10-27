namespace MSharp.Framework
{
    using System;
    using System.IO;
    using System.Linq;

    class DiskDocumentStorageProvider : IDocumentStorageProvider
    {
        public void Save(Document document)
        {
            var fileDataToSave = document.FileData; // Because file data will be lost in delete.

            if (File.Exists(document.LocalPath))
            {
                lock (string.Intern(document.LocalPath))
                {
                    var data = File.ReadAllBytes(document.LocalPath);
                    if (data == null) Delete(document);
                    else if (data.SequenceEqual(document.FileData)) return; // Nothing changed.
                    else Delete(document);
                }
            }

            lock (string.Intern(document.LocalPath))
            {
                new Action(() => File.WriteAllBytes(document.LocalPath, fileDataToSave)).Invoke(retries: 6, waitBeforeRetries: TimeSpan.FromSeconds(0.5));
            }
        }

        public void Delete(Document document)
        {
            if (!Directory.Exists(document.LocalFolder)) Directory.CreateDirectory(document.LocalFolder);

            // Delete old file. TODO: Archive the files instead of deleting.
            foreach (var file in Directory.GetFiles(document.LocalFolder, document.GetFileNameWithoutExtension() + ".*"))
            {
                lock (string.Intern(file))
                {
                    new Action(() => File.Delete(file)).Invoke(retries: 6, waitBeforeRetries: TimeSpan.FromSeconds(0.5));
                }
            }
        }

        public byte[] Load(Document document)
        {
            lock (string.Intern(document.LocalPath))
            {
                if (File.Exists(document.LocalPath))
                    return File.ReadAllBytes(document.LocalPath);
            }

            // Look in fall-back paths for document file
            foreach (var fallbackPath in document.FallbackPaths)
            {
                lock (string.Intern(fallbackPath))
                {
                    if (File.Exists(fallbackPath))
                        return File.ReadAllBytes(fallbackPath);
                }
            }

            return new byte[0];
        }

        public bool FileExists(Document document)
        {
            if (document.LocalPath.HasValue() && File.Exists(document.LocalPath))
            {
                return true;
            }

            // Check for file in fall-back paths
            if (document.FallbackPaths.Any(File.Exists))
            {
                return true;
            }

            return false;
        }


    }
}