using System;
using System.Security.Principal;

namespace MSharp.Framework.Services.Authentication
{
    public interface IAuthenticationProvider
    {
        void LogOn(IUser user, string domain, bool remember, TimeSpan timeout);
        void LogOff(IUser user);
        void LoginBy(string provider);

        IUser LoadUser(IPrincipal principal);
        void PreRequestHandler(string path);
    }
}
