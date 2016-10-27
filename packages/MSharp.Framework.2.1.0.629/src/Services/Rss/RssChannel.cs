using System;
using System.Collections.Generic;

namespace MSharp.Framework.Services.Rss
{
    public class RssChannel
    {
        /// <summary>
        /// Creates a new RssChannel instance.
        /// </summary>
        public RssChannel()
        {
            Language = "en-us";
            PubDate = LocalTime.Now;
        }

        public string Title { get; set; }
        public string Link { get; set; }
        public string Description { get; set; }
        public string Language { get; set; }
        public DateTime PubDate { get; set; }

        public IEnumerable<IRssItem> Items { get; set; }
    }
}
