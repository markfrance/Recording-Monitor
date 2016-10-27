namespace System
{
    using MSharp.Framework;

    partial class MSharpExtensions
    {
        /// <summary>
        /// Shortens this GUID.
        /// </summary>
        public static ShortGuid Shorten(this Guid guid)
        {
            return new ShortGuid(guid);
        }

        /// <summary>
        /// This will use Database.Get() to load the specified entity type with this ID.
        /// </summary>
        public static T To<T>(this Guid? guid) where T : IEntity
        {
            if (guid == null) return default(T);

            return guid.Value.To<T>();
        }

        /// <summary>
        /// This will use Database.Get() to load the specified entity type with this ID.
        /// </summary>
        public static T To<T>(this Guid guid) where T : IEntity
        {
            if (guid == Guid.Empty) return default(T);

            return Database.Get<T>(guid);
        }
    }
}