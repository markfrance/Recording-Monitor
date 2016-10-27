using System;

namespace MSharp.Framework.Services.Rss
{
    public interface IRssItem
    {
        string Title { get; }
        string Link { get; }
        string Description { get; }
        DateTime PubDate { get; }
        string Guid { get; }
    }
}
