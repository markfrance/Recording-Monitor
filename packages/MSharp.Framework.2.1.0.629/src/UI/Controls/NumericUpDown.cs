using System;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace MSharp.Framework.UI.Controls
{
   
    [ValidationProperty("Text")]
    public class NumericUpDown : TextBox
    {
        /// <summary>
        /// Creates a new NumericUpDown instance.
        /// </summary>
        public NumericUpDown()
        {
            PlusLink = new HyperLink { CssClass = "plus-button" };
            MinusLink = new HyperLink { CssClass = "minus-button" };
            CssClass += " numeric-updown-textbox";

            PlusImageUrl = "~/images/icons/plus.png";
            MinusImageUrl = "~/images/icons/minus.png";

            Minimum = 0;
            Maximum = int.MaxValue;
            StepSize = 1;
        }
        
        #region PlusLink
        /// <summary>
        /// Gets or sets the PlusLink of this NumericUpDown.
        /// </summary>
        public HyperLink PlusLink { get; set; }
        #endregion

        #region MinusLink
        /// <summary>
        /// Gets or sets the MinusLink of this NumericUpDown.
        /// </summary>
        public HyperLink MinusLink { get; set; }
        #endregion

        #region PlusImageUrl
        /// <summary>
        /// Gets or sets the PlusImageUrl of this NumericUpDown.
        /// </summary>
        public string PlusImageUrl { get; set; }
        #endregion
        
        #region MinusImageUrl
        /// <summary>
        /// Gets or sets the MinusImageUrl of this NumericUpDown.
        /// </summary>
        public string MinusImageUrl { get; set; }
        #endregion

        #region Minimum
        /// <summary>
        /// Gets or sets the Minimum of this NumericUpDown.
        /// </summary>
        public int Minimum { get; set; }
        #endregion

        #region Maximum
        /// <summary>
        /// Gets or sets the Maximum of this NumericUpDown.
        /// </summary>
        public int Maximum { get; set; }
        #endregion

        #region StepSize
        /// <summary>
        /// Gets or sets the StepSize of this NumericUpDown.
        /// </summary>
        public int StepSize { get; set; }
        #endregion
        
        static string ScriptFormat = @"
function numericUpDown_AddValue(senderId, add)
{{
    var control = $('#' + senderId);
    var minimum = parseInt(control.attr('minimum'));
    var maximum = parseInt(control.attr('maximum'));
    var current = parseInt(control.val());
    if (isNaN(current)) current = 0;
    current = current + add;
    if(current < minimum) current = minimum;
    if(current > maximum) current = maximum;
    control.val(current.toString());
}}
";
        public override void RenderBeginTag(HtmlTextWriter writer)
        {
            writer.Write("<div class='numeric-updown'>");
            MinusLink.RenderControl(writer);
            base.RenderBeginTag(writer);
        }

        public override void RenderEndTag(HtmlTextWriter writer)
        {
            base.RenderEndTag(writer);

            PlusLink.RenderControl(writer);
            writer.Write("</div>");
        }
       
        protected override void OnPreRender(EventArgs e)
        {
            Attributes["Minimum"] = Minimum.ToString();
            Attributes["Maximum"] = Maximum.ToString();
            Attributes["StepSize"] = StepSize.ToString();

            var plusImage = new Image { ImageUrl = PlusImageUrl };
            var minusImage = new Image { ImageUrl = MinusImageUrl };
            PlusLink.Controls.Add(plusImage);
            MinusLink.Controls.Add(minusImage);
            PlusLink.NavigateUrl = "javascript:numericUpDown_AddValue('{0}', {1});".FormatWith(this.ClientID, StepSize);
            MinusLink.NavigateUrl = "javascript:numericUpDown_AddValue('{0}', -{1});".FormatWith(this.ClientID, StepSize);

            Page.ClientScript.RegisterClientScriptBlock(GetType(), "Script for NumericUpDown", ScriptFormat, addScriptTags: true);

            base.OnPreRender(e);
        }
    }
}