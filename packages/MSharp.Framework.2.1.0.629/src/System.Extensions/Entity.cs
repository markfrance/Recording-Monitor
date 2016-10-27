using System.Collections.Generic;
using System.Linq;
using MSharp.Framework;

namespace System
{
    partial class MSharpExtensions
    {
        /// <summary>
        /// Determines if this item is in a specified list of specified items.
        /// </summary>
        public static bool IsAnyOf<T>(this T item, params T[] options) where T : IEntity
        {
            if (item == null) return options.Contains(default(T));

            return options.Contains(item);
        }

        /// <summary>
        /// Determines if this item is in a specified list of specified items.
        /// </summary>
        public static bool IsAnyOf<T>(this T item, IEnumerable<T> options) where T : IEntity
        {
            return options.Contains(item);
        }

        /// <summary>
        /// Determines if this item is none of a list of specified items.
        /// </summary>
        public static bool IsNoneOf<T>(this T item, params T[] options) where T : IEntity
        {
            if (item == null) return !options.Contains(default(T));

            return !options.Contains(item);
        }

        /// <summary>
        /// Determines if this item is none of a list of specified items.
        /// </summary>
        public static bool IsNoneOf<T>(this T item, IEnumerable<T> options) where T : IEntity
        {
            if (item == null) return !options.Contains(default(T));

            return !options.Contains(item);
        }

        /// <summary>
        /// Clones all items of this collection.
        /// </summary>
        public static List<T> CloneAll<T>(this IEnumerable<T> list) where T : IEntity
        {
            return list.Select(i => (T)i.Clone()).ToList();
        }

        /// <summary>
        /// Determines whether this document is an image.
        /// </summary>
        public static bool IsImage(this Document doc)
        {
            if (doc.IsEmpty()) return false;

            try
            {
                using (System.Drawing.Imaging.BitmapHelper.FromBuffer(doc.FileData))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the id of this entity.
        /// </summary>
        internal static string GetFullIdentifierString(this IEntity entity)
        {
            if (entity == null) return null;

            return entity.GetType().GetRootEntityType().FullName + "/" + entity.GetId();
        }

        /// <summary>
        /// Validates all entities in this collection.
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="entities">The entities.</param>
        public static void ValidateAll<T>(this IEnumerable<T> entities) where T : Entity
        {
            foreach (var entity in entities)
            {
                entity.Validate();
            }
        }

        /// <summary>
        /// Returns this Entity only if the given predicate evaluates to true and this is not null.
        /// </summary>        
        public static T OnlyWhen<T>(this T entity, Func<T, bool> criteria) where T : Entity
        {
            return entity != null && criteria(entity) ? entity : null;
        }

        /// <summary>
        /// Returns all entity Guid IDs for this collection.
        /// </summary>
        public static IEnumerable<TId> IDs<TId>(this IEnumerable<IEntity<TId>> entities)
        {
            return entities.Select(entity => entity.ID);
        }
    }
}