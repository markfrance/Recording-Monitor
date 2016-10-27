using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MSharp.Framework
{
    /// <summary>
    /// When applied to a property, indicates that such property does not exist in the database.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class CalculatedAttribute : Attribute
    {
        static Dictionary<PropertyInfo, bool> Cache = new Dictionary<PropertyInfo, bool>();

        /// <summary>
        /// Determines if a given property is calculated.
        /// </summary>
        public static bool IsCalculated(PropertyInfo property)
        {
            bool result;

            if (Cache.TryGetValue(property, out result)) return result;

            return DetectAndCache(property);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        static bool DetectAndCache(PropertyInfo property)
        {
            var result = property.IsDefined(typeof(CalculatedAttribute), inherit: true); 

            try { return Cache[property] = result; }
            catch { return result; }
        }
    }
}