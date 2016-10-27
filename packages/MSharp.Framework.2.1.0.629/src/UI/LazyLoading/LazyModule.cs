using System;
using System.Collections.Generic;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace MSharp.Framework.UI
{

    public class LazyModule : System.Web.UI.Control
    {
        /// <summary>
        /// Creates a new LazyModule instance.
        /// </summary>
        public LazyModule()
        {
            LoadingImageUrl = "~/Image/PleaseWait.gif";
        }

        public string UserControl { get; set; }

        public string Settings { get; set; }

        public string LoadingImageUrl { get; set; }

        HtmlControl Container;

        protected override void CreateChildControls()
        {
            base.CreateChildControls();

            Page.ClientScript.RegisterStartupScript(GetType(), "Load " + ClientID, GenerateLoadScript(), addScriptTags: true);

            Controls.Add(Container = new HtmlGenericControl("span") { ID = "LazyContainer" });

            Controls.Add(new Image { ImageUrl = LoadingImageUrl, ID = "LoadingImage" });

        }

        private string GenerateLoadScript()
        {
            return @"
$(document).ready(function() {
    alert('lazy request will be sent...');

    // TODO: send an Ajax request to the server.
    [#LAZY.BOX#]
});".Replace("[#LAZY.BOX#]", Container.ForJQuery());
        }
    }
}
