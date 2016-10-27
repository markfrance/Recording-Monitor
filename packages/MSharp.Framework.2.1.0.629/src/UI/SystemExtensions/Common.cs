namespace System
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Web;
    using System.Web.UI;
    using System.Web.UI.HtmlControls;
    using System.Web.UI.WebControls;
    using MSharp.Framework;
    using MSharp.Framework.Services;
    using MSharp.Framework.Services.Globalization;
    using MSharp.Framework.UI.Controls;
    using Xml.Linq;
    public static partial class MSharpExtensions
    {
        /// <summary>
        /// Adds the specified query string setting to this Url.
        /// </summary>
        public static Uri AddQueryString(this Uri url, string key, string value)
        {
            var qs = url.GetQueryString();

            qs.RemoveWhere(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            qs.Add(key, value);

            return url.ReplaceQueryString(qs);
        }

        /// <summary>
        /// Gets the query string parameters of this Url.
        /// </summary>
        public static Dictionary<string, string> GetQueryString(this Uri url)
        {
            var entries = System.Web.HttpUtility.ParseQueryString(url.Query);
            return entries.AllKeys.ExceptNull().ToDictionary(a => a.ToLower(), a => entries[a]);
        }

        public static T GetSelected<T>(this AutoComplete list) where T : IEntity
        {
            var id = list.SelectedValue;

            if (id == null) return default(T);
            else return Database.GetOrDefault<T>(id);
        }

        /// <summary>
        /// Sets the selected items of this list control to the specified array of objects.
        /// </summary>
        public static void SetAllSelected<T>(this ListControl list, IEnumerable<T> selectedItems) where T : IEntity
        {
            selectedItems = selectedItems.Where(i => i != null);

            foreach (ListItem item in list.Items)
            {
                item.Selected = selectedItems.Any(i => i.GetId().ToString() == item.Value);
            }
        }

        /// <summary>
        /// Sets the selected item of this list control.
        /// </summary>
        public static void SetSelectedItem<T>(this ListControl list, T item) where T : IEntity
        {
            if (item == null)
            {
                var nullItem = list.Items.FindByValue(Guid.Empty.ToString());
                if (nullItem != null)
                {
                    list.SelectedValue = Guid.Empty.ToString();
                }
                else
                {
                    nullItem = list.GetItems().FirstOrDefault(i => i.Value.IsEmpty());
                    if (nullItem != null)
                    {
                        list.SelectedValue = nullItem.Value;
                    }
                    else
                    {
                        list.SelectedIndex = -1;
                    }
                }
            }
            else
            {
                try { list.SelectedValue = item.GetId().ToString(); }
                catch { throw new Exception(list.ID + " does not have an item with ID of " + item.GetId() + "."); }
            }
        }

        public static List<T> GetAllSelected<T>(this ListControl list) where T : IEntity
        {
            var result = new List<T>();

            foreach (var value in list.Items.Cast<ListItem>().Where(i => i.Selected).Select(i => i.Value))
            {
                if (value != Guid.Empty.ToString())
                    result.Add(Database.Get<T>(value));
            }

            return result;
        }

        public static bool Lacks(this DataControlFieldCollection collection, DataControlField field)
        {
            return !collection.Contains(field);
        }

        /// <summary>
        /// Sets the selected item.
        /// </summary>
        public static void SetSelectedItem(this AutoComplete autoComplete, IEntity item)
        {
            if (item == null)
                autoComplete.SetSelectedItem(string.Empty, string.Empty);
            else autoComplete.SetSelectedItem(item.ToString(), item.GetId().ToString());
        }

        #region Request.Get

        /// <summary>
        /// Returns an object whose ID is given in query string with the key of "id".
        /// </summary>
        public static T Get<T>(this HttpRequest request) where T : class, IEntity
        {
            return Get<T>(request, "id");
        }

        /// <summary>
        /// Gets the cookies sent by the client.
        /// </summary>
        public static IEnumerable<HttpCookie> GetCookies(this HttpRequest request)
        {
            if (request.Cookies == null) return Enumerable.Empty<HttpCookie>();

            return request.Cookies.AllKeys.Select(key => request.Cookies[key]);
        }

        /// <summary>
        /// Gets the data with the specified type from QueryString[key].
        /// If the specified type is an entity, then the ID of that record will be read from query string and then fetched from database.
        /// </summary>
        public static T Get<T>(this HttpRequest request, string key)
        {
            return DoGet<T>(request, key, throwIfNotFound: true);
        }


        static T DoGet<T>(this HttpRequest request, string key, bool throwIfNotFound)
        {
            if (typeof(T).Implements<IEntity>())
            {
                return GetEntity<T>(request, key, throwIfNotFound);
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)GetValue(request, key);
            }
            else if (typeof(T).IsValueType)
            {
                return (T)GetValue<T>(request, key);
            }
            else throw new Exception("Request.Get<T>() does not recognize the type of " + typeof(T).FullName);
        }

        /// <summary>
        /// Returns a string value specified in the request context.
        /// </summary>
        static string GetValue(this HttpRequest request, string key)
        {
            return request[key] ?? request.RequestContext.RouteData.Values[key].ToStringOrEmpty();
        }

        /// <summary>
        /// Returns a value specified in the request context.
        /// </summary>
        static T GetValue<T>(this HttpRequest request, string key)
        {
            var data = GetValue(request, key);

            if (data.IsEmpty())
            {
                if (typeof(T).IsNullable() || typeof(T) == typeof(string)) return default(T);
                else throw new Exception("Request does not contain a value for '" + key + "'");
            }

            return data.To<T>();
        }

        /// <summary>
        /// Gets the record with the specified type. The ID of the record will be read from QueryString[key].
        /// </summary>
        static T GetEntity<T>(this HttpRequest request, string key, bool throwIfNotFound = true)
        {
            if (request == null)
            {
                if (HttpContext.Current != null)
                {
                    request = HttpContext.Current.Request;
                }
                else throw new InvalidOperationException("Request.Get<T>() can only be called inside an Http context.");
            }

            if (key == ".") key = "." + typeof(T).Name;

            var value = request[key];
            if (value.IsEmpty()) return default(T);

            try { return (T)Database.Get(value, typeof(T)); }
            catch (Exception ex)
            {
                if (throwIfNotFound)
                {
                    throw new InvalidOperationException("Loading a {0} from the page argument of '{1}' failed.".FormatWith(typeof(T).FullName, key), ex);
                }
                else { return default(T); }
            }
        }

        #endregion

        #region Request.GetOrDefault

        /// <summary>
        /// Gets the record with the specified type. The ID of the record will be read from QueryString["id"].
        /// </summary>
        public static T GetOrDefault<T>(this HttpRequest request)
        {
            return GetOrDefault<T>(request, "id");
        }

        /// <summary>
        /// Gets the record with the specified type. The ID of the record will be read from QueryString[key].
        /// </summary>
        public static T GetOrDefault<T>(this HttpRequest request, string key)
        {
            if (request == null)
            {
                if (HttpContext.Current != null)
                {
                    request = HttpContext.Current.Request;
                }
                else throw new InvalidOperationException("Request.GetOrDefault<T>() can only be called inside an Http context.");
            }

            if (key == ".") key = "." + typeof(T).Name;

            if (!request.Has(key)) return default(T);

            try { return request.DoGet<T>(key, throwIfNotFound: false); }
            catch { return default(T); }
        }

        #endregion

        public static IEnumerable<T> GetList<T>(this HttpRequest request, string key) where T : class, IEntity
        {
            return GetList<T>(request, key, ',');
        }

        /// <summary>
        /// Gets a list of objects of which Ids come in query string.
        /// </summary>
        /// <param name="key">The key of the query string element containing ids.</param>
        /// <param name="seperator">The sepeerator of Ids in the query string value. The default will be comma (",").</param>
        public static IEnumerable<T> GetList<T>(this HttpRequest request, string key, char seperator) where T : class, IEntity
        {
            var ids = request[key];
            if (ids.IsEmpty())
                yield break;
            else
            {
                foreach (var id in ids.Split(seperator))
                {
                    yield return Database.Get<T>(id);
                }
            }
        }

        /// <summary>
        /// Gets the selected object on this list control.
        /// </summary>
        public static T GetSelected<T>(this ListControl list) where T : IEntity
        {
            if (list.Items.Count == 0) return default(T);

            var selectedId = list.SelectedValue;

            if (selectedId.IsEmpty() || selectedId == Guid.Empty.ToString())
            {
                return default(T);
            }
            else
            {
                return Database.Get<T>(selectedId);
            }
        }

        /// <summary>
        /// Gets the items of this list control.
        /// </summary>
        public static IEnumerable<ListItem> GetItems(this ListControl list)
        {
            return list.Items.Cast<ListItem>();
        }

        /// <summary>
        /// Gets the items of this data list.
        /// </summary>
        public static IEnumerable<DataListItem> GetItems(this DataList list)
        {
            return list.Items.Cast<DataListItem>();
        }

        /// <summary>
        /// Gets the rows of this data list.
        /// </summary>
        public static IEnumerable<GridViewRow> GetRows(this GridView list)
        {
            return list.Rows.Cast<GridViewRow>();
        }

        public static IEnumerable<T> GetParentSource<T>(this IDataItemContainer container)
        {
            var parentList = (container as Control)?.FindParent<BaseDataBoundControl>();

            if (parentList == null) return null;

            return parentList.DataSource as IEnumerable<T>;
        }

        /// <summary>
        /// TODO: Refactor and clean this method.
        /// </summary>
        public static bool IsMobile(this HttpRequest request)
        {
            var agent = request.ServerVariables["HTTP_USER_AGENT"]?.ToLower();

            // Checks the user-agent  
            if (agent != null)
            {
                // Checks if its a Windows browser but not a Windows Mobile browser  
                if (agent.Contains("windows") && !agent.Contains("windows ce"))
                {
                    return false;
                }

                // Checks if it is a mobile browser  
                var pattern = "up.browser|up.link|windows ce|iphone|iemobile|mini|mmp|symbian|midp|wap|phone|pocket|mobile|pda|psp";
                var mc = System.Text.RegularExpressions.Regex.Matches(agent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (mc.Count > 0)
                    return true;

                // Checks if the 4 first chars of the user-agent match any of the most popular user-agents  
                var popUA = "|acs-|alav|alca|amoi|audi|aste|avan|benq|bird|blac|blaz|brew|cell|cldc|cmd-|dang|doco|eric|hipt|inno|ipaq|java|jigs|kddi|keji|leno|lg-c|lg-d|lg-g|lge-|maui|maxo|midp|mits|mmef|mobi|mot-|moto|mwbp|nec-|newt|noki|opwv|palm|pana|pant|pdxg|phil|play|pluc|port|prox|qtek|qwap|sage|sams|sany|sch-|sec-|send|seri|sgh-|shar|sie-|siem|smal|smar|sony|sph-|symb|t-mo|teli|tim-|tosh|tsm-|upg1|upsi|vk-v|voda|w3c |wap-|wapa|wapi|wapp|wapr|webc|winw|winw|xda|xda-|";
                if (popUA.Contains("|" + agent.Substring(0, 4) + "|"))
                    return true;
            }

            // Checks the accept header for wap.wml or wap.xhtml support  
            var accept = request.ServerVariables["HTTP_ACCEPT"];
            if (accept != null)
            {
                if (accept.Contains("text/vnd.wap.wml") || accept.Contains("application/vnd.wap.xhtml+xml"))
                {
                    return true;
                }
            }

            // Checks if it has any mobile HTTP headers  

            var xWapProfile = request.ServerVariables["HTTP_X_WAP_PROFILE"];
            var profile = request.ServerVariables["HTTP_PROFILE"];
            var opera = request.Headers["HTTP_X_OPERAMINI_PHONE_UA"];

            if (xWapProfile != null || profile != null || opera != null)
            {
                return true;
            }

            return false;
        }

        public static T FindControl<T>(this System.Web.UI.Control parent, string controlId) where T : System.Web.UI.Control
        {
            var control = parent.FindControl(controlId);
            if (control == null) return null;
            if (control is T)
                return (T)control;
            throw new Exception("The control with the ID of '" + controlId + "' is of type " + control.GetType().FullName + ". It cannot be casted to type " +
                typeof(T).FullName + ".");
        }

        /// <summary>
        /// Determines whether this grid view row is tagged deleted.
        /// </summary>
        public static bool IsTaggedDeleted(this GridViewRow row)
        {
            var status = row.FindControl<HtmlInputHidden>("hfStatus");

            if (status == null)
                throw new InvalidOperationException("This row does not have a hidden field with the ID of 'hdStatus'.");
            else return status.Value == "deleted";
        }

        public static bool ShouldSaveRecord(this GridViewRow row)
        {
            var status = row.FindControl<HtmlInputHidden>("hfStatus");

            if (status == null)
                throw new InvalidOperationException("This row does not have a hidden field with the ID of 'hdStatus'.");
            else return status.Value == "save";
        }

        /// <summary>
        /// Finds the search keywords used by this user on Google that led to the current request.
        /// </summary>
        public static string FindSearchKeyword(this HttpRequest request)
        {
            var urlReferrer = request.UrlReferrer?.ToString();
            if (urlReferrer.IsEmpty()) return null;

            // Note: Only Google is supported for now:

            if (!urlReferrer.ToLower().Contains(".google.co"))
                return null;

            foreach (var possibleQuerystringKey in new[] { "q", "as_q" })
            {
                var query = request.UrlReferrer.Query?.TrimStart("?").Split('&').Trim().FirstOrDefault(p => p.StartsWith(possibleQuerystringKey + "="));

                if (query.HasValue())
                {
                    return HttpUtility.UrlDecode(query.Substring(1 + possibleQuerystringKey.Length));
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the actual IP address of the user considering the Proxy and other HTTP elements.
        /// </summary>
        public static string GetIPAddress(this HttpRequest request)
        {
            string result;

            //if (request.ServerVariables["HTTP_VIA"] != null)
            //{
            //    // using proxy
            //    result = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            //}
            //else
            //{
            //    // real IP
            //    result = request.ServerVariables["REMOTE_ADDR"];
            //}

            //if (result.IsEmpty()) 

            result = request.UserHostAddress.Or("");

            return result;
        }

        #region Private IPs




        /// <summary>
        /// Determines if the given ip address is in any of the private IP ranges
        /// </summary>
        public static bool IsPrivateIp(string address)
        {
            if (address.IsEmpty()) return false;

            var bytes = System.Net.IPAddress.Parse(address).GetAddressBytes();
            if (BitConverter.IsLittleEndian)
            {
                bytes = bytes.Reverse().ToArray();
            }

            var ip = BitConverter.ToUInt32(bytes, 0);

            return PrivateIpRanges.Any(range => range.Contains(ip));
        }

        static readonly Range<uint>[] PrivateIpRanges = new[] {
             //new Range<uint>(0u, 50331647u),              // 0.0.0.0 to 2.255.255.255
             new Range<uint>(167772160u, 184549375u),     // 10.0.0.0 to 10.255.255.255
             new Range<uint>(2130706432u, 2147483647u),   // 127.0.0.0 to 127.255.255.255
             new Range<uint>(2851995648u, 2852061183u),   // 169.254.0.0 to 169.254.255.255
             new Range<uint>(2886729728u, 2887778303u),   // 172.16.0.0 to 172.31.255.255
             new Range<uint>(3221225984u, 3221226239u),   // 192.0.2.0 to 192.0.2.255
             new Range<uint>(3232235520u, 3232301055u),   // 192.168.0.0 to 192.168.255.255
             new Range<uint>(4294967040u, 4294967295u)    // 255.255.255.0 to 255.255.255.255
        };

        /// <summary>
        /// Determines whether this request is initiated from the local network, i.e. its IP starts with "192.168.".
        /// </summary>
        [Obsolete("This is not reliable. Different hosting environments behave differently. Don't use.")]
        public static bool IsLocalNetwork(this HttpRequest request)
        {
            return request.IsLocal || IsPrivateIp(request.GetIPAddress());
        }

        #endregion

        /// <summary>
        /// Writes the specified content wrapped in a DIV tag.
        /// </summary>
        public static void WriteLine(this HttpResponse response, string content)
        {
            response.Write("<div>{0}</div>".FormatWith(content));
        }

        /// <summary>
        /// Redirects the client to the specified URL with a 301 status (permanent).
        /// </summary>
        public static void RedirectPermanent(this HttpResponse response, string permanentUrl)
        {
            RedirectPermanent(response, permanentUrl, endResponse: true);
        }

        /// <summary>
        /// Redirects the client to the specified URL with a 301 status (permanent).
        /// </summary>
        public static void RedirectPermanent(this HttpResponse response, string permanentUrl, bool endResponse)
        {
            response.Status = "301 Moved Permanently";
            response.AddHeader("Location", permanentUrl);
            if (endResponse)
                response.End();
        }

        public static TableRow Add(this TableRowCollection table, params TableCell[] cells)
        {
            var row = new TableRow();
            table.Add(row);
            row.Cells.AddRange(cells.ToArray());

            return row;
        }

        public static TableCell Add(this TableCellCollection row, string cellContent)
        {
            var result = new TableCell { Text = cellContent };

            row.Add(result);

            return result;
        }

        /// <summary>
        /// Returns a flat list of this item plus all items in its hierarchy.
        /// </summary>
        public static IEnumerable<MenuItem> FlattenWithChildren(this MenuItem item)
        {
            return item.ChildItems.Cast<MenuItem>().SelectMany(i => i.FlattenWithChildren()).Concat(item);
        }

        /// <summary>
        /// Removes the specified query string parameter.
        /// </summary>
        public static Uri RemoveEmptyQueryParameters(this Uri url)
        {
            var toRemove = url.GetQueryString().Where(x => x.Value.IsEmpty()).ToList();

            foreach (var item in toRemove) url = url.RemoveQueryString(item.Key);

            return url;
        }

        /// <summary>
        /// Removes the specified query string parameter.
        /// </summary>
        public static Uri RemoveQueryString(this Uri url, string key)
        {
            var qs = url.GetQueryString();
            key = key.ToLower();
            if (qs.ContainsKey(key))
            {
                qs.Remove(key);
            }

            return url.ReplaceQueryString(qs);
        }

        /// <summary>
        /// Removes all query string parameters of this Url and instead adds the specified ones.
        /// </summary>
        public static Uri ReplaceQueryString(this Uri baseUrl, Dictionary<string, string> queryStringDictionary)
        {
            var r = new StringBuilder();

            r.Append(baseUrl.Scheme);

            r.Append("://");

            r.Append(baseUrl.Host);

            if (baseUrl.Port != 80 && baseUrl.Port != 443) r.Append(":" + baseUrl.Port);

            r.Append(baseUrl.AbsolutePath);

            var query = queryStringDictionary.Select(a => "{0}={1}".FormatWith(a.Key, a.Value.UrlEncode())).ToString("&");

            if (query.HasValue())
            {
                r.Append("?");
                r.Append(query);
            }

            return new Uri(r.ToString());
        }

        /// <summary>
        /// Adds the specified list to session state and returns a unique Key for that.
        /// </summary>
        public static string AddList<T>(this System.Web.SessionState.HttpSessionState session, IEnumerable<T> list) where T : IEntity
        {
            return AddList<T>(session, list, TimeSpan.FromHours(1));
        }

        /// <summary>
        /// Adds the specified list to session state and returns a unique Key for that.
        /// </summary>
        public static string AddList<T>(this System.Web.SessionState.HttpSessionState session, IEnumerable<T> list, TimeSpan timeout) where T : IEntity
        {
            var expiryDate = DateTime.Now.Add(timeout);

            var key = "L|" + Guid.NewGuid().ToString() + "|" + expiryDate.ToOADate();
            session[key] = list.Where(x => x != null).Select(a => a.GetId()).ToString("|").Or(string.Empty);

            #region Also delete old ones

            var expiredKeys = session.Keys.Cast<string>().Where(k => k.StartsWith("L|") && k.Split('|').Length == 3 && DateTime.FromOADate(k.Split('|').Last().To<double>()) < DateTime.Now).ToArray();
            expiredKeys.Do(k => session.Remove(k));

            #endregion

            return key;
        }

        /// <summary>
        /// Retrieves a list of objects specified by the session key which is previously generated by Session.AddList() method.
        /// </summary>
        public static IEnumerable<T> GetList<T>(this System.Web.SessionState.HttpSessionState session, string key) where T : Entity
        {
            if (key.IsEmpty())
                throw new ArgumentNullException("key");

            if (key.Split('|').Length != 3)
                throw new ArgumentException("Invalid list key specified. Bar character is expected.");

            var date = key.Split('|').Last().TryParseAs<double>();

            if (date == null)
                throw new ArgumentException("Invalid list key specified. Data after Bar character should be OADate.");

            var ids = session[key] as string;
            if (ids == null)
                throw new TimeoutException("The list with the key " + key + " is expired and removed from the session.");

            return ids.Split('|').Select(i => Database.GetOrDefault<T>(i)).ExceptNull().ToArray();
        }

        /// <summary>
        /// Converts the pager to query string-based links instead of the default post-back-driven model.
        /// </summary>
        public static void ConvertPagerToLink(this GridView gridList, string queryStringID = "pageIndex")
        {
            var request = HttpContext.Current.Request;
            var pageIndexQuery = request[queryStringID].Or("").ToLower();
            gridList.PageIndex = (pageIndexQuery.TryParseAs<int>() ?? 1) - 1;
            if (pageIndexQuery == "last") gridList.PageIndex = int.MaxValue;
            gridList.RowDataBound += (sender, e) =>
            {
                if (e.Row.RowType == System.Web.UI.WebControls.DataControlRowType.Pager)
                {
                    var links = e.Row.Cells[0].Controls[0].Controls[0].Controls.OfType<System.Web.UI.Control>().SelectMany(a => a.Controls.OfType<System.Web.UI.WebControls.LinkButton>()).ToArray();
                    var webRoot = request.Url.GetWebsiteRoot();
                    var url = new Uri(webRoot + HttpContext.Current.Items["ORIGINAL.REQUEST.PATH"].ToStringOrEmpty().Or(request.Url.ToString()).TrimStart("/"));
                    var navigationUrlFormat = url.RemoveQueryString(queryStringID).PathAndQuery.TrimStart(webRoot).TrimEnd("?");
                    navigationUrlFormat += (navigationUrlFormat.Contains("?") ? "&" : "?") + queryStringID + "={0}";
                    links.Do(link =>
                    {
                        link.Visible = false;

                        var command = link.CommandArgument.ToLower();
                        if (command == "prev") command = Math.Max(gridList.PageIndex, 1).ToString();
                        else if (command == "next") command = (gridList.PageIndex + 2).ToString();

                        link.Parent.Controls.Add("<a href='{0}' class='pager-link'>{1}</a>".FormatWith(navigationUrlFormat.FormatWith(command), link.Text));
                    });
                }
            };
        }

        /// <summary>
        /// Runs the parallel select in the current HTTP context.
        /// </summary>
        public static ParallelQuery<TResult> SelectInHttpContext<TSource, TResult>(this ParallelQuery<TSource> list, Func<TSource, TResult> selector)
        {
            var httpContext = HttpContext.Current;

            return list.Select(x => { HttpContext.Current = httpContext; return selector(x); });
        }

        public static IEnumerable<string> GetSelectedValues(this CheckBoxList list)
        {
            foreach (ListItem item in list.Items)
                if (item.Selected)
                    yield return item.Value;
        }

        public static int GenerationGap(this SiteMapNode parent, SiteMapNode child)
        {
            int result = 0;

            for (; child != parent && child != null; child = child.ParentNode)
                result++;

            if (child == null) return int.MaxValue;
            else return result;
        }

        public static SiteMapNode FindByKey(this SiteMapProvider provider, string key)
        {
            if (key.IsEmpty()) return null;

            if (HttpContext.Current != null)
                // The standard implementation requires HttpContext.
                return provider.RootNode.GetAllNodes().OfType<SiteMapNode>().FirstOrDefault(n => n.ResourceKey == key);
            else
            {
                var root = AppDomain.CurrentDomain.GetPath("Web.SiteMap").AsFile().Get(x => x.Exists() ? x : null)
                    .Get(x => x.ReadAllText().To<XDocument>().Root);

                if (root != null) root.RemoveNamespaces();

                var node = root?.Descendants().OrEmpty().FirstOrDefault(x => x.GetValue<string>("@resourceKey") == key);
                if (node == null) return null;

                return new SiteMapNode(provider, key, node.GetValue<string>("@url"), node.GetValue<string>("@title"),
                    node.GetValue<string>("@description"));
            }
        }

        /// <summary>
        /// Iterator recursively returning each role in the current nodes and all of it's ancestor roles.
        /// </summary>
        public static IEnumerable<string> InheritedRoles(this SiteMapNode node)
        {
            foreach (var role in node.Roles)
            {
                yield return role.ToString();
            }

            if (node.ParentNode == null) yield break;

            foreach (var role in node.ParentNode.InheritedRoles())
            {
                yield return role;
            }
        }

        /// <summary>
        /// Overloads the SiteMapNode->IsAccessibleToUser method to test if the given User can access a particular page.
        /// </summary>
        public static bool IsAccessibleToUser(this SiteMapNode node, IUser user)
        {
            var userRoles = user.GetRoles().ToHashSet();

            var inRole = node.InheritedRoles().Any(userRoles.Contains);

            return inRole;
        }

        public static T Find<T>(this Control parent, string controlId) where T : Control
        {
            return parent.FindControl(controlId) as T;
        }

        public static string GetSetting(this SiteMapNode node, string key)
        {
            return GetSetting(node, key, includingInheritedSettings: true);
        }

        public static string GetSetting(this SiteMapNode node, string key, bool includingInheritedSettings)
        {
            if (node == null || node.ParentNode == null) return null;

            var settings = node.GetSettings();

            if (settings.ContainsKey(key))
            {
                return settings[key];
            }
            else if (includingInheritedSettings)
            {
                return GetSetting(node.ParentNode, key, includingInheritedSettings: true);
            }
            else
            {
                return null;
            }
        }

        public static Dictionary<string, string> GetSettings(this SiteMapNode node)
        {
            if (node.Description.IsEmpty()) return new Dictionary<string, string>();
            else
            {
                var result = new Dictionary<string, string>();
                foreach (var setting in node.Description.Split('|'))
                {
                    try
                    {
                        var key = setting.Split('=')[0];
                        var value = setting.Split('=')[1];
                        value = value.Replace("$#BAR#$", "|").Replace("$#EQUALS#$", "=");
                        result.Add(key, value);
                    }
                    catch (Exception ex)
                    {
                        throw new FormatException("Could not extract the site map node settings from {" + setting + "}.", ex);
                    }
                }

                return result;
            }
        }

        public static IEnumerable<SiteMapNode> ParentsHierarchy(this SiteMapNode node)
        {
            for (SiteMapNode parent = node; parent != null; parent = parent.ParentNode)
                yield return parent;
        }

        public static DataControlField FindColumn(this GridView gridList, string headerText)
        {
            return FindColumns(gridList, headerText).FirstOrDefault();
        }

        public static IEnumerable<DataControlField> FindColumns(this GridView gridList, string headerText)
        {
            foreach (DataControlField c in gridList.Columns)
                if (c.HeaderText == headerText)
                    yield return c;
        }

        /// <summary>
        /// Determines if the specified argument exists in the request (query string or form).
        /// </summary>
        public static bool Has(this HttpRequest request, string argument) => request[argument].HasValue();

        /// <summary>
        /// Determines whether the scheme of the current request is HTTPS.
        /// </summary>
        public static bool IsHttps(this HttpRequest request) => request.Url.Scheme.ToLower() == "https";

        /// <summary>
        /// Gets the root of the requested website.
        /// </summary>
        public static string GetWebsiteRoot(this Uri requestUrl)
        {
            var result = requestUrl.Scheme + "://" + requestUrl.DnsSafeHost;
            if (requestUrl.Port != 80 && requestUrl.Port != 443) result += ":" + requestUrl.Port;
            result += "/";
            return result;
        }

        /// <summary>
        /// Gets the absolute URL for a specified relative url.
        /// </summary>
        public static string GetAbsoluteUrl(this HttpRequest request, string relativeUrl)
        {
            return request.Url.GetWebsiteRoot() + relativeUrl.TrimStart("/");
        }

        /// <summary>
        /// Downloads the text in this URL.
        /// </summary>
        public static string Download(this Uri url, string cookieValue = null, int timeOut = 60000)
        {
            var request = (HttpWebRequest)HttpWebRequest.Create(url);

            request.Timeout = timeOut;

            if (cookieValue.HasValue())
            {
                request.CookieContainer = new CookieContainer();
                request.CookieContainer.SetCookies(url, cookieValue.OrEmpty());
            }

            using (var response = request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    return stream.ReadAllText();
                }
            }
        }

        /// <summary>
        /// Downloads the data in this URL.
        /// </summary>
        public static byte[] DownloadData(this Uri url, string cookieValue = null, int timeOut = 60000)
        {
            var request = (HttpWebRequest)HttpWebRequest.Create(url);

            request.Timeout = timeOut;

            if (cookieValue.HasValue())
            {
                request.CookieContainer = new CookieContainer();
                request.CookieContainer.SetCookies(url, cookieValue.OrEmpty());
            }

            using (var response = request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    return stream.ReadAllBytes();
                }
            }
        }

        /// <summary>
        /// Downloads the data in this URL.
        /// </summary>
        public static Document DownloadDocument(this Uri url, string cookieValue = null, int timeOut = 60000)
        {
            var fileName = "File.Unknown";

            if (url.IsFile)
            {
                fileName = url.ToString().Split('/').Last();
            }

            return new Document(url.DownloadData(cookieValue, timeOut), fileName);
        }

        /// <summary>
        /// Reads all text in this stream as UTF8.
        /// </summary>
        public static string ReadAllText(this Stream response)
        {
            string result = "";

            // Pipes the stream to a higher level stream reader with the required encoding format. 
            using (var readStream = new StreamReader(response, Encoding.UTF8))
            {
                var read = new char[256];
                // Reads 256 characters at a time.
                int count = readStream.Read(read, 0, 256);

                while (count > 0)
                {
                    // Dumps the 256 characters on a string and displays the string to the console.
                    result += new string(read, 0, count);

                    count = readStream.Read(read, 0, 256);
                }
            }

            return result;
        }

        ///// <summary>
        ///// Removes a specified querystring key from this Uri.
        ///// </summary>
        //public static string RemoveQueryString(this Uri requestUrl, string key)
        //{
        //    var fullUrl = requestUrl.PathAndQuery;

        //    if (requestUrl.Query.HasValue())
        //    {
        //        var query = fullUrl.Substring(fullUrl.IndexOf("?") + 1);

        //        var modifiedQuery = query.Split('&').Where(kv => !kv.StartsWith(key + "=")).ToString("&");

        //        return fullUrl.TrimEnd(query.Length) + modifiedQuery;
        //    }
        //    else return fullUrl;
        //}


        /// <summary>
        /// Returns the control on the page that caused the page to post back.
        /// </summary>
        public static Control GetPostBackControl(this Page page)
        {
            if (page.Request == null) return null;

            var id = page.Request.Params["__EVENTTARGET"];
            if (id.HasValue())
            {
                return page.FindControl(id);
            }
            else
            {
                if (page.Request.Form == null) return null;

                foreach (var key in page.Request.Form.AllKeys)
                {
                    var searchableKey = key;
                    if (searchableKey.HasValue() && searchableKey.EndsWith(".x"))
                    {
                        // Image button:
                        searchableKey = searchableKey.TrimEnd(2);
                    }

                    if (searchableKey.IsEmpty()) continue;

                    var control = page.FindControl(searchableKey);
                    if (control is Button || control is ImageButton) return control;
                }

                return null;
            }
        }


        public static void AddRange(this MenuItemCollection itemsCollection, IEnumerable<MenuItem> items)
        {
            foreach (var item in items)
                itemsCollection.Add(item);
        }


        /// <summary>
        /// Restarts the currently running web application.
        /// </summary>
        public static void Restart(this System.Web.HttpApplicationState application)
        {
            var webConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Web.config");

            if (!File.Exists(webConfig))
                throw new Exception("HttpApplication.Restart() is only available in website context. The current AppDomain has no Web.config file in the root.");

            File.SetLastWriteTimeUtc(webConfig, DateTime.UtcNow);

            //using (var writer = File.AppendText(webConfig))
            //{
            //    writer.Write(' ');
            //}

            //var body = File.ReadAllText(webConfig);
            //if (body.EndsWith(Environment.NewLine))
            //    body = body.Trim();
            //else body = body + Environment.NewLine;

            //webConfig.AsFile().WriteAllText(body);
        }

        public static string GetRelativePath(this HttpRequest request)
        {
            var path = request.Path;

            if (request.ApplicationPath != "/")
            {
                // Virtual directory:
                path = path.Substring(request.ApplicationPath.Length);
            }

            return path;
        }

        /// <summary>
        /// Gets the GridViewRow of this command.
        /// </summary>
        public static GridViewRow GetRow(this GridViewCommandEventArgs args)
        {
            return (args.CommandSource as Control)?.FindParent<GridViewRow>();
        }

        /// <summary>
        /// Returns the translation of this object's string representation.
        /// </summary>
        public static string ToString(this IEntity instance, ILanguage language)
        {
            if (language == null)
                throw new ArgumentNullException("language");

            return Translator.Translate(instance.ToString(), language);
        }

        /// <summary>
        /// Adds the range of specified entities translated in the specified language.
        /// </summary>
        public static void AddRange<T>(this ListItemCollection items, IEnumerable<T> objects, ILanguage language) where T : IEntity
        {
            foreach (T o in objects)
                items.Add(new ListItem(o.ToString(language), o.GetId().ToString()));
        }

        /// <summary>
        /// Gets the Html Encoded version of this text.
        /// </summary>
        public static string HtmlEncode(this string text)
        {
            if (text.IsEmpty())
                return string.Empty;

            return HttpUtility.HtmlEncode(text);
        }

        /// <summary>
        /// Gets the Html Decoded version of this text.
        /// </summary>
        public static string HtmlDecode(this string text)
        {
            if (text.IsEmpty()) return string.Empty;

            return HttpUtility.HtmlDecode(text);
        }

        /// <summary>
        /// Gets the Url Encoded version of this text.
        /// </summary>
        public static string UrlEncode(this string text)
        {
            if (text.IsEmpty()) return string.Empty;

            return HttpUtility.UrlEncode(text);
        }

        /// <summary>
        /// Gets the Url Decoded version of this text.
        /// </summary>
        public static string UrlDecode(this string text)
        {
            if (text.IsEmpty())
                return string.Empty;

            return HttpUtility.UrlDecode(text);
        }

        /// <summary>
        /// Properly sets a query string key value in this Uri, returning a new Uri object.
        /// </summary>
        public static Uri SetQueryString(this Uri uri, string key, object value)
        {
            var valueString = string.Empty;

            if (value != null)
            {
                if (value is IEntity)
                {
                    valueString = (value as IEntity).GetId().ToString();
                }
                else
                {
                    valueString = value.ToString();
                }
            }

            var pairs = HttpUtility.ParseQueryString(uri.Query);

            pairs[key] = valueString;

            var builder = new UriBuilder(uri) { Query = pairs.ToString() };

            return builder.Uri;
        }
    }
}