using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using MSharp.Framework.Services.Globalization;
using MSharp.Framework.UI.Controls;

namespace MSharp.Framework.UI
{
    /// <summary>
    /// Represents an .ascx file, also known as a user control, requested from a
    /// server that hosts an ASP.NET Web application. The file must be called from
    /// a Web Forms page or a parser error will occur.
    /// </summary>
    public class UserControl : global::System.Web.UI.UserControl, ICallbackEventHandler
    {
        bool GenerateAjaxFunctions;
        string[] CallBackArguments;

        /// <summary>
        /// Creates a new UserControl instance.
        /// </summary>
        public UserControl()
        {
            JavascriptVariable = new JavascriptVariable(this);
        }

        /// <summary>
        /// Gets a reference to the <see cref="T:System.Web.UI.Page"/> instance that contains the server control.
        /// </summary>
        public new Page Page
        {
            get
            {
                try
                {

                    return (Page)base.Page;
                }
                catch
                {
                    throw new InvalidOperationException("The page on which {0} is hosted should inherit from {1}.".FormatWith(GetType().FullName, typeof(Page).FullName));
                }
            }
            set
            {
                base.Page = value;
            }
        }

        protected System.Web.UI.Page UnderlyingPage
        {
            get { return base.Page; }
            set { base.Page = value; }
        }

        public MessageBoxManager MessageBox
        {
            get
            {
                return MessageBoxManager.GetMessageBox(UnderlyingPage);
            }
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

        protected T GetActiveValue<T>(T modelValue, ListControl control) where T : IEntity
        {
            if (IsPostBack)
            {
                var id = Request.Form[control.UniqueID];
                if (id.HasValue() && id != Guid.Empty.ToString())
                {
                    return Database.Get<T>(id);
                }
                else
                {
                    return control.GetSelected<T>();
                }
            }
            else return modelValue;
        }

        protected T GetActiveValue<T>(T modelValue, AutoComplete control) where T : IEntity
        {
            if (IsPostBack)
            {
                var id = Request.Form[control.UniqueID + "$SelectedValue"];
                if (id.HasValue() && id != Guid.Empty.ToString())
                {
                    return Database.Get<T>(id);
                }
                else
                {
                    return control.GetSelected<T>();
                }
            }
            else return modelValue;
        }

        protected string GetActiveValue(string modelValue, TextBox control)
        {
            if (IsPostBack)
            {
                return Request.Form[control.UniqueID].Or(control.Text);
            }
            else return modelValue;
        }

        protected string GetActiveValue(string modelValue, HiddenField control)
        {
            if (IsPostBack)
            {
                return Request.Form[control.UniqueID].Or(control.Value);
            }
            else return modelValue;
        }

        protected bool GetActiveValue(bool modelValue, CheckBox control)
        {
            if (IsPostBack)
            {
                return Request.Form[control.UniqueID].ToStringOrEmpty().ToLower() == "on";
            }
            else return modelValue;
        }

        /// <summary>
        ///  Gets a value indicating whether the page request is the result of a call back.
        ///  Returns true if the page request is the result of a call back; otherwise, false.
        /// </summary>
        protected bool IsCallBack { get { return UnderlyingPage.IsCallback; } }

        protected bool? GetActiveValue(bool modelValue, ListControl control)
        {
            return GetActiveValue((bool?)modelValue, control);
        }

        protected bool? GetActiveValue(bool? modelValue, ListControl control)
        {
            if (IsPostBack)
            {
                var result = Request.Form[control.UniqueID].TryParseAs<bool>();
                if (result.HasValue)
                {
                    return result;
                }

                if (control.SelectedValue == "True")
                    return true;
                if (control.SelectedValue == "False")
                    return false;
                return null;
            }
            else return modelValue;
        }

        protected IEnumerable<T> GetActiveValue<T>(IEnumerable<T> modelValue, ListControl control) where T : Entity
        {
            if (IsPostBack)
            {
                var id = control.UniqueID;

                try
                {
                    return
                        Request.Form.AllKeys
                        .Where(k => k.HasValue() && k.StartsWith(control.UniqueID))
                        .Select(k => k.TrimStart(control.UniqueID + "$").To<int>())
                        .Select(i => Database.Get<T>(control.Items[i].Value))
                        .ToArray();
                }
                catch (Exception ex)
                {
                    throw new Exception("Could not get active value for " + control.ID + " because its Items are changed from the previous request." +
                        " GetActiveValue(CheckBoxList) needs the checkbox list to maintain its source of items between postbacks.", ex);
                }
            }
            else return modelValue;
        }

        public event EventHandler PreLoad;

        protected override void OnLoad(EventArgs e)
        {
            OnPreLoad();
            base.OnLoad(e);
        }

        protected virtual void OnPreLoad()
        {
            PreLoad?.Invoke(this, EventArgs.Empty);
        }

        protected T FindControl<T>(string controlId) where T : Control
        {
            return MSharpExtensions.FindControl<T>(this, controlId);
        }

        #region Ajax API

        public string CallAjax()
        {
            GenerateAjaxFunctions = true;
            return ClientID + "_CallServer";
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (GenerateAjaxFunctions)
            {
                UnderlyingPage.ClientScript.RegisterClientScriptBlock(GetType(), "Ajax.API_" + ClientID, GenerateAjaxFunctionsCode(), addScriptTags: true);
            }
        }

        string GenerateAjaxFunctionsCode()
        {
            var callServerFunctionName = ClientID + "_CallServer";
            var receiveServerDataFunctionName = ClientID + "_ReceiveServerData";

            var callBackReference = UnderlyingPage.ClientScript.GetCallbackEventReference(this, "arg", receiveServerDataFunctionName, "context");

            return @"  function {0}(parameters)
                       {{
                            var context = new Object();                                        
                            context.onCallBack = function(returnedObject) {{ context.worker = returnedObject; }};

                            // create a joined string from arguments
                            var arg = '';

                            for(var i=0;i<arguments.length;i++) {{
                                arg+= String(arguments[i]).replace(/\|/g,'[#P¬I¬¬PE#]'); // single pipe will be escaped to ||
                                if (i<arguments.length-1)
                                    arg+='|';
                            }}

                            {1};
                            
                            return context;            
                       }}

                        function {2}(returnedValue, context)
                        {{
                            if (context.worker!=undefined)
                                context.worker(eval(returnedValue));
                        }}
                        ".FormatWith(callServerFunctionName, callBackReference, receiveServerDataFunctionName);
        }

        public string GetCallbackResult()
        {
            var result = ProcessAjaxCall(CallBackArguments);
            string jSonResult = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result);
            return "(" + jSonResult + ")";
        }

        /// <summary>
        /// This should return an anonymous object. Each property of that object will be exposed to the client-side function
        /// via a JSon object.
        /// </summary>
        /// <param name="arguments">The parameters sent by the client call.</param>
        public virtual object ProcessAjaxCall(string[] arguments)
        {
            return null;
        }

        public void RaiseCallbackEvent(string eventArgument)
        {
            CallBackArguments = null;
            if (eventArgument != null)
                CallBackArguments = eventArgument.Split('|').Select(x => x.ReplaceAll("[#P¬I¬¬PE#]", "|")).ToArray();
        }

        #endregion

        /// <summary>
        /// Gets the URL to a page specified by its resource key (from Site Map).
        /// </summary>
        protected string PageUrl(string resourceKey)
        {
            var page = Page as UI.Page;

            if (page != null)
            {
                return page.PageUrl(resourceKey);
            }
            else
            {
                return UI.Page.GetPageUrl(resourceKey);
            }
        }
        
        public JavascriptVariable JavascriptVariable { get; private set; }
        
        #region CloseModal

        /// <summary>
        /// Closes the current modal window.
        /// </summary>
        public void CloseModal()
        {
            Page.CloseModal();
        }

        /// <summary>
        /// Closes the current modal window.
        /// </summary>
        public void CloseModal(AfterClose action = AfterClose.DoNothing, string url = "")
        {
            Page.CloseModal(action, url);
        }

        /// <summary>
        /// Closes the current modal window.
        /// </summary>
        public void CloseModal(AfterClose action)
        {
            Page.CloseModal(action);
        }

        /// <summary>
        /// Closes the current modal window.
        /// </summary>
        public void CloseModal(string url)
        {
            Page.CloseModal(url);
        }

        #endregion

        /// <summary>
        /// Gets the currently specified return URL.
        /// </summary>
        protected string GetReturnUrl()
        {
            return Request.GetReturnUrl();
        }

    }
}