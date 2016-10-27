namespace System.Linq
{
    using System.Collections;
    using System.Reflection;

    internal class PropertyComparer : IComparer
    {
        private PropertyInfo Property;

        public PropertyComparer(PropertyInfo property)
        {
            this.Property = property;
        }

        public K ExtractValue<T, K>(T item)
        {
            return (K)Property.GetValue(item, null);
        }

        public int Compare(object first, object second)
        {
            return Comparer.Default.Compare(Property.GetValue(first, null), Property.GetValue(second, null));
        }
    }
}
