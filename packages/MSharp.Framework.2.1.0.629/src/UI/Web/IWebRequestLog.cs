using System;

namespace MSharp.Framework.Services
{
    [LogEvents(false), CacheObjects(false)]
    public interface IWebRequestLog : IEntity
    {
        string IP { get; set; }
        double ProcessingTime { get; set; }
        string RequestType { get; set; }
        int ResponseLength { get; set; }
        string SearchKeywords { get; }
        string SessionId { get; set; }
        DateTime Start { get; set; }
        string Url { get; set; }
        string UrlReferer { get; set; }
        string UserAgent { get; set; }
    }

    public static class WebRequestLogExtensions
    {
        public static int CountSessionRequests(this IWebRequestLog request)
        {
            if (request.SessionId.IsEmpty()) return 1;
            return Database.Count<IWebRequestLog>(x => x.SessionId == request.SessionId);
        }

        public static string GetLastVisitedUrl(this IWebRequestLog request)
        {
            if (request.SessionId.IsEmpty()) return request.Url;

            return Database.GetList<IWebRequestLog>(x => x.SessionId == request.SessionId).WithMax(x => x.Start)?.Url;
        }

        public static TimeSpan GetDuration(this IWebRequestLog request)
        {
            return TimeSpan.FromMilliseconds(request.ProcessingTime);
        }

        public static bool IsBouncedBack(this IWebRequestLog request)
        {
            return request.CountSessionRequests() == 1;
        }
    }

}
