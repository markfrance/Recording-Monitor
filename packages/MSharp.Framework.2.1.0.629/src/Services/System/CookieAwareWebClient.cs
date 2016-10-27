namespace MSharp.Framework.Services
{
    using System;
    using System.Net;

    public class CookieAwareWebClient : WebClient
    {
        public CookieAwareWebClient()
            : this(new CookieContainer())
        { }

        public CookieAwareWebClient(CookieContainer container)
        {
            this.CookieContainer = container;
        }

        public CookieContainer CookieContainer { get; set; }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);

            var castRequest = request as HttpWebRequest;
            if (castRequest != null)
            {
                castRequest.CookieContainer = this.CookieContainer;
            }

            return request;
        }
    }
}