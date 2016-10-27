namespace MSharp.Framework
{
    using System;
    using System.ComponentModel;
    using System.Security.Principal;
    using System.Web;
    using System.Web.Configuration;
    using System.Web.Security;
    using MSharp.Framework.Services;
    using MSharp.Framework.Services.Authentication;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class UserServices
    {
        public static IAuthenticationProvider AuthenticationProvider;

        static UserServices()
        {
            var provider = Config.Get("Authentication.Provider");
            if (provider.HasValue())
            {
                AuthenticationProvider = (IAuthenticationProvider)Type.GetType(provider).CreateInstance();
            }
            else
            {
                AuthenticationProvider = new FormsAuthenticationProvider();
            }
        }



        public static void LogOn(this IUser user)
        {
            LogOn(user, domain: null, remember: false);
        }

        static void LogOn(this IUser user, string domain)
        {
            LogOn(user, domain, remember: false);
        }

        public static void LogOn(this IUser user, bool remember)
        {
            LogOn(user, null, remember);
        }

        static TimeSpan GetAuthenticationTimeout()
        {
            try
            {
                var authenticationSection = WebConfigurationManager.OpenWebConfiguration("/").GetSection("system.web/authentication") as AuthenticationSection;
                var result = authenticationSection.Forms.Timeout;

                if (result == TimeSpan.Zero)
                {
                    result = TimeSpan.FromMinutes(20);
                }

                return result;
            }
            catch
            {
                return TimeSpan.FromMinutes(20);
            }
        }

        public static IPrincipal Retrieve(this IPrincipal user)
        {
            if (user == null) return null;

            if (user.Identity.IsAuthenticated && user.Identity is FormsIdentity)
            {
                // Get Forms Identity From Current User
                var id = (FormsIdentity)user.Identity;
                // Get Forms Ticket From Identity object
                var ticket = id.Ticket;
                // Retrieve stored user-data (our roles from db)
                var userData = ticket.UserData;
                var roles = userData.Split(',');
                // Create a new Generic Principal Instance and assign to Current User
                return new GenericPrincipal(id, roles);
            }
            else return user;
        }

        static void LogOn(this IUser user, string domain, bool remember)
        {
            AuthenticationProvider.LogOn(user, domain, remember, GetAuthenticationTimeout());
        }

        public static void LogOff(this IUser user)
        {
            AuthenticationProvider.LogOff(user);

            HttpContext.Current.Session.Perform(s => s.Abandon());

            // In case of migration to Owin, the legacy cookie should be removed for users already logged in.
            FormsAuthenticationProvider.LogOffFormsAuthentication();
        }

        public static void LoginBy(string provider)
        {
            AuthenticationProvider.LoginBy(provider);
        }
    }
}