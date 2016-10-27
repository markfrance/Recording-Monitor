namespace System
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Web.UI.WebControls;
    using MSharp.Framework;

    partial class MSharpExtensions
    {
        public static void Add(this ListItemCollection items, IEntity entity)
        {
            items.Add(new ListItem(entity.ToString(), entity.GetId().ToString()));
        }

        public static ListItem Find(this ListItemCollection items, IEntity entity)
        {
            if (entity == null) return null;
            else
                try { return items.FindByValue(entity.GetId().ToString()); }
                catch { return null; }
        }

        public static void AddRange<T>(this ListItemCollection listItems, IEnumerable<T> items, Func<T, string> displayExpression) where T : IEntity
        {
            foreach (var item in items)
                listItems.Add(new ListItem(displayExpression?.Invoke(item), item.GetId().ToString()));
        }

        public static void AddRange<T>(this ListItemCollection listItems, IEnumerable<T> items, T selectedItem, Func<T, string> displayExpression) where T : IEntity
        {
            foreach (var item in items)
            {
                if (item == null) continue;

                listItems.Add(new ListItem(displayExpression?.Invoke(item), item.GetId().ToString()) { Selected = item.Equals(selectedItem) });
            }
        }

        public static void AddRange<T>(this ListItemCollection listItems, IEnumerable<T> items, Func<T, string> displayExpression, Func<T, string> valueExpression)
        {
            foreach (var item in items)
                listItems.Add(new ListItem(displayExpression?.Invoke(item), valueExpression?.Invoke(item)));
        }

        public static void AddRange(this ListItemCollection listItems, IEnumerable items)
        {
            AddRange(listItems, items, (IEnumerable)null);
        }

        public static void AddRange(this ListItemCollection listItems, IEnumerable items, IEntity selectedItem)
        {
            AddRange(listItems, items, new[] { selectedItem });
        }

        public static void AddRange<T>(this ListItemCollection listItems, IEnumerable<T> items, IEnumerable<T> selectedItems, Func<T, string> displayExpression) where T : IEntity
        {
            foreach (var item in items)
            {
                if (item == null) continue;

                listItems.Add(new ListItem(displayExpression?.Invoke(item), item.GetId().ToString())
                {
                    Selected = selectedItems != null && selectedItems.Contains(item)
                });
            }
        }

        public static void AddRange(this ListItemCollection listItems, IEnumerable items, IEnumerable selectedItems)
        {
            var selected = selectedItems == null ? null : selectedItems.OfType<object>();

            foreach (var item in items)
            {
                if (item == null) continue;
                else if (item is IEntity)
                    listItems.Add((IEntity)item);
                else if (item is ListItem)
                    listItems.Add((ListItem)item);
                else listItems.Add(item.ToString());

                if (selected != null && selected.Contains(item))
                    listItems[listItems.Count - 1].Selected = true;
            }
        }

        /// <summary>
        /// Gets the selected object on this list control or returns null if no object exists for selected item.
        /// </summary>
        public static T GetSelectedOrDefault<T>(this ListControl list) where T : IEntity
        {
            if (list.Items.Count == 0) return default(T);

            var selectedId = list.SelectedValue;

            if (selectedId.IsEmpty() || selectedId == Guid.Empty.ToString())
                return default(T);

            return Database.GetOrDefault<T>(selectedId);
        }

        public static void Add(this ListItemCollection items, string text, string id)
        {
            items.Add(new ListItem(text, id));
        }

        public static void AddRange<T>(this ListItemCollection items) where T : IEntity
        {
            items.AddRange((IEnumerable)Database.GetList<T>());
        }

        public static void AddRange<T>(this ListItemCollection items, Expression<Func<T, bool>> criteria) where T : IEntity
        {
            items.AddRange((IEnumerable)Database.GetList<T>(criteria));
        }

        public static void AddRange(this ListItemCollection items, ListItemCollection otherItems)
        {
            if (otherItems == null)
                throw new ArgumentNullException("otherItems");

            foreach (ListItem each in otherItems)
                items.Add(each);
        }

        public static void Add(this ListItemCollection items, string text, Guid id)
        {
            items.Add(new ListItem(text, id.ToString()));
        }

        public static void RemoveByValue(this ListItemCollection items, string value)
        {
            var item = items.FindByValue(value);
            if (item != null)
                items.Remove(item);
        }

    }
}
