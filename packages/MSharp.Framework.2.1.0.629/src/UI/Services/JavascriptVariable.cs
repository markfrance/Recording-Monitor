using System.Web.UI;
namespace MSharp.Framework.UI
{
    public class JavascriptVariable
    {
       Control Owner;

        /// <summary>
        /// Creates a new JavascriptVariable instance.
        /// </summary>
        public JavascriptVariable(Control owner)
        {
            Owner = owner;
        }

        public string this[string name]
        {
            get
            {
                return Owner.ClientID + "_" + name;
            }
        }
    }
}
