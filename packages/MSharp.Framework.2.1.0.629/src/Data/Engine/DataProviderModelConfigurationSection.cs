namespace MSharp.Framework.Data
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Reflection;
    using System.Xml;
    using System.Xml.Linq;

    public class DataProviderModelConfigurationSection : ConfigurationSection
    {
        private List<DataProviderFactoryInfo> _Providers;
        public IEnumerable<DataProviderFactoryInfo> Providers { get { return _Providers; } }

        protected override void DeserializeSection(System.Xml.XmlReader reader)
        {
            if (!reader.Read() || (reader.NodeType != XmlNodeType.Element))
            {
                throw new ConfigurationErrorsException("Configuration reader expected to find an element", reader);
            }

            DeserializeElement(reader, serializeCollectionKey: false);
        }

        protected override void DeserializeElement(System.Xml.XmlReader reader, bool serializeCollectionKey)
        {
            var xml = reader.ReadOuterXml();
            var root = XDocument.Parse(xml).Root?.Element("providers");
            if (root == null) return;

            _Providers = new List<DataProviderFactoryInfo>();

            foreach (var provider in root.Elements())
            {
                var assembly = provider.Attribute("assembly").Value;
                var type = provider.Attribute("type")?.Value;
                var providerFactoryType = provider.Attribute("providerFactoryType").Value;
                var mappingResource = provider.Attribute("mappingResource");
                //if (mappingResource == null)
                //    throw new Exception("The data access provider configuration needs to specify the mappingResource attribute.");

                var mappingDirectory = GetMappingDirectory(provider.Attribute("mappingDirectory"));

                var connectionStringKey = string.Empty;
                if (provider.Attribute("connectionStringKey") != null)
                    connectionStringKey = provider.Attribute("connectionStringKey").Value;

                var connectionString = string.Empty;
                if (provider.Attribute("connectionString") != null)
                    connectionString = provider.Attribute("connectionString").Value;

                var providerItem = new DataProviderFactoryInfo
                {
                    AssemblyName = assembly,
                    TypeName = type,
                    MappingResource = mappingResource?.Value,
                    ProviderFactoryType = providerFactoryType,
                    MappingDirectory = mappingDirectory,
                    ConnectionStringKey = connectionStringKey,
                    ConnectionString = connectionString
                };

                _Providers.Add(providerItem);
            }

            // Read sync file:
            var syncAttribute = root.Attribute("syncFilePath");
            if (syncAttribute != null)
                SyncFilePath = syncAttribute.Value;

            // Read file dependancy:
            var fileDependancyPathAttribute = root.Attribute("fileDependancyPath");
            if (fileDependancyPathAttribute != null)
                FileDependancyPath = fileDependancyPathAttribute.Value;
        }

        private string GetMappingDirectory(XAttribute setting)
        {
            if (setting == null || setting.Value.IsEmpty())
                return string.Empty;

            var result = setting.Value;

            if (result.StartsWith("\\\\") || result.Contains(":"))
            {
                // Absolute path:
                return result;
            }

            result = AppDomain.CurrentDomain.BaseDirectory + "/" + result + "/";
            result = result.Replace("/", "\\");

            while (result.Contains(@"\\"))
                result = result.Replace(@"\\", @"\");

            return result;
        }

        #region SyncFilePath
        /// <summary>
        /// Gets or sets the SyncFilePath of this DataProviderModelConfigurationSection.
        /// </summary>
        public string SyncFilePath { get; set; }
        #endregion

        #region FileDependancyPath
        /// <summary>
        /// Gets or sets the SyncFilePath of this DataProviderModelConfigurationSection.
        /// </summary>
        public string FileDependancyPath { get; set; }
        #endregion

    }

}