using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using MSharp.Framework.Services;

namespace MSharp.Framework.UI
{
    internal class SortExpressionProperty
    {
        StateBag ViewState;
        GridView GridView;
        bool IsPostBack;
        string SortExpressionKey;
        const string LIST_CONTAINERS_SORT_EXPRESSION_COOKIE_NAME = "MSharp.Framework.UI.Grid.SortExpressions";
        const int MAXIMUM_COOKIE_SIZE = 750; // 1.5KB, with 2 bytes per character.

        /// <summary>
        /// Creates a new SortExpressionProperty instance.
        /// </summary>
        public SortExpressionProperty(StateBag viewState, GridView gridView, string sortExpressionKey, bool isPostBack)
        {
            this.ViewState = viewState;
            this.GridView = gridView;
            this.IsPostBack = isPostBack;
            this.SortExpressionKey = sortExpressionKey;
        }

        public string Get()
        {
            var result = ViewState["Current.Sort.Expression"]?.ToString();

            if (result.IsEmpty())
                result = GetSortExpressionFromCookie();

            return result;
        }

        /// <summary>
        /// Sets the specified value directly without processing it.
        /// </summary>
        public void SetDirect(string value)
        {
            ViewState["Current.Sort.Expression"] = value;

            SetSortExpressionInCookie(value);

            if (GridView != null)
            {
                GridView.Attributes["CurrentSort"] = value;

                if (!IsPostBack)
                {
                    // Set the default sort of the column with the same sort as the "default sort" of the module:
                    GridView.Columns.OfType<DataControlField>().Where(c => c.SortExpression == value).Do(c => c.SortExpression += " DESC");
                }
            }
        }

        /// <summary>
        /// Sets the specified sort expression. It processes DESC before setting it to create the toggle effect.
        /// </summary>
        public void Set(string value)
        {
            if (value == Get())
                value += " DESC";

            value = value.TrimEnd(" DESC DESC");

            SetDirect(value);
        }

        static Dictionary<string, string> ReadAllSortExpressionSettings()
        {
            var cookie = CookieProperty.Get(LIST_CONTAINERS_SORT_EXPRESSION_COOKIE_NAME);

            if (cookie.IsEmpty()) return new Dictionary<string, string>();

            var items = cookie.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries).Trim();
            var settings = items.Select(a => a.Split('=').Trim().ToArray()).Where(x => x.Length == 2).ToArray();

            return settings.ToDictionary(a => a[0], a => a[1]);
        }

        internal string GetSortExpressionFromCookie()
        {
            try
            {
                return ReadAllSortExpressionSettings().GetOrDefault(SortExpressionKey);
            }
            catch
            {
                // Bad cookie:
                return null;
            }
        }

        internal void SetSortExpressionInCookie(string newSortExpression)
        {
            try
            {
                var items = ReadAllSortExpressionSettings();

                if (items.ContainsKey(SortExpressionKey))
                    items.Remove(SortExpressionKey);

                // Add it to the end of the list:
                items.Add(SortExpressionKey, newSortExpression);

                CookieProperty.Set(LIST_CONTAINERS_SORT_EXPRESSION_COOKIE_NAME, CreateNewCookieValue(items));
            }
            catch
            {
                // Problem with cookie. Ignore.
            }
        }

        string CreateNewCookieValue(Dictionary<string, string> data)
        {
            var result = new StringBuilder();

            // Start from the end of the list:
            foreach (var key in data.Keys.Reverse())
            {
                var newEntry = "{0}={1}".FormatWith(key, data[key]) + "&";

                if (result.Length + newEntry.Length >= MAXIMUM_COOKIE_SIZE)
                    return result.ToString();

                result.Append(newEntry);
            }

            return result.ToString();
        }
    }
}