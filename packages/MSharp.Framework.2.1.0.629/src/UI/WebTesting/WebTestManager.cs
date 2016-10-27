namespace MSharp.Framework.UI
{
    using System;
    using System.Linq;
    using System.Data.SqlClient;
    using System.Text;
    using System.Threading;
    using System.Web;
    using MSharp.Framework.Services;
    using MSharp.Framework.Services.Testing;
    using Newtonsoft.Json;

    public class WebTestManager
    {
        internal static bool IsDatabaseBeingCreated;
        internal static bool? TempDatabaseInitiated;

        internal static void AwaitReadiness()
        {
            while (IsDatabaseBeingCreated) Thread.Sleep(100); // Wait until it's done.
        }

        public static string CurrentRunner { get; set; }

        static bool? _IsTddExecutionMode;

        /// <summary>
        /// Determines whether the application is running under Temp database mode.
        /// </summary>
        public static bool IsTddExecutionMode()
        {
            if (_IsTddExecutionMode.HasValue) return _IsTddExecutionMode.Value;

            var db = Config.GetConnectionString("AppDatabase").Get(c =>
                new SqlConnectionStringBuilder(c).InitialCatalog);

            db = db.Or("").ToLower().TrimStart("[").TrimEnd("]");

            _IsTddExecutionMode = db.EndsWith(".temp");

            return _IsTddExecutionMode.Value;
        }

        internal static void InitiateTempDatabase(bool enforceRestart, bool mustRenew)
        {
            if (!IsTddExecutionMode()) return;

            IsDatabaseBeingCreated = true;

            try
            {
                SqlConnection.ClearAllPools();

                AutomatedTask.DeleteExecutionStatusHistory();

                if (enforceRestart)
                    TempDatabaseInitiated = null;

                if (TempDatabaseInitiated.HasValue) return;

                var generator = new TestDatabaseGenerator(isTempDatabaseOptional: true, mustRenew: mustRenew);

                TempDatabaseInitiated = generator.Process();

                Database.Refresh();

                SqlConnection.ClearAllPools();

                // new Action(() => Database.Find<IEmailQueueItem>()).Invoke(retries: 20, waitBeforeRetries: TimeSpan.FromSeconds(1));
            }
            finally
            {
                IsDatabaseBeingCreated = false;
            }
        }

        public static void ProcessCommand(string command)
        {
            if (command.IsEmpty()) return;

            if (!IsTddExecutionMode()) throw new Exception("Invalid command in non TDD mode.");

            var request = HttpContext.Current.Request;
            var response = HttpContext.Current.Response;

            var isShared = request["mode"] == "shared";

            if (command == "snap")
            {
                new Snapshot(request["name"], isShared).Create(HttpContext.Current);
            }
            else if (command == "restore")
            {
                new Snapshot(request["name"], isShared).Restore(HttpContext.Current);
            }
            else if (command == "remove_snapshots")
            {
                Snapshot.RemoveSnapshots();
            }
            else if (command == "snapshots_list")
            {
                response.EndWith(JsonConvert.SerializeObject(Snapshot.GetList(isShared)));
            }
            else if (command == "snapExists")
            {
                if (new Snapshot(request["name"], isShared).Exists())
                {
                    response.EndWith("true");
                }
                else
                {
                    response.EndWith("false");
                }
            }
            else if (command.IsAnyOf("start", "run", "ran", "cancel", "restart"))
            {
                InitiateTempDatabase(enforceRestart: true, mustRenew: true);

                if (request.Has("runner")) CurrentRunner = request["runner"];
            }
            else if (command == "testEmail")
            {
                new EmailTestService(request, response).Process();
            }
            else if (command == "tasks")
            {
                DispatchTasksList();
            }
            else if (command == "setLocalDate")
            {
                var time = LocalTime.Now.TimeOfDay;
                if (request.Has("time")) time = TimeSpan.Parse(request["time"]);

                var date = LocalTime.Today;
                if (request.Has("date")) date = request["date"].To<DateTime>();

                date = date.Add(time);

                var trueOrigin = DateTime.Now;

                LocalTime.RedefineNow(() => { return date.Add(DateTime.Now.Subtract(trueOrigin)); });
                response.Clear();
                response.EndWith(date.ToString("yyyy-MM-dd @ HH:mm:ss"));
            }
            else if (command == "remove_snapshot")
            {
                Snapshot.RemoveSnapshot(request["name"]);
            }
            else if (command == "inject.service.response")
            {
                IntegrationTestInjector.Inject(request["service"], request["request"], request["response"]);
            }
        }

        /// <summary>
        /// To invoke this, send a request to /?web.test.command=tasks
        /// </summary>
        public static void DispatchTasksList()
        {
            var response = HttpContext.Current.Response;
            var request = HttpContext.Current.Request;

            response.ContentType = "text/html";

            response.Write("<html>");

            response.Write("<body>");

            if (request.Has("t"))
            {
                AutomatedTask.GetAllTasks().Single(t => t.Name == request["t"]).Execute();
                response.Write("Done: {0}<br/><br/>".FormatWith(request["t"]));
            }

            // Render a list of tasks
            response.Write(AutomatedTask.GetAllTasks().Select(t => "<a href='/?web.test.command=tasks&t={0}'>{0}</a>".FormatWith(t.Name)).ToString("<br/>") +
                "<br/><br/><a href='/Web.Test.Command=restart' style='display:none;'>Restart Temp Database</a>");

            response.Write("<script src='https://ajax.googleapis.com/ajax/libs/jquery/2.1.3/jquery.min.js'></script>");

            response.Write("</body>");

            response.Write("</html>");

            response.End();
        }

        internal static string GetSanityAdaptorScript()
        {
            var r = new StringBuilder();

            r.AppendLine("window.OpenBrowserWindow = function(url, target) { if (target && target != '_parent' && target != 'parent') target='_self'; window.open(url, target); }");

            r.AppendLine("$(function() { ");

            r.AppendLine("$(window).off('click.SanityAdapter').on('click.SanityAdapter', function(e) {");
            r.AppendLine("var link = $(e.target).filter('a').removeAttr('target'); } );");

            r.AppendLine("});");

            return r.ToString();
        }

        public static string GetWebTestWidgetHtml(HttpRequest request)
        {
            var url = request.Url.RemoveQueryString("Web.Test.Command").ToString();
            if (url.Contains("?")) url += "&"; else url += "?";

            return @"<div class='webtest-commands'
style='position: fixed; left: 49%; bottom: 0; margin-bottom: -96px; text-align: center; width: 130px; transition: margin-bottom 0.25s ease; background: #2ea8eb; color: #fff; font-size: 12px; font-family:Arial;'
onmouseover='this.style.marginBottom=""0""' onmouseout='this.style.marginBottom=""-96px""'
>
<div style='width: 100%; background-color:#1b648d; padding: 3px 0;font-size: 13px; font-weight: 700;'>Test...</div>
<div style='width: 100%; padding: 4px 0;'><a href='[URL]restart' style='color: #fff;'>Restart DB</a></div>
<div style='width: 100%; padding: 4px 0;'><a href='[URL]remove_snapshots' style='color: #fff;'>Kill DB Snapshots</a></div>
<div style='width: 100%; padding: 4px 0;'><a target='$modal' href='[URL]testEmail' style='color: #fff;'>Outbox...</a></div>
<div style='width: 100%; padding: 4px 0;'><a target='$modal' href='[URL]tasks' style='color: #fff;'>Tasks...</a></div>
</div>".Replace("[URL]", url + "Web.Test.Command=");
        }
    }
}
