namespace MSharp.Framework.UI.Controls
{
    using System;
    using System.Web.UI;
    using System.Web.UI.WebControls;

    public class CollapsibleCheckBoxList : CheckBoxList
    {
        TextBox ViewBox;
        TextBox SearchBox;
        Panel ToolBox;
        Panel ContainerPanel;
        Panel SelectedItemsPanel;

        #region HideDelay
        /// <summary>
        /// Gets or sets the HideDelay of this MultiSelectList.
        /// </summary>
        public int HideDelay { get; set; }
        #endregion

        #region PluralName
        /// <summary>
        /// Gets or sets the PluralName of this MultiSelectList.
        /// </summary>
        public string PluralName { get; set; }
        #endregion

        #region InitialSearchText
        /// <summary>
        /// Gets or sets the InitialSearchText of this MultiSelectList.
        /// </summary>
        public string InitialSearchText { get; set; }
        #endregion

        #region NotSetText
        /// <summary>
        /// Gets or sets the NotSetText of this MultiSelectList.
        /// </summary>
        public string NotSetText { get; set; }
        #endregion

        #region AutoPostBack
        bool _IsAutoPostBack = false;
        public override bool AutoPostBack
        {
            get
            {
                return false;
            }
            set
            {
                _IsAutoPostBack = value;
                base.AutoPostBack = false;
            }
        }
        #endregion

        /// <summary>
        /// Creates a new MultiSelectList instance.
        /// </summary>
        public CollapsibleCheckBoxList()
        {
            ViewBox = new TextBox { ID = "txtCheckboxList", CssClass = "text-box" };
            SearchBox = new TextBox { ID = "txtSearch", CssClass = "search-box" };
            ToolBox = new Panel { ID = "pnlToolbox", CssClass = "toolbox" };
            ToolBox.Controls.Add(new LinkButton { CssClass = "select-all", Text = "Select all" });
            ToolBox.Controls.Add(" | ");
            ToolBox.Controls.Add(new LinkButton { CssClass = "remove-all", Text = "Remove all" });

            ContainerPanel = new Panel { ID = "pnlContainer", CssClass = "panel-container" };
            SelectedItemsPanel = new Panel { ID = "pnlSelectedItems", CssClass = "selected-items" };
            HideDelay = 500;
        }

        protected override void OnPreRender(EventArgs e)
        {
            ViewBox.Text = NotSetText;
            ViewBox.Attributes["notSetText"] = NotSetText;
            ViewBox.Attributes["autoPostBack"] = _IsAutoPostBack.ToString().ToLower();
            ViewBox.Attributes["controlID"] = ClientID;

            SearchBox.Text = InitialSearchText;
            SearchBox.Style["display"] = "none";

            base.OnPreRender(e);

            ContainerPanel.Attributes["style"] = "display:none;position:absolute;";

            ViewBox.Attributes["hideDelay"] = HideDelay.ToString();
            ViewBox.Attributes["pluralName"] = PluralName;
        }

        protected override void Render(HtmlTextWriter writer)
        {
            writer.WriteLine("<div class='multiselect-dropdown{0}'>".FormatWith(this.CssClass.WithPrefix(" ")));

            ContainerPanel.RenderBeginTag(writer);

            ToolBox.RenderControl(writer);

            writer.WriteLine("<div class='items-list' >");
            base.Render(writer);
            writer.WriteLine("</div>");

            SelectedItemsPanel.RenderControl(writer);
            ContainerPanel.RenderEndTag(writer);

            SearchBox.RenderControl(writer);
            ViewBox.RenderControl(writer);

            writer.WriteLine("</div>");
        }
    }
}