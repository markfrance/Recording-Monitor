using System.Web;

namespace MSharp.Framework.UI
{

    public class LazyLoader : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            var data = Decode(context.Request.QueryString.ToString());
        }

        static string Decode(string data)
        {
            // TODO: Decode
            return data;
        }

        public bool IsReusable
        {
            get { return false; }
        }

    }
}
