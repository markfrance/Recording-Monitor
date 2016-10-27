using System;
using System.Xml.Linq;

namespace MSharp.Framework.Services
{
    public class XmlProperties
    {
        XDocument Data = new XDocument();

        const string DocumentTemplate = @"<?xml version=""1.0"" encoding=""utf-8"" ?><Root>{0}</Root>";

        /// <summary>
        /// Creates a new XmlProperties instance.
        /// </summary>
        public XmlProperties(string xmlContent)
        {
            Data = XDocument.Parse(string.Format(DocumentTemplate, xmlContent));
        }

        /// <summary>
        /// Creates a new XmlProperties instance.
        /// </summary>
        public XmlProperties() : this("")
        {
        }

        public T Get<T>(string key)
        {
            if (Data.Root.GetElement(key) == null)
            {
                return default(T);
            }
            else
            {
                return Data.Root.GetValue<T>(key);
            }
        }

        public string Set(string key, object value)
        {
            var element = Data.Root.GetElement(key);

            if (value == null)
            {
                element?.Remove();
            }
            else
            {
                if (element == null)
                    Data.Root.Add(new XElement(key, value));
                else element.Value = $"{value}";
            }

            return ToString();
        }

        /// <summary>
        /// Returns the XML representation of this instance.
        /// </summary>
        public override string ToString()
        {
            return Data.Root.ToString().TrimStart("<Root>").TrimEnd("</Root>");
        }
    }
}
