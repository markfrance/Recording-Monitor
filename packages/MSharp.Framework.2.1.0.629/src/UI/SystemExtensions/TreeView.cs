namespace System
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.UI;
    using System.Web.UI.HtmlControls;
    using System.Web.UI.WebControls;
    using MSharp.Framework;
    using MSharp.Framework.Services;

    partial class MSharpExtensions
    {
        /// <summary>
        /// Converts the grid view to a tree view by adding some spacer and a collapse icon
        /// to the left side of a web control that has attribute [IsTreeNodeAnchor='true'].
        /// In case there is not such control the first beginning of the row will be chosen.
        /// </summary>
        /// <param name="gridView">The grid view.</param>
        public static void MakeTreeView(this GridView gridView, Func<IHierarchy, bool> isItemCollapsed)
        {
            gridView.RowCreated += (sender, args) =>
            {
                if (args.Row.RowType != DataControlRowType.DataRow)
                    return;

                var item = args.Row.DataItem as IHierarchy;
                if (item == null)
                    throw new Exception("Can not convert this grid view to a treeview. The item assigned to row number " + args.Row.RowIndex.ToString() + " is not of type IHierarchy");

                args.Row.ConfigureTreeViewNode(item, isItemCollapsed);
            };
        }

        static void ConfigureTreeViewNode(this GridViewRow row, IHierarchy item, Func<IHierarchy, bool> isItemCollapsed)
        {
            var parent = item.GetParent();

            var isVisible = true;
            if (parent != null)
            {
                isVisible = !isItemCollapsed(parent);
            }

            var isCollapsed = isItemCollapsed(item);
            var isRoot = parent == null;
            var isLeaf = item.GetChildren().None();

            // setting up grid view row attributes
            row.Attributes["itemid"] = item.GetId().ToString();
            row.CssClass += " {0}{1}{2}{3}".FormatWith(
                item.GetAllParents().Select(a => a.GetId()).ToString(" "),
                " treeview-leaf-node".OnlyWhen(isLeaf),
                " treeview-root-node".OnlyWhen(isRoot),
                " collapsed".OnlyWhen(isCollapsed && !isLeaf));

            if (!isVisible)
            {
                row.Style["display"] = "none";
                row.Attributes["collapsedfor"] = parent.GetId().ToString();
            }

            // Creating additional controls
            var spacerSpan = CreateSpacer(item.GetAllParents().Count());
            var collapseIcon = CreateLink(item.GetId().ToString());

            // putting additional controls in the right place
            var anchorControl = FindAnchorControl(row);

            var parentToAdd = anchorControl?.Parent ?? row.Cells[0];
            var controlIndex = anchorControl == null ? 0 : parentToAdd.Controls.IndexOf(anchorControl);

            parentToAdd.Controls.AddAt(controlIndex, collapseIcon);
            parentToAdd.Controls.AddAt(controlIndex, spacerSpan);
        }

        static Control CreateSpacer(int count)
        {
            return new HtmlGenericControl("span")
            {
                InnerHtml = Enumerable.Repeat("<span class='spacer'>&nbsp;</span>", count).ToString(" ")
            };
        }

        static Control CreateLink(string id)
        {
            var collapseIcon = new HtmlAnchor
            {
                InnerHtml = "&nbsp;",
                HRef = "javascript:collapseExpandTreeViewNode('{0}');".FormatWith(id)
            };
            collapseIcon.Attributes["class"] = "treeview-node-icon";
            return collapseIcon;
        }

        static Control FindAnchorControl(GridViewRow row)
        {
            Control anchorControl = row.GetAllChildren().OfType<WebControl>().FirstOrDefault(c => c.Attributes["IsTreeNodeAnchor"] != null && c.Attributes["IsTreeNodeAnchor"].ToLower() == "true");
            if (anchorControl == null)
                anchorControl = row.GetAllChildren().OfType<HtmlControl>().FirstOrDefault(c => c.Attributes["IsTreeNodeAnchor"] != null && c.Attributes["IsTreeNodeAnchor"].ToLower() == "true");

            return anchorControl;
        }

        /// <summary>
        /// Converts the grid view to a tree view by adding some spacer and a collapse icon
        /// to the left side of a web control that has attribute [IsTreeNodeAnchor='true'].
        /// In case there is not such control the first beginning of the row will be chosen.
        /// </summary>
        /// <param name="gridView">The grid view.</param>        
        public static void MakeTreeView(this GridView gridView, bool collapsed = false)
        {
            MakeTreeView(gridView, a => collapsed);
            //gridView.RowCreated += (sender, args) =>
            //{
            //    if (args.Row.RowType != DataControlRowType.DataRow)
            //        return;

            //    var item = args.Row.DataItem as IHierarchy;
            //    if (item == null)
            //    {
            //        throw new Exception("Can not convert this grid view to a treeview. The item assigned to row number " + args.Row.RowIndex.ToString() + " is not of type IHierarchy");
            //    }
            //    args.Row.ConfigureTreeViewNode(item, collapsed);
            //};
        }

        /// <summary>
        /// Adds a hierarchy of nodes for a specified hirarchical item.
        /// </summary>
        public static void Add(this TreeNodeCollection nodes, IHierarchy item)
        {
            var node = new TreeNode(item.Name, item.GetId().ToString());

            foreach (var child in item.GetChildren())
                node.ChildNodes.Add(child);

            node.CollapseAll();
            node.SelectAction = TreeNodeSelectAction.None;

            nodes.Add(node);
        }

        /// <summary>
        /// Adds a hierarchy of nodes for the specified hirarchical items.
        /// </summary>
        public static void Add<T>(this TreeNodeCollection nodes, IEnumerable<T> items) where T : IHierarchy
        {
            foreach (var item in items)
                Add(nodes, item);
        }

        /// <summary>
        /// Gets the data item of this node.
        /// ID of the required object will be read from the Value of this node, and then the object of the specified type will be fetched from the database.
        /// </summary>
        public static T GetDataItem<T>(this TreeNode node) where T : IEntity
        {
            return Database.Get<T>(node.Value);
        }

        /// <summary>
        /// Finds a node in this treeview with its Value equal to the specified object's id.
        /// </summary>
        public static TreeNode FindNode(this TreeView tree, IEntity entity)
        {
            if (entity == null) return null;

            return tree.GetAllChildren().FirstOrDefault(n => n.Value == entity.GetId().ToString());
        }

        /// <summary>
        /// Gets the selected object IDs in this tree view.
        /// </summary>
        public static IEnumerable<Guid> GetSelectedIds(this TreeView tree)
        {
            return tree.GetAllChildren().Where(n => n.Checked).Select(n => new Guid(n.Value));
        }


        /// <summary>
        /// Retreives a list of all the TreeNode objects that are descendants of this calling node, regardless of their degree of separation.
        /// </summary>
        public static IEnumerable<TreeNode> GetAllChildren(this TreeNode root)
        {
            var nodes = root.ChildNodes.Cast<TreeNode>().ToList();

            foreach (TreeNode c in root.ChildNodes)
                nodes.AddRange(c.GetAllChildren());

            return nodes;
        }

        /// <summary>
        /// Retreives a list of all the TreeNode objects that directly or indirectly children of this tree, regardless of their degree of separation.
        /// </summary>
        public static IEnumerable<TreeNode> GetAllChildren(this TreeView tree)
        {
            var nodes = tree.Nodes.Cast<TreeNode>().ToList();

            foreach (TreeNode c in tree.Nodes)
            {
                nodes.AddRange(c.GetAllChildren());
            }
            return nodes;
        }
    }
}