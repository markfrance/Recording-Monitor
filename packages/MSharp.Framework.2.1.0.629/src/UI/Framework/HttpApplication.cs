namespace MSharp.Framework.UI
{
    using System;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Security.Principal;
    using MSharp.Framework.Services;

    public class HttpApplication : BaseHttpApplication
    {
        public HttpApplication()
        {
            ConfigureUserProvider();
        }

        /// <summary>
        /// Loads the IUser object from the specified ASP.NET user principal.
        /// </summary>
        public static Func<IPrincipal, IUser> LoadUser = userPrincipal =>
        {
            return UserServices.AuthenticationProvider.LoadUser(userPrincipal);
        };

        protected override void OnBeginRequest()
        {
            base.OnBeginRequest();

            if (ClearAppleAdapters())
                if (Request.UserAgent?.Contains("AppleWebKit") ?? false) Request.Browser.Adapters.Clear();

            var path = Request.GetRelativePath();

            if (path == "/KeepAlive.ashx") Response.EndWith("Kept alive");

            // Backwards compatibility:
            if (path == "/tasks.ashx" && WebTestManager.IsTddExecutionMode())
                WebTestManager.DispatchTasksList();

            ProcessPagesFolderPrefix(path);
        }

        void ProcessPagesFolderPrefix(string path)
        {
            var hasIllegalCharacters = Path.GetInvalidPathChars().Intersects(Request.FilePath);

            if (hasIllegalCharacters) return;

            if (!File.Exists(Request.PhysicalPath))
            {
                var extension = Path.GetExtension(Request.PhysicalPath)?.ToLower();

                if (extension == ".aspx" && Request.FilePath != "/Download.File.aspx")
                {
                    RewriteStdandardAspxPath(path, Request.QueryString);
                }
            }
        }

        void RewriteStdandardAspxPath(string path, NameValueCollection queryString)
        {
            if (!Context.Items.Contains("ORIGINAL.REQUEST.PATH"))
                Context.Items.Add("ORIGINAL.REQUEST.PATH", Request.Path);

            if (!path.ToLower().StartsWith("/pages/"))
            {
                path = Request.ApplicationPath.TrimEnd("/") + "/Pages" + path;

                if (queryString.HasKeys())
                    path += "?" + queryString;

                Context.RewritePath(path, rebaseClientPath: true);
            }
        }



        protected virtual bool ClearAppleAdapters()
        {
            return true;
        }

        [Obsolete("Instead of this, use IIS Always Running mode: https://www.google.co.uk/search?q=iis%20always%20running")]
        protected virtual bool ShouldKeepTheApplicationAlive() { return true; }

        protected override void OnAuthenticateRequest()
        {
            base.OnAuthenticateRequest();

            if (Request.FilePath == "/Download.File.aspx")
            {
                var path = Request.RawUrl.Substring("/Download.File.aspx?".Length);
                var user = LoadUser?.Invoke(Context?.User);
                new SecureFileDispatcher(path, user).Dispatch();
            }
        }

        protected virtual void ConfigureUserProvider()
        {
            var possibleContextTypes = AppDomain.CurrentDomain.GetAssemblies()
                   .Where(a => a.FullName.ToLower().Contains("app_code"))
                   .Select(a => a.GetType("App.Context")).ExceptNull().ToArray();

            if (possibleContextTypes.None())
                throw new Exception("There is no App.Context class available.");

            if (possibleContextTypes.Count() > 1)
                throw new Exception("There is more than one App.Context class available in this context! " + possibleContextTypes.Select(t => t.AssemblyQualifiedName).ToString(" | "));

            var userProperty = possibleContextTypes.Single().GetProperty("User", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy);
            if (userProperty == null) throw new Exception("App.Context type does not have a public static property named User");

            if (!userProperty.PropertyType.Implements<IUser>())
            {
                throw new Exception("App.Context.User property does not implement {0} interface.".FormatWith(typeof(IUser).FullName));
            }

            Page.GetCurrentUserMethod = () => (IUser)userProperty.GetValue(null);
        }

        protected override IPrincipal RetrieveActualUser(IPrincipal principal)
        {
            return User.Retrieve();
        }
    }
}