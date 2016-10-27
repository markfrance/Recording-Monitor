namespace MSharp.Framework.Services
{
    using System;
    using System.IO;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Serialization;

    /// <summary>
    /// Provides services for object XML serialization.
    /// </summary>
    public static class XmlSerialization
    {
        /// <summary>
        /// Generates an XML text equivalent of the specified object.
        /// </summary>
        public static string Serialize(object value, bool omitXmlDeclaration = false)
        {
            if (value == null)
            {
                return null;
            }

            var serializer = new XmlSerializer(value.GetType());

            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                OmitXmlDeclaration = omitXmlDeclaration
            };

            using (var textWriter = new StringWriterWithEncoding(Encoding.UTF8))
            {
                using (var xmlWriter = XmlWriter.Create(textWriter, settings))
                {
                    serializer.Serialize(xmlWriter, value);
                }

                return textWriter.ToString();
            }
        }

        /// <summary>
        /// Converts the specified xml text into an instance of T.
        /// </summary>
        public static T Deserialize<T>(string xml, bool omitXmlDeclaration = false, string rootElementName = "")
        {
            if (xml.IsEmpty())
            {
                return default(T);
            }

            xml = FixXmlFirstCharacterBug(xml);

            var serializer = new XmlSerializer(typeof(T));

            if (rootElementName.HasValue())
            {
                var root = new XmlRootAttribute
                {
                    ElementName = rootElementName,
                    IsNullable = true
                };

                serializer = new XmlSerializer(typeof(T), root);
            }

            using (var textReader = new StringReader(xml))
            {
                using (var xmlReader = XmlReader.Create(textReader, new XmlReaderSettings()))
                {
                    return (T)serializer.Deserialize(xmlReader);
                }
            }
        }

        static string RemoveNamespaces(this string xmlDocument)
        {
            return XElement.Parse(xmlDocument).RemoveNamespaces().ToString();
        }

        static string FixXmlFirstCharacterBug(string xml)
        {
            return xml == null ? null : xml.TrimBefore("<");
        }
    }
}