using System;
using System.IO;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace MSharp.Framework.UI.Controls
{
   
    /// <summary>
    /// A control that displays an image, hover image and responds to mouse clicks on the image.
    /// </summary>
    public class HoverButton : ImageButton
    {
        public string HoverImageUrl { get; set; }

        public string NavigateUrl { get; set; }

        public string Target { get; set; }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (HoverImageUrl.HasValue())

            AlternateText = AlternateText.Or(Text);

            ImageUrl = Page.ResolveUrl(ImageUrl);

            if (HoverImageUrl.HasValue())
            {
                Attributes.Add("onmouseover", "this.src='" + Page.ResolveUrl(HoverImageUrl) + "';");
                Attributes.Add("onfocus", "this.src='" + Page.ResolveUrl(HoverImageUrl) + "';");

                Attributes.Add("onmouseout", "this.src='" + Page.ResolveUrl(ImageUrl) + "';");
                Attributes.Add("onblur", "this.src='" + Page.ResolveUrl(ImageUrl) + "';");
            }

            if (HoverImageUrl.HasValue())
                System.Web.UI.ScriptManager.RegisterStartupScript(Page, GetType(), "Preload" + Page.ResolveUrl(HoverImageUrl),
                    "preloadImages('" + Page.ResolveUrl(HoverImageUrl) + "');", addScriptTags: true);
        }

        protected override void Render(HtmlTextWriter writer)
        {
            if (NavigateUrl.IsEmpty())
            {
                base.Render(writer);
                return;
            }

            var output = new StringBuilder();
            base.Render(new HtmlTextWriter(new StringWriter(output)));
            var url = Page.ResolveUrl(NavigateUrl);

            output = output
                .Replace("<input type=\"image\" ",

                 "<a href=\"{0}\"{1}>".FormatWith(url, Target.WithPrefix(" target=\"").WithSuffix("\"")) +

                "<img style=\"border=0px;\" ")

                .Append("</a>");

            writer.Write(output.ToString());
        }
    }
}