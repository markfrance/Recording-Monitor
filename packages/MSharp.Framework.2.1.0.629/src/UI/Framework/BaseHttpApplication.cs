namespace MSharp.Framework.UI
{
    using System;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Security.Principal;
    using System.Web;
    using MSharp.Framework.Services;

    public abstract class BaseHttpApplication : System.Web.HttpApplication
    {
        static bool AlreadyInitiated;
        protected BaseHttpApplication()
        {
            WebRequestLogService.Register(this);
            BeginRequest += (s, e) => OnBeginRequest();
            EndRequest += BaseHttpApplication_EndRequest;
            Error += (s, e) => OnError();
            AuthenticateRequest += (s, e) => OnAuthenticateRequest();
            PreRequestHandlerExecute += (s, e) => HandleAuthentication();
            InitiateApplication();
        }

        void BaseHttpApplication_EndRequest(object sender, EventArgs e)
        {
            if (Request.IsHttps())
                Response.Cookies.OfType<HttpCookie>().Except(x => x.Secure).Do(c => c.Secure = true);
        }

        static void InitiateApplication()
        {
            // The constructor is called more than once.
            if (AlreadyInitiated)
                return;
            else
                AlreadyInitiated = true;
            WebTestManager.InitiateTempDatabase(enforceRestart: false, mustRenew: false);
            LookForInsecureFiles();
        }

        static void LookForInsecureFiles()
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            if (root.AsDirectory().GetFiles("*.zip").Any())
            {
                var ip = HttpContext.Current?.Request.UserHostAddress;

                var error = new Exception("There is a zip file in the website root: " + root + " which is a security threat. Computer:" + Environment.MachineName + ". Please remove it ASAP.");

                if (ip == "127.0.0.1") throw error;

                try
                {
                    error.SendAsNotification(Config.Get("Error.Notification.Receiver").Or("info@geeks.ltd.uk"));
                }
                catch
                {
                    // No logging is needed
                }
            }
        }

        void HandleAuthentication()
        {
            UserServices.AuthenticationProvider.PreRequestHandler(Request.GetRelativePath());
        }

        protected virtual void OnBeginRequest()
        {
            WebTestManager.AwaitReadiness();
            if (Request["Web.Test.Command"] == "Sql.Profile")
            {
                var file = MSharp.Framework.Data.DataAccessProfiler.GenerateReport(Request["Mode"] == "Snapshot");
                Response.EndWith("Report generated: " + file.FullName);
            }
            else
                WebTestManager.ProcessCommand(Request["Web.Test.Command"]);
            ProcessInjectedFiles();
        }

        public override void Init()
        {
            base.Init();
            PreSendRequestHeaders += (s, e) =>
            {
                try
                {
                    Response.Headers.Remove("Server");
                    Response.Headers.Remove("X-Powered-By");
                    Response.Headers.Remove("X-AspNet-Version");
                }
                catch (PlatformNotSupportedException)
                {
                }
            }

            ;
        }

        protected virtual void OnError()
        {
            try
            {
                Response.Filter = null;
            }
            catch
            {
                // No logging is needed
            }

            var error = Server.GetLastError();
            try
            {
                LogError(error);
                SendErrorNotification(error);
                SqlConnection.ClearAllPools();
            }
            catch
            {
                // No logging is needed
            }
        }

        protected virtual IApplicationEvent LogError(Exception error)
        {
            if (Config.Get<bool>("Log.Unhandled.Errors", defaultValue: true))
                return Log.Error(error);
            return null;
        }

        protected virtual IEmailQueueItem SendErrorNotification(Exception error)
        {
            var toNotify = Config.Get("Error.Notification.Receiver");
            if (ShouldNotify(error))
                return error.SendAsNotification();
            return null;
        }

        /// <summary>
        /// Determines whether a notification for the specified error be emailed.
        /// </summary>
        protected virtual bool ShouldNotify(Exception error)
        {
            try
            {
                if (Request.UserLanguages == null || Request.UserLanguages.Length == 0)
                {
                    // It's perhaps a robot:
                    return false;
                }

                if (Request.Browser.ToStringOrEmpty().Contains("bot/"))
                    return false;
            }
            catch
            {
                // No logging is needed
            }

            return true;
        }

        void ProcessInjectedFiles()
        {
            if (!WebTestManager.IsTddExecutionMode())
                return;
            if (Request.Files == null || Request.Files.Count == 0)
                return;
            foreach (var key in Request.Form.AllKeys.Where(k => k.EndsWith("_InjectedContents")))
            {
                var fileKey = key.TrimEnd("_InjectedContents");
                Request.Files.InjectFile(fileKey, Convert.FromBase64String(Request.Form[key]), Request.Form[fileKey + "_InjectedFileName"], "application/octet-stream");
            }
        }

        public void Application_AcquireRequestState(object sender, EventArgs e)
        {
            if (Request["Web.Test.Command"].IsAnyOf("KillSessionAndCookies", "restart"))
            {
                try
                {
                    Session.Clear();
                    Request.Cookies.AllKeys.Do(c => Response.Cookies[c].Expires = DateTime.Now.AddDays(-1));
                }
                catch
                {
                    // Check why
                    // No logging is needed
                }
            }
        }

        protected virtual void OnAuthenticateRequest()
        {
            Context.User = RetrieveActualUser(User);
        }

        /// <summary>
        /// Retrieves the actual user implementation based on the basic ASP.NET principle info.
        /// </summary>
        protected abstract IPrincipal RetrieveActualUser(IPrincipal principal);
    }
}