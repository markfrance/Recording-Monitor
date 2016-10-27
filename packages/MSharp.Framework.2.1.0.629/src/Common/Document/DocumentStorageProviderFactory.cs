using System;
using System.Collections.Generic;

namespace MSharp.Framework
{
    public class DocumentStorageProviderFactory
    {
        public static IDocumentStorageProvider DefaultProvider = new DiskDocumentStorageProvider();

        /// <summary>
        /// This is to be configured in Global.asax if a different provider is needed for specific files.
        /// Example: MSharp.Framework.DocumentStorageProviderFactory.Add("Customer.Logo", new MySpecialStorageProvider);
        /// </summary>
        public static Dictionary<string, IDocumentStorageProvider> Providers = new Dictionary<string, IDocumentStorageProvider>();

        /// <summary>
        /// In the format: {type}.{property} e.g. Customer.Logo.
        /// </summary>
        internal static IDocumentStorageProvider GetProvider(string folderName)
        {
            if (folderName.IsEmpty()) return DefaultProvider;

            return Providers.GetOrDefault(folderName) ?? DefaultProvider;
        }
    }
}
