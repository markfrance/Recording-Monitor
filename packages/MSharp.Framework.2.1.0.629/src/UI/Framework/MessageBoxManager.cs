using System;
using System.Collections.Generic;
using System.Linq;
using webUI = System.Web.UI;

namespace MSharp.Framework.UI
{
    public class MessageBoxManager
    {
        webUI.Page Page;

        internal readonly List<string> Messages = new List<string>();
        internal readonly List<string> GentleMessages = new List<string>();

        /// <summary>
        /// Creates a new MessageBoxManager instance.
        /// </summary>
        public MessageBoxManager(webUI.Page page)
        {
            if (page == null)
                throw new ArgumentNullException("page");

            Page = page;
            Page.PreRenderComplete += new EventHandler(Page_PreRenderComplete);
        }

        /// <summary>
        /// Applies the Render scripts for this message box on the specified page.
        /// </summary>
        void Page_PreRenderComplete(object sender, EventArgs e)
        {

            // Normal Alerts ------------------------------------ :
            if (Messages.Any())
            {
                var message = Messages.Select(m => JavascriptEncode(m)).ToString("\\r\\n");

                var script = @" RunOnLoad(function () {{ setTimeout(function() {{ alert('{0}'); }}, 150); }});".FormatWith(message);

                System.Web.UI.ScriptManager.RegisterStartupScript(Page, GetType(), "ShowMessageBox", script, addScriptTags: true);
            }
            
            // Gentle Alerts ------------------------------------ :
            if (GentleMessages.Any())
            {
                var message = GentleMessages.Select(m => JavascriptEncode(m)).ToString("\\r\\n");

                var script = @" RunOnLoad(function () {{ setTimeout(function() {{ alertGently('{0}'); }}, 150); }});".FormatWith(message);

                System.Web.UI.ScriptManager.RegisterStartupScript(Page, GetType(), "ShowMessageBox Gently", script, addScriptTags: true);
            }
        }


        static string JavascriptEncode(string message)
        {
            return message.Trim().Replace("\\", "\\\\")
                .Replace("'", "\\'").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n");
        }


        /// <summary>
        /// Shows the specified message object's ToString() to the user.
        /// </summary>
        public void Show(object message)
        {
            Show(message?.ToString());
        }

        /// <summary>
        /// Shows the specified message to the user.
        /// </summary>
        public void Show(string message)
        {
            if (message.IsEmpty())
                message = "[Empty Message]";

            if (!Messages.Contains(message))
                Messages.Add(message);
        }


        /// <summary>
        /// Gently shows the specified message object's ToString() to the user.
        /// </summary>
        public void ShowGently(object message)
        {
            ShowGently(message?.ToString());
        }

        /// <summary>
        /// Gently shows the specified message to the user.
        /// </summary>
        public void ShowGently(string message)
        {
            if (message.IsEmpty())
                message = "[Empty Message]";

            if (!GentleMessages.Contains(message))
                GentleMessages.Add(message);
        }


        //internal bool IsEmpty()
        //{
        //    return Messages.None();
        //}

        internal static MessageBoxManager GetMessageBox(System.Web.UI.Page page)
        {
            if (page == null)
                throw new ArgumentNullException("page");

            var errorMessage = "The specified System.Web.UI.Page instance's type does not define a public property of type MessageBoxManager named 'MessageBox'.";

            try
            {
                var result = EntityManager.ReadProperty(page, "MessageBox") as MessageBoxManager;

                if (result == null)
                    throw new ArgumentException(errorMessage);

                return result;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(errorMessage, ex);
            }
        }
    }
}