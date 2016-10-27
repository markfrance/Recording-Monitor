using System;
using System.Linq;
using System.Web;
using System.Xml.Linq;

namespace MSharp.Framework.Services.Rss
{
    public static class RssServer
    {
        /// <summary>
        /// Generates Rss Xml document for the specified items.
        /// </summary>
        public static string GenerateXml(RssChannel channel)
        {
            if (channel == null)
                throw new ArgumentNullException("channel");

            var document = new XDocument(new XElement("rss", new XAttribute("version", "2.0"),
                new XElement("channel",
                    new XElement("title", channel.Title),
                    new XElement("link", channel.Link),
                    new XElement("description", channel.Description),
                    new XElement("language", channel.Language),
                    new XElement("pubDate", channel.PubDate.ToUniversal().ToString("dd MMM yyyy hh:mm:ss GMT")),

                    from item in channel.Items
                    select CreateItem(item))));


            return @"<?xml version=""1.0""?>" + document;
        }

        static XElement CreateItem(IRssItem item)
        {
            return new XElement("item",
                new XElement("title", item.Title),
                new XElement("link", item.Link),
                new XElement("description", item.Description),
                new XElement("pubDate", item.PubDate),
                new XElement("guid", item.Guid));
        }

        /// <summary>
        /// Dispatches an Xml document in the current http response.
        /// </summary>
        public static void Dispatch(RssChannel channel)
        {
            var xml = GenerateXml(channel);

            var response = HttpContext.Current.Response;

            response.Clear();
            response.ContentType = "text/xml";
            response.Write(xml);
            response.Flush();
            response.End();
        }
    }
}
