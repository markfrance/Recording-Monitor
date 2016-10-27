using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MSharp.Framework
{
    /// <summary>
    /// Provides shortcut access to the value specified in web.config (or App.config) under AppSettings or ConnectionStrings.
    /// </summary>
    public class Config
    {
        /// <summary>
        /// Gets the connection string with the specified key.
        /// </summary>
        public static string GetConnectionString(string key)
        {
            return ConfigurationManager.ConnectionStrings[key]?.ConnectionString;
        }

        /// <summary>
        /// Gets the value configured in Web.Config (or App.config) under AppSettings.
        /// </summary>
        public static string Get(string key)
        {
            return Get(key, string.Empty);
        }

        /// <summary>
        /// Gets the value configured in Web.Config (or App.config) under AppSettings.
        /// If no value is found there, it will return the specified default value.
        /// </summary>
        public static string Get(string key, string defaultValue)
        {
            return ConfigurationManager.AppSettings[key].Or(defaultValue);
        }

        /// <summary>
        /// Reads the value configured in Web.Config (or App.config) under AppSettings.
        /// It will then convert it into the specified type.
        /// </summary>
        public static T Get<T>(string key)
        {
            return Get<T>(key, default(T));
        }

        /// <summary>
        /// Reads the value configured in Web.Config (or App.config) under AppSettings.
        /// It will then convert it into the specified type.
        /// If no value is found there, it will return the specified default value.
        /// </summary>
        public static T Get<T>(string key, T defaultValue)
        {
            var value = "[???]";
            try
            {
                value = Get(key, defaultValue.ToStringOrEmpty());

                if (value.IsEmpty()) return defaultValue;

                var type = typeof(T);

                return value.To<T>();
            }
            catch (Exception ex)
            {
                throw new Exception("Could not retrieve '{0}' config value for key '{1}' and value '{2}'.".FormatWith(typeof(T).FullName, key, value), ex);
            }
        }

        /// <summary>
        /// Reads the value configured in Web.Config (or App.config) under AppSettings.
        /// It will then try to convert it into the specified type.
        /// If no vale is found in AppSettings or the conversion fails, then it will return null, or the default value of the specified type T.
        /// </summary>
        public static T TryGet<T>(string key)
        {
            var value = Get(key);

            if (value.IsEmpty()) return default(T);

            var type = typeof(T);

            try
            {
                return (T)value.To(type);
            }
            catch
            {
                return default(T);
            }
        }

        /// <summary>
        /// Determines whether the specified key is defined in configuration file.
        /// </summary>
        public static bool IsDefined(string key)
        {
            return Get(key).HasValue();
        }

        /// <summary>
        /// Reads the app settings from a specified configuration file.
        /// </summary>
        public static Dictionary<string, string> ReadAppSettings(FileInfo configFile)
        {
            if (configFile == null) throw new ArgumentNullException("configFile");

            if (!configFile.Exists()) throw new ArgumentException("File does not exist: " + configFile.FullName);

            var result = new Dictionary<string, string>();

            var config = XDocument.Parse(configFile.ReadAllText());

            var appSettings = config.Root.Elements().SingleOrDefault(a => a.Name.LocalName == "appSettings");

            if (appSettings != null)
            {
                foreach (var setting in appSettings.Elements())
                {
                    var key = setting.GetValue<string>("@key");

                    if (result.ContainsKey(key))
                        throw new Exception("The key '{0}' is defined more than once in the application config file '{1}'.".FormatWith(key, configFile.FullName));

                    result.Add(key, setting.GetValue<string>("@value"));
                }
            }

            return result;
        }

    }
}