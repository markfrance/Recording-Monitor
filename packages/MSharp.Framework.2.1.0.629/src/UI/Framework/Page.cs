namespace MSharp.Framework.UI
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web;
    using System.Web.Security;
    using System.Web.UI;
    using System.Web.UI.WebControls;
    using Data;
    using MSharp.Framework.Services;
    using MSharp.Framework.Services.Globalization;

    /// <summary>
    /// Base Page containing base common functionality, all pages inherit from this.
    /// </summary>
    public class Page : global::System.Web.UI.Page
    {
        const string GUEST_ROLE = "Guest";
        static readonly bool InsertCommonResourcesEnabled = Config.Get<bool>("Pages.CommonResources.Enabled", defaultValue: true);
        static ConcurrentDictionary<string, string> PageUrlsCache = new ConcurrentDictionary<string, string>();

        Dictionary<string, ScriptInsertLocation> RegisteredScriptFiles = new Dictionary<string, ScriptInsertLocation>();
        Control StartupControl;

        public Page()
        {
            MaintainScrollPositionOnPostBack = true;
            AutoResetCss = true;
            MessageBox = new MessageBoxManager(this);
        }

        public bool AutoResetCss { get; set; }

        protected virtual bool AlwaysAllowLocalAccess() { return false; }

        IEnumerable<string> GetPermittedRoles(SiteMapNode page)
        {
            if (page.Roles.Count > 0) return page.Roles.Cast<string>().Trim();
            else if (page.ParentNode != null) return GetPermittedRoles(page.ParentNode);
            else return Enumerable.Empty<string>();
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            var postBackListControl = Page.GetPostBackControl() as ListControl;
            if (postBackListControl != null)
                SetFocus(postBackListControl);

            if (ShouldGZip())
                Context.SupportGZip();
        }

        /// <summary>
        /// Sets startup focus on a specified control.
        /// </summary>
        public void Focus(Control control)
        {
            StartupControl = control;
        }

        protected virtual void EnforceSecurity()
        {
            if (AlwaysAllowLocalAccess() && Request.IsLocal) return;

            if (SiteMap.CurrentNode != null)
            {
                var permittedRoles = GetPermittedRoles(SiteMap.CurrentNode);

                if (permittedRoles.None() || permittedRoles.Contains(GUEST_ROLE))
                {
                    // No Security:
                    return;
                }

                if (GetCurrentUserMethod == null)
                {
                    throw new Exception("{0}.GetUserMethod delegate must be set to App.Context.User method (or something similar).".FormatWith(GetType().FullName));
                }

                var user = GetCurrentUserMethod?.Invoke();
                if (user == null || permittedRoles.None(role => user.IsInRole(role)))
                {
                    RedirectToLoginPage();
                }
            }
        }

        /// <summary>
        /// Redirects the user to the Login Page.
        /// </summary>
        protected virtual void RedirectToLoginPage()
        {
            if (!Page.IsCallback)
            {
                var loginUrl = (this.Master as MasterPage)?.GetLogInUrl();
                loginUrl = loginUrl.Or(Config.Get("Access.Denied.Page.Url"));

                if (loginUrl.HasValue())
                {
                    if (loginUrl.Lacks("ReturnUrl"))
                    {
                        loginUrl += (loginUrl.Contains("?") ? "&" : "?") + "ReturnUrl=" + Request.RawUrl.UrlEncode();
                    }

                    Response.Redirect(loginUrl);
                }
                else
                {
                    FormsAuthentication.RedirectToLoginPage();
                }
            }

            Response.End();
        }

        public static Func<IUser> GetCurrentUserMethod { get; set; }

        protected override void OnPreInit(EventArgs e)
        {
            if (Request != null && Request["ASP.NET.COMPILE.ONLY"] == "true")
            {
                Response.Write("Compiled successfully.");
                Response.End();
            }

            ProcessSSL();

            EnforceSecurity();

            if (StartupControl != null)
                ClientScript.RegisterStartupScript(GetType(), "Set startup focus", "$(document).ready(function(){$(" +
                    StartupControl.ForBrowser() + ").focus();});", addScriptTags: true);

            if (WebTestManager.CurrentRunner == "Sanity")
                ClientScript.RegisterStartupScript(GetType(), "Adapt for Sanity",
                    WebTestManager.GetSanityAdaptorScript(), addScriptTags: true);

            base.OnPreInit(e);
        }

        protected virtual IEnumerable<string> GetScriptFilesToReference()
        {
            var scripts = new[] { "Geeks.Scripts.js", "Reflection.js", "CollapsibleCheckBoxList.js", "Geeks.jQuery.Extensions.js" }
                .Select(s => GetVirtualPath("Content/" + s)).ToList();

            // JQuery UI:
            scripts.Insert(0, GetVirtualPath("Content/jquery-ui-{0}.custom.min.js".FormatWith(Config.Get("JQuery.UI.Version").Or("1.8.14"))));

            // jQuery:
            var jqueryVersion = Config.Get("JQuery.Version").Or(Config.Get("Application.UI.Page.JQuery.Version")).Or("1.9.1");
            if (Config.Get<bool>("Use.JQuery.From.CDN", defaultValue: false))
                scripts.Insert(0, "{0}://ajax.googleapis.com/ajax/libs/jquery/{1}/jquery.min.js".FormatWith(GetCurrentDefaultScheme(), jqueryVersion));
            else
                scripts.Insert(0, GetVirtualPath("Content/jquery-{0}.min.js".FormatWith(jqueryVersion)));

            return scripts;
        }


        protected virtual IEnumerable<string> GetCssFilesToReference()
        {
            if (AutoResetCss)
                yield return GetVirtualPath("Content/_Reset.css");

            yield return GetVirtualPath("Content/jquery-ui-{0}.custom.css".FormatWith(Config.Get("JQuery.UI.Version").Or("1.8.14")));
        }

        bool FileExists(string virtualPath)
        {
            try
            {
                return Server.MapPath(virtualPath).AsFile().Exists();
            }
            catch { return false; }
        }

        protected virtual void InsertCommonResources()
        {
            if (Header == null) return;

            // Add standard Scripts to the page:
            var scripts = GetScriptFilesToReference().ToArray();
            for (var i = 0; i < scripts.Length; i++)
            {
                if (FileExists(scripts[i]))
                {
                    Header.Controls.AddAt(i, "<script src=\"{0}\" type=\"text/javascript\"></script>".FormatWith(scripts[i]));
                }
            }

            var css = GetCssFilesToReference().ToArray();
            for (var i = 0; i < css.Length; i++)
            {
                if (FileExists(css[i]))
                {
                    Header.Controls.AddAt(i, "<link rel=\"stylesheet\" type=\"text/css\" href=\"{0}\" />".FormatWith(css[i]));
                }
            }

            if (Request.Browser.Browser == "IE")
            {
                if (Config.Get<bool>("Render.IE.Compatible.With.IE7", defaultValue: false))
                    Header.Controls.Add("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=7\" />");
            }
        }

        /// <summary>
        /// Gets the virtual path of a resource file.
        /// </summary>
        /// <param name="resourceLocation">The path of the resource from the root of the website. For example "Content/style.css"</param>
        protected string GetVirtualPath(string resourceLocation)
        {
            return Request.ApplicationPath.TrimEnd("/") + "/" + resourceLocation.TrimStart("/");
        }

        /// <summary>
        /// Determines the required scheme (http / https) for this page.
        /// </summary>
        protected virtual string RequiredScheme { get { return null; } }

        string GetCurrentDefaultScheme()
        {
            return RequiredScheme.Or(Config.Get("Default.Page.RequiredScheme")).Or("http");
        }

        protected virtual void ProcessSSL()
        {
            var url = Request.Url;

            var scheme = RequiredScheme.Or(Config.Get("Default.Page.RequiredScheme"));
            if (scheme.HasValue() && url.Scheme?.ToLower() != scheme.ToLower())
            {
                var redirectUrl = scheme + url.ToString().Substring(url.Scheme.Length);
                Response.Redirect(redirectUrl);
            }
        }

        /// <summary>
        /// Initializes the <see cref="T:System.Web.UI.HtmlTextWriter"/> object and calls on the child controls of the <see cref="T:System.Web.UI.Page"/> to render.
        /// </summary>
        /// <param name="writer">The <see cref="T:System.Web.UI.HtmlTextWriter"/> that receives the page content.</param>
        protected override void Render(HtmlTextWriter writer)
        {
            if (InsertCommonResourcesEnabled)
            {
                InsertCommonResources();
            }

            ReferenceRegisteredScripts();

            var originalPath = Context.Items["ORIGINAL.REQUEST.PATH"] as string;

            if (originalPath.HasValue())
                Context.RewritePath(originalPath);

            //  NeatCssTagManager.RenderPage(base.Render, writer);
            base.Render(writer);

            //if (HttpApplication.TempDatabaseInitiated == false) throw new Exception("FALSE!!");
            //if (HttpApplication.TempDatabaseInitiated == null) throw new Exception("NULL!!");


            if (WebTestManager.IsTddExecutionMode() &&
                Request.FilePath.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase) &&
                !IsCallback && !Request.IsAjaxCall())
            {
                writer.Write(WebTestManager.GetWebTestWidgetHtml(Request));
            }
        }

        public delegate void RaisePostBackEventHandler(IPostBackEventHandler source, string e);

        /// <summary>
        /// Occurs just before PostBackEvent is raised.
        /// </summary>
        public event RaisePostBackEventHandler PostBackEventRaising;

        /// <summary>
        /// Raises the post back event.
        /// </summary>
        protected override void RaisePostBackEvent(IPostBackEventHandler source, string e)
        {
            PostBackEventRaising?.Invoke(source, e);

            base.RaisePostBackEvent(source, e);
        }

        public MessageBoxManager MessageBox { get; private set; }

        /// <summary>
        /// This will redirect the user to the current Request.Url.
        /// </summary>
        public virtual void Refresh()
        {
            Response.Redirect(Request.RawUrl);
        }

        /// <summary>
        /// Will return the translation of the specified phrase in the language specified in user's cookie (or default language).
        /// </summary>
        public static string Translate(string phrase)
        {
            return Translator.Translate(phrase);
        }

        /// <summary>
        /// Will return the translation of the specified html block in the language specified in user's cookie (or default language).
        /// </summary>
        public static string TranslateHtml(string html)
        {
            return Translator.TranslateHtml(html);
        }

        /// <summary>
        /// Gets the URL to a page specified by its resource key (from Site Map).
        /// </summary>
        public virtual string PageUrl(string resourceKey)
        {
            return GetPageUrl(resourceKey);
        }



        /// <summary>
        /// Gets the URL to a page specified by its resource key (from Site Map).
        /// </summary>
        public static string GetPageUrl(string resourceKey)
        {
            return PageUrlsCache.GetOrAdd(resourceKey, key =>
            {
                var node = SiteMap.Provider.FindByKey(key);

                if (node == null)
                {
                    throw new ArgumentException("There is no page with the key of " + key + " in the site map.");
                }
                else if (node.Url.StartsWith("~/Pages/"))
                {
                    return "~/" + node.Url.TrimStart("~/Pages/");
                }
                else if (node.Url.StartsWith("/Pages/"))
                {
                    return "/" + node.Url.TrimStart("/Pages/");
                }
                else
                {
                    return node.Url;
                }
            });
        }

        #region Close Modal Normally

        /// <summary>
        /// Determines whether the current request is From inside an update panel.
        /// </summary>
        protected bool IsFromUpdatePanel()
        {
            if (!Page.IsPostBack) return false;
            if (Page.IsCallback) return true;
            if (Request.IsAjaxCall()) return true;

            var scriptManagerId = System.Web.UI.ScriptManager.GetCurrent(this)?.ID;

            return scriptManagerId.HasValue() && Request.Form.AllKeys.Contains(scriptManagerId);
        }

        #region CloseModal

        /// <summary>
        /// Closes the current modal window.
        /// </summary>
        public void CloseModal()
        {
            CloseModal(AfterClose.DoNothing, string.Empty);
        }

        /// <summary>
        /// Closes the current modal window.
        /// </summary>
        public void CloseModal(AfterClose action)
        {
            CloseModal(action, string.Empty);
        }

        /// <summary>
        /// Closes the current modal window.
        /// </summary>
        public void CloseModal(string url)
        {
            CloseModal(AfterClose.DoNothing, url);
        }

        /// <summary>
        /// Closes the current modal window.
        /// </summary>
        public virtual void CloseModal(AfterClose action = AfterClose.DoNothing, string url = "")
        {
            var script = GenerateCloseModalScript(action, url);

            if (IsFromUpdatePanel() || Database.AnyOpenTransaction()) // || Request.Browser.Browser == "Chrome"
            {
                // Inside update panel:
                ScriptManager.RegisterStartupScript(Page, GetType(), "Close Modal" + Guid.NewGuid(), script, addScriptTags: true);
            }
            else
            {
                Response.Clear();
                Response.Write(@"<html>
                                    <body>
                                        <script type='text/javascript'>
                                            {0}
                                        </script>
                                    </body>
                                </html>".FormatWith(script));
                Response.End();
            }
        }

        #endregion

        string GenerateCloseModalScript(AfterClose action, string url)
        {
            switch (action)
            {
                case AfterClose.RefreshParent: return "parent.CloseModal(true);";
                case AfterClose.RefreshParentFully: return "window.parent.location = window.parent.location;";
                case AfterClose.RedirectTo: return "parent.OpenBrowserWindow(\"{0}\",'_parent');".FormatWith(url);
                default: return "parent.CloseModal(false);";
            }
        }

        #endregion

        /// <summary>
        /// Gets the currently specified return URL.
        /// </summary>
        protected string GetReturnUrl()
        {
            return Request.GetReturnUrl();
        }

        /// <summary>
        /// http://support.microsoft.com/kb/316431
        /// This method is trying to solve the problem of downloading files in IE through secure access protocol (https)
        /// </summary>
        /// <param name="cacheSettings"></param>
        protected override void InitOutputCache(System.Web.UI.OutputCacheParameters cacheSettings)
        {
            if (Request.Browser.Browser.ToLower() == "ie")
            {
                cacheSettings.Location = System.Web.UI.OutputCacheLocation.Client;
                cacheSettings.Duration = 0;
                cacheSettings.VaryByParam = "*";
            }

            base.InitOutputCache(cacheSettings);
        }

        /// <summary>
        /// Determines whether GZip should be enabled on this page's response.
        /// </summary>
        protected bool ShouldGZip()
        {
            return Config.Get("GZip.Pages.Response", defaultValue: false);
        }

        #region Registered script files

        protected virtual void ReferenceRegisteredScripts()
        {
            if (Header != null)
            {
                foreach (var file in RegisteredScriptFiles.Where(x => x.Value == ScriptInsertLocation.HeaderTop).Reverse())
                    Header.Controls.AddAt(0, "<script src=\"{0}\" type=\"text/javascript\"></script>".FormatWith(file.Key));

                foreach (var file in RegisteredScriptFiles.Where(x => x.Value == ScriptInsertLocation.HeaderBottom))
                    Header.Controls.Add("<script src=\"{0}\" type=\"text/javascript\"></script>".FormatWith(file.Key));
            }

            if (Form != null)
            {
                foreach (var file in RegisteredScriptFiles.Where(x => x.Value == ScriptInsertLocation.FormTop).Reverse())
                    Form.Controls.AddAt(0, "<script src=\"{0}\" type=\"text/javascript\"></script>".FormatWith(file.Key));

                foreach (var file in RegisteredScriptFiles.Where(x => x.Value == ScriptInsertLocation.FormBottom))
                    Form.Controls.Add("<script src=\"{0}\" type=\"text/javascript\"></script>".FormatWith(file.Key));
            }
        }


        /// <summary>
        /// Registers an external script url on this page.
        /// </summary>
        /// <param name="location">Specifies whether the script should be added to the page header. If False, it will be added to the </param>
        public void RegisterScriptFile(string scriptUrl, ScriptInsertLocation location = ScriptInsertLocation.FormBottom)
        {
            lock (RegisteredScriptFiles)
            {
                if (!RegisteredScriptFiles.ContainsKey(scriptUrl))
                    RegisteredScriptFiles.Add(scriptUrl, location);
            }
        }

        #endregion
    }

    public enum ScriptInsertLocation
    {
        HeaderTop,
        HeaderBottom,
        FormTop,
        FormBottom
    }
}