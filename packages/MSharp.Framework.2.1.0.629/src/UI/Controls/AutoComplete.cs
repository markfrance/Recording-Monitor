using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using AjaxControlToolkit;

namespace MSharp.Framework.UI.Controls
{
    [ValidationProperty("Text")]
    public class AutoComplete : CompositeControl, IButtonControl
    {
        /// <summary>
        /// Creates a new AutoComplete instance.
        /// </summary>
        public AutoComplete()
        {
            Items = new ListItemCollection();
            InnerTextBox = new TextBox { ID = "txt", CssClass = "textbox" };
            ItemsPanel = new Panel { ID = "items", CssClass = "panel" };
            ItemsPanel.Style.Add("position", "absolute");
            ItemsPanel.Style.Add("display", "none");

            SelectedValueBox = new HiddenField { ID = "SelectedValue" };

            base.CssClass = "AutoComplete";

            PageCount = 1;
        }

        public string WrapperCssClass
        {
            get
            {
                return base.CssClass;
            }
            set
            {
                base.CssClass = value;
            }
        }

        public override string CssClass
        {
            get
            {
                return InnerTextBox.CssClass;
            }
            set
            {
                InnerTextBox.CssClass = value;
            }
        }

        public bool SuppressEnterKey
        {
            get
            {
                return InnerTextBox.Attributes["SuppressEnterKey"].TryParseAs<bool>() ?? true;
            }
            set
            {
                InnerTextBox.Attributes["SuppressEnterKey"] = value.ToString();
            }
        }


        public override string AccessKey
        {
            get
            {
                return InnerTextBox.AccessKey;
            }
            set
            {
                InnerTextBox.AccessKey = value;
            }
        }

        #region SelectedValueBox
        /// <summary>
        /// Gets or sets the SelectedValueBox of this AutoComplete.
        /// </summary>
        HiddenField SelectedValueBox { get; set; }
        #endregion

        public static bool IsAutoCompletePostBack()
        {
            return System.Web.HttpContext.Current.Request["__AUTOCOMPLETE_ID"].HasValue();
        }

        #region Events
        #region TextChanged Event
        public event EventHandler TextChanged;
        void OnTextChanged()
        {
            if (TextChanged == null) return;

            if (ClientSide) Text = string.Empty;

            TextChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region SelectedValueChanged event
        public event EventHandler SelectedValueChanged;
        void OnSelectedValueChanged()
        {
            SelectedValueChanged?.Invoke(this, new EventArgs());
        }
        #endregion
        #endregion

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            if (System.IO.File.Exists(AppDomain.CurrentDomain.GetPath("Content\\AutoComplete.Control.js")))
                Page.ClientScript.RegisterClientScriptInclude("AutoComplete.Control.Scripts", Page.ResolveUrl("~/Content/AutoComplete.Control.js"));
        }

        protected override void OnLoad(EventArgs e)
        {
            // handle postback
            if (Page.Request["__AUTOCOMPLETE_ID"] == UniqueID)
            {
                var filter = Page.Request["__AUTOCOMPLETE_FILTER"];
                RaiseCallbackEvent(filter, Page.Request["__AUTOCOMPLETE_PAGECOUNT"].TryParseAs<int>() ?? 1);
                Page.Response.Write(GetCallbackResult(filter));
                Page.Response.ContentType = "application/json";
                Page.Response.End();
            }

            // handle selected value changed
            if (Page.Request["__EVENTTARGET"] == ClientID)
                OnSelectedValueChanged();

            base.OnLoad(e);
        }

        #region AutoPostBack
        /// <summary>
        /// Gets or sets the AutoPostBack of this AutoComplete.
        /// </summary>
        public bool AutoPostBack { get; set; }
        #endregion

        #region OnSelectedValueChange
        /// <summary>
        /// Gets or sets the OnSelectedValueChanged of this AutoComplete.
        /// </summary>
        public string OnSelectedValueChange { get; set; }
        #endregion

        #region OnCollapse
        /// <summary>
        /// Gets or sets the OnCollapse of this AutoComplete.
        /// </summary>
        public string OnCollapse { get; set; }
        #endregion

        #region RequestDelay
        /// <summary>
        /// Gets or sets the RequestDelay of this AutoComplete DEFAULT IS 300.
        /// </summary>
        public int? RequestDelay { get; set; }
        #endregion

        #region ExpandOnFocus
        /// <summary>
        /// Gets or sets the ExpandOnFocus of this AutoComplete.
        /// </summary>
        public bool ExpandOnFocus { get; set; }
        #endregion

        #region OptimizedMode
        /// <summary>
        /// Gets or sets the OptimizedMode of this AutoComplete.
        /// </summary>
        public bool? OptimizedMode { get; set; }
        #endregion

        #region SourceProvider
        /// <summary>
        /// Gets or sets the SourceProvider of this AutoComplete.
        /// </summary>
        public string SourceProvider { get; set; }
        #endregion

        #region ClientSide
        /// <summary>
        /// Gets or sets the ClientSide of this AutoComplete.
        /// </summary>
        public bool ClientSide { get; set; }
        #endregion

        #region Items
        /// <summary>
        /// Gets or sets the Items of this control.
        /// </summary>
        public ListItemCollection Items { get; private set; }
        #endregion

        #region WatermarkText
        /// <summary>
        /// Gets or sets the WatermarkText of this AutoComplete.
        /// </summary>
        public string WatermarkText { get; set; }
        #endregion

        #region NotFoundText
        /// <summary>
        /// Gets or sets the NotFoundText of this AutoComplete.
        /// </summary>
        public string NotFoundText { get; set; }
        #endregion

        /// <summary>
        /// Sets the selected item.
        /// </summary>
        public void SetSelectedItem(string text, string value)
        {
            Text = text;
            SelectedValue = value;
        }

        #region Text
        /// <summary>
        /// Gets or sets the Text of this AutoComplete.
        /// </summary>

        public string Text { get { return InnerTextBox.Text; } set { InnerTextBox.Text = value; } }
        #endregion

        #region LoadingText
        /// <summary>
        /// Gets or sets the LoadingText of this AutoComplete.
        /// </summary>
        public string LoadingText { get; set; }
        #endregion

        #region InnerTextBox
        /// <summary>
        /// Gets or sets the InnerTextBox of this AutoComplete.
        /// </summary>
        public TextBox InnerTextBox { get; private set; }
        #endregion

        #region ItemsPanel
        /// <summary>
        /// Gets or sets the ItemsPanel of this AutoComplete.
        /// </summary>
        public Panel ItemsPanel { get; private set; }
        #endregion

        #region SelectedValue
        /// <summary>
        /// Gets or sets the SelectedValue of this AutoComplete.
        /// </summary>
        public string SelectedValue
        {
            get
            {
                return SelectedValueBox.Value
                    .Or(Page.Request[SelectedValueBox.UniqueID])
                    .Or(Items.FindByText(Text)?.Value);
            }
            set { SelectedValueBox.Value = value; }
        }
        #endregion

        /// <summary>
        /// Disables auto complete behaviour and acts like a normal text box
        /// </summary>
        public bool TextBoxOnly { get; set; }

        protected override void CreateChildControls()
        {
            base.CreateChildControls();

            if (!TextBoxOnly)
            {
                InnerTextBox.Attributes.Add("autocomplete", "off");
            }

            Controls.Add(InnerTextBox);
            Controls.Add(SelectedValueBox);
            Controls.Add(ItemsPanel);

            if (TextBoxOnly)
            {
                InnerTextBox.Attributes["TextBoxOnly"] = "1";
            }

        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.PreRender"/> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs"/> object that contains the event data.</param>
        protected override void OnPreRender(EventArgs e)
        {
            ConfigureScriptsAndAttributes();

            base.OnPreRender(e);
        }

        /// <summary>
        /// Sets input focus to a control.
        /// </summary>
        public void Focus(bool safe)
        {
            InnerTextBox?.Focus(safe);
        }

        /// <summary>
        /// Sets input focus to a control.
        /// </summary>
        public override void Focus()
        {
            Focus(safe: false);
        }

        public void ConfigureScriptsAndAttributes()
        {
            EnsureChildControls();

            this.Attributes.Add("name", UniqueID);

            if (RequestDelay.HasValue)
            {
                InnerTextBox.Attributes["requestDelay"] = RequestDelay.Value.ToString();
            }

            if (ExpandOnFocus)
            {
                InnerTextBox.Attributes["expandOnFocus"] = "true";
            }

            if (OptimizedMode.HasValue)
            {
                InnerTextBox.Attributes["optimizedMode"] = OptimizedMode.Value.ToString().ToLower();
            }

            if (SourceProvider.HasValue())
            {
                InnerTextBox.Attributes["SourceProvider"] = SourceProvider;
            }

            if (ClientSide)
            {
                InnerTextBox.Attributes["clientSide"] = "true";
            }

            if (AutoPostBack)
            {
                InnerTextBox.Attributes["AutoPostBack"] = "true";
            }

            if (OnSelectedValueChange.HasValue())
            {
                InnerTextBox.Attributes["OnSelectedValueChange"] = OnSelectedValueChange;
            }

            if (OnCollapse.HasValue())
            {
                InnerTextBox.Attributes["OnCollapse"] = OnCollapse;
            }

            if (NotFoundText.HasValue())
            {
                InnerTextBox.Attributes["NotFoundText"] = NotFoundText;
            }

            if (WatermarkText.HasValue())
            {
                var water = new TextBoxWatermarkExtender { WatermarkText = WatermarkText, TargetControlID = InnerTextBox.ID, WatermarkCssClass = InnerTextBox.CssClass.WithSuffix(" waterMark") };
                Controls.Add(water);
            }
        }

        #region PageSize
        /// <summary>
        /// Gets or sets the PageCount of this AutoComplete.
        /// </summary>
        public int PageCount { get; set; }
        #endregion

        string GetCallbackResult(string filter)
        {
            return GetCallbackResult(Items.Cast<ListItem>(), filter);
        }

        /// <summary>
        /// Gets the callback result of the given items in autocomplete-feidnly json format.
        /// </summary>
        /// <param name="items">items to be serialized</param>
        /// <param name="filter">filter that is used to produce these items</param>
        public static string GetCallbackResult(IEnumerable<ListItem> items, string filter)
        {
            return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new
            {
                items = items.Select(item => new { value = item.Value, display = item.Text }).ToArray(),
                filter = filter
            });
        }

        public void RaiseCallbackEvent(string text, int pageCount)
        {
            if (text == WatermarkText) Text = string.Empty;
            else Text = text;

            PageCount = pageCount;

            OnTextChanged();
        }

        #region IButtonControl Members

        public string CommandArgument { get; set; }
        public string CommandName { get; set; }
        public bool CausesValidation { get; set; }
        public string ValidationGroup { get; set; }
        public string PostBackUrl { get; set; }

        event EventHandler IButtonControl.Click { add { throw new NotImplementedException(); } remove { throw new NotImplementedException(); } }
        event CommandEventHandler IButtonControl.Command { add { throw new NotImplementedException(); } remove { throw new NotImplementedException(); } }

        #endregion
    }
}