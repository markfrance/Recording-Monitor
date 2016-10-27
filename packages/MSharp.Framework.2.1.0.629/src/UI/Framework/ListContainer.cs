namespace MSharp.Framework.UI
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Web;
    using System.Web.UI.WebControls;
    using MSharp.Framework.Data;

    public abstract class ListContainer<T> : UserControl where T : IEntity
    {
        /// <summary>
        /// Gets the filters added for search.
        /// </summary>
        protected List<Expression<Func<T, bool>>> Filters = new List<Expression<Func<T, bool>>>();

        /// <summary>
        /// Represents the current datasource item in the list.
        /// </summary>
        protected T Item;

        #region Workaround for ASP.NET postback issue

        protected HiddenField SelectedListItemIdHolder = new HiddenField { ID = "SelectedListItemIdHolder" };

        /// <summary>
        /// Gets the item on which the current command is executing.
        /// </summary>
        protected virtual T GetSelectedItem()
        {
            var id = Request.Form[SelectedListItemIdHolder.UniqueID];
            if (id.IsEmpty()) return default(T);
            return Database.Get<T>(id);
        }

        protected virtual string SetPostBackCommandItem(T item)
        {
            return SelectedListItemIdHolder.ForBrowser() + ".value = '" + (item as dynamic).ID + "';";
        }

        #endregion

        /// <summary>
        /// Raises the <see cref="E:PreRender"/> event.
        /// </summary>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            // Storeave the filter criteria of the list in Page's viewstate.
            // This enables post-back events such as sorting and paging to operate on the correct data source.
            ViewState["FilterCriteria"] = FilterCriteria.Select(c => c.ToString()).ToArray();

            GetGridList().Perform(g => g.Attributes["CurrentSort"] = SortExpression);
        }

        /// <summary>
        /// Gets the name of the Selected Columns cookie.
        /// </summary>
        protected virtual string GetSelectedColumnsCookieName()
        {
            return SiteMap.CurrentNode.Get(n => n.ResourceKey + ">") + ClientID + "|" + GetType().Name + ".Selected.Columns";
        }

        /// <summary>
        /// Gets the name of the sort expression cookie key.
        /// </summary>
        protected virtual string GetSortExpressionKey()
        {
            return SiteMap.CurrentNode.Get(n => n.ResourceKey + ">") + ClientID + "|" + GetType().Name;
        }

        #region Filter Criteria
        List<Criterion> _FilterCriteria;
        protected List<Criterion> FilterCriteria
        {
            get
            {
                if (_FilterCriteria == null)
                {
                    _FilterCriteria = new List<Criterion>();
                    if (ViewState["FilterCriteria"] != null)
                        foreach (string c in (IEnumerable)ViewState["FilterCriteria"])
                            _FilterCriteria.Add(Criterion.Parse(c));
                }
                return _FilterCriteria;
            }
        }

        /// <summary>
        /// Adds a filter to the search conditions.
        /// </summary>
        public void AddFilter(Expression<Func<T, bool>> criterion)
        {
            if (!Filters.Contains(criterion))
                Filters.Add(criterion);

            FilterCriteria.Add(Criterion.From(criterion));

            _DataSource = null;
        }



        /// <summary>
        /// Clears all filters from this list.
        /// </summary>
        public void ClearFilters()
        {
            Filters.Clear();
            FilterCriteria.Clear();
        }

        #endregion

        #region SortExpression property

        /// <summary>
        /// Gets or sets the current sort expression of this module.                
        /// </summary>
        public string SortExpression
        {
            get
            {
                return GetSortExpressionProperty().Get();

            }
            set
            {
                //new SortExpressionProperty(ViewState, GetGridList() as GridView, GetSortExpressionKey(), IsPostBack).Set(value);
                GetSortExpressionProperty().Set(value);
            }
        }

        SortExpressionProperty GetSortExpressionProperty()
        {
            return new SortExpressionProperty(ViewState, GetGridList() as GridView, GetSortExpressionKey(), IsPostBack);
        }

        /// <summary>
        /// Resets the sort expression to the specified value.
        /// </summary>
        public void ResetSortExpression(string sortExpression)
        {
            new SortExpressionProperty(ViewState, GetGridList() as GridView, GetSortExpressionKey(), IsPostBack).SetDirect(sortExpression);
        }

        #endregion

        /// <summary>
        /// Gets the filter criteria of this list.
        /// </summary>
        protected virtual IEnumerable<Criterion> GetFilterCriteria()
        {
            return new List<Criterion>(FilterCriteria);
        }

        protected virtual IEnumerable<Func<T, bool>> GetFilters()
        {
            return Filters.Select(f => f.Compile());
        }

        /// <summary>
        /// Gets the query options to use with the database query.
        /// </summary>
        protected virtual IEnumerable<QueryOption> GetQueryOptions()
        {
            yield break;
        }

        protected virtual IEnumerable<T> GetBaseDataSource()
        {
            return Database.GetList<T>(GetFilterCriteria(), GetQueryOptions().ToArray());
        }

        protected IEnumerable<T> _DataSource;
        protected virtual IEnumerable<T> GetDataSource()
        {
            if (_DataSource == null)
            {
                try { _DataSource = GetBaseDataSource(); }
                catch (Exception ex)
                {
                    throw new Exception("Loading the list data source failed.", ex);
                }

                try { SortDataSource(); }
                catch (Exception ex)
                {
                    if (ex.Message.Or("").Contains(" is not a readable property of ") && Request.HttpMethod == "GET")
                    {
                        // Default sort from Cookie is invalid as the referenced property does not exist any more.
                    }
                    else
                    {
                        throw new Exception("Sorting data source failed.", ex);
                    }
                }
            }

            if (_DataSource == null)
            {
                throw new Exception("_DataSource is Null for " + GetType().FullName + ": " + ID);
            }
            else if (!(_DataSource is List<T>))
            {
                _DataSource = _DataSource.ToList();
            }

            return _DataSource;
        }

        ///// <summary>
        ///// Binds a data source to the invoked server control and all its child controls.
        ///// </summary>
        //public override void DataBind()
        //{
        //    PopulateList();

        //    base.DataBind();
        //}

        protected virtual void List_PageIndexChanging(object sender, System.Web.UI.WebControls.GridViewPageEventArgs e)
        {
            var grid = sender as GridView;
            grid.PageIndex = e.NewPageIndex;
            grid.DataBind();
        }

        protected virtual void List_Sorting(object sender, System.Web.UI.WebControls.GridViewSortEventArgs e)
        {
            var grid = sender as GridView;

            if (grid == null)
                throw new NotSupportedException("Standard sorting is supported only in GridViews.");

            // Sort command will always start at the first page:
            grid.PageIndex = 0;

            // Update the module's new "Sort Expression" in ViewState so it is maintained in post backs.
            SortExpression = e.SortExpression;

            // Make sure the clicked sort column is now changed to DESC (or if it was DESC, it is now ASC):
            var currentSortColumn = grid.Columns.Cast<DataControlField>().FirstOrDefault(c => c.SortExpression == e.SortExpression);
            if (currentSortColumn != null)
            {
                if (e.SortExpression.EndsWith(" DESC"))
                    currentSortColumn.SortExpression = e.SortExpression.TrimEnd(" DESC");
                else currentSortColumn.SortExpression += " DESC";
            }

            // Apply the sort logic:
            SortDataSource();

            // Set the GridView's new data source and bind it:
            grid.DataSource = FindGridSource(grid);
            grid.DataBind();
        }

        IEnumerable<T> FindGridSource(GridView grid)
        {
            if (grid.Parent is DataListItem)
            {
                // Grouping:                
                var group = GetGroups().Cast<object>().ElementAt((grid.Parent as DataListItem).ItemIndex);
                return GetGroup(group);
            }
            else return GetDataSource();
        }

        /// <summary>
        /// Gets the groups of the currently selected grouping expression.
        /// </summary>
        protected virtual IEnumerable GetGroups()
        {
            throw new NotImplementedException("GetGroups() is not implemented in " + GetType().FullName);
        }

        /// <summary>
        /// Gets the items that fall into a specified group.
        /// </summary>
        protected virtual IEnumerable<T> GetGroup(object groupValue)
        {
            throw new NotImplementedException("GetGroup() is not implemented in " + GetType().FullName);
        }

        protected virtual void SortDataSource()
        {
            if (SortExpression.HasValue())
            {
                if (SortExpression.HasValue())
                {
                    try
                    {
                        if (SortExpression.EndsWith(" DESC"))
                        {
                            _DataSource = GetDataSource()
                                          .OrderByDescending(SortExpression.TrimEnd(" DESC".Length))
                                          .ToList();
                        }
                        else
                        {
                            _DataSource = GetDataSource().OrderBy(SortExpression).ToList();
                        }
                    }
                    catch // A property which does not exist anymore.
                    {
                        // Let's reset everything
                        MSharp.Framework.Services.CookieProperty.Remove("MSharp.Framework.UI.Grid.SortExpressions");
                        ViewState["Current.Sort.Expression"] = null;

                        // Better luck next time
                        _DataSource = GetDataSource().ToList();
                    }
                }
            }
        }

        /// <summary>
        /// Adds search filter expressions to this list's data source.
        /// </summary>
        protected virtual void AddSearchFilters()
        {
        }

        public virtual void PopulateList()
        {
            try
            {
                FilterCriteria.Clear();
                AddSearchFilters();

                if (ListControl == null)
                {
                    throw new Exception("Populate list must be overridden when no static grid exists in the list container context.");
                }

                ListControl.DataSource = GetDataSource();
            }
            catch (Exception ex)
            {
                throw new Exception("Could not load or bind the list data source.", ex);
            }
        }

        public virtual void Reload()
        {
            _DataSource = null;

            PopulateList();
            DataBind();
        }




        protected DataControlField GetSelectedGridColumn()
        {
            var grid = ListControl as GridView;
            if (grid == null) return null;
            if (SortExpression.IsEmpty()) return null;

            return grid.Columns.Cast<DataControlField>().FirstOrDefault(x => x.SortExpression == SortExpression);
        }

        protected override void OnInit(EventArgs e)
        {
            if (ListControl != null)
            {
                ListControl.Parent.Controls.Add(SelectedListItemIdHolder);
            }
            else
            {
                Controls.Add(SelectedListItemIdHolder);
            }

            base.OnInit(e);

            InitializeGridView();
            InitializeListView();
        }

        void InitializeGridView()
        {
            var grid = ListControl as GridView;
            if (grid == null) return;

            grid.RowCreated += (sender, args) => Item = (T)args.Row.DataItem;

            // Fix Sort of selected column:
            if (IsPostBack || SortExpression.IsEmpty()) return;

            var selectedColumn = GetSelectedGridColumn();
            if (selectedColumn != null)
            {
                if (SortExpression.EndsWith(" DESC"))
                {
                    selectedColumn.SortExpression = SortExpression.TrimEnd(" DESC");
                }
                else
                {
                    selectedColumn.SortExpression += " DESC";
                }
            }
        }

        void InitializeListView()
        {
            var listView = ListControl as ListView;
            if (listView == null) return;

            listView.ItemCreated += (s, e) =>
                {
                    if (e.Item.ItemType == ListViewItemType.DataItem)
                    {
                        var dataItem = e.Item as ListViewDataItem;
                        if (dataItem == null)
                            Item = GetDataItem(s as ListView, e.Item);
                        else Item = (T)dataItem.DataItem;
                    }
                };
        }


        protected T GetDataItem(ListViewItem item)
        {
            return GetDataItem(ListControl as ListView, item);
        }

        protected T GetDataItem(ListView sender, ListViewItem item)
        {
            if (_DataSource == null || item.ItemType == ListViewItemType.EmptyItem)
                return default(T);

            //    var asPagable = sender as IPageableItemContainer;

            var itemIndex = (ListControl as ListView).Controls[0].Controls.Count - 2;

            //itemIndex += StartRowIndex;

            return _DataSource.ElementAt(itemIndex);
        }

        private DataBoundControl ListControl => GetGridList();

        protected virtual DataBoundControl GetGridList() { return FindControl("gridList") as DataBoundControl; }
    }
}