namespace MSharp.Framework.Services.Authentication
{
    using System;
    using System.Web;
    using System.Web.Security;

    class FormsAuthenticationProvider : IAuthenticationProvider
    {
        public void LogOn(IUser user, string domain, bool remember, TimeSpan timeout)
        {
            var expireTime = DateTime.Now.Add(timeout);
            if (remember) expireTime = DateTime.Now.AddMonths(1);

            var ticket = new FormsAuthenticationTicket(2, user.GetId().ToString(), DateTime.Now, expireTime, remember, "no-user-data", FormsAuthentication.FormsCookiePath);

            // Hash the cookie for transport over the wire
            var hash = FormsAuthentication.Encrypt(ticket);
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, hash) { Expires = expireTime, Secure = HttpContext.Current.Request.IsHttps(), HttpOnly = true };

            if (FormsAuthentication.CookieDomain.HasValue())
                cookie.Domain = FormsAuthentication.CookieDomain;

            if (domain.HasValue())
                cookie.Domain = domain;

            // Add the cookie to the list for outbound response
            HttpContext.Current.Response.Cookies.Add(cookie);

            // This might work better:

            //var authcookie = FormsAuthentication.GetAuthCookie(user.ID.ToString(), remember);
            //if (domain.HasValue())
            //    authcookie.Domain = domain;
            // HttpContext.Current.Response.AppendCookie(authcookie);

            HttpContext.Current.User = HttpContext.Current.User.Retrieve();
        }

        public void LogOff(IUser user)
        {
            LogOffFormsAuthentication();
        }

        internal static void LogOffFormsAuthentication()
        {
            FormsAuthentication.SignOut();

            var endCookie = new HttpCookie(FormsAuthentication.FormsCookieName, "")
            {
                Expires = DateTime.Now.AddYears(-1)
            };
            HttpContext.Current.Response.Cookies.Add(endCookie);
        }

        public void LoginBy(string provider)
        {
            throw new NotSupportedException("Legacy authentication provider does not support external login");
        }

        public IUser LoadUser(System.Security.Principal.IPrincipal principal)
        {
            var userId = ApplicationEventManager.GetCurrentUserId(principal);

            if (userId == null)
            {
                var cookieValue = CookieProperty.Get<string>(".ASPXAUTH", defaultValue: null);
                if (cookieValue.HasValue())
                    userId = FormsAuthentication.Decrypt(cookieValue).Name;
            }

            if (userId == null) return null;// throw new Exception("There is no current user logged in.");

            return Database.Get<IUser>(userId);
        }

        public void PreRequestHandler(string path)
        {
            // nothing
        }
    }
}
