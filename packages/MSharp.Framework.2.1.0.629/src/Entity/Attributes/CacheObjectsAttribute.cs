namespace MSharp.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Specifies if a type is cacheable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public sealed class CacheObjectsAttribute : Attribute
    {
        static bool? DEFAULT_UNCONFIGURED = null;

        static Dictionary<Type, bool?> Cache = new Dictionary<Type, bool?>();

        static object SyncLock = new object();

        bool Enabled;

        /// <summary>
        /// Creates a new CacheObjectsAttribute instance.
        /// </summary>
        public CacheObjectsAttribute(bool enabled)
        {
            Enabled = enabled;
        }

        /// <summary>
        /// Determines if caching is enabled for a given type.
        /// </summary>
        public static bool? IsEnabled(Type type)
        {
            bool? result;

            if (Cache.TryGetValue(type, out result)) return result;

            return DetectAndCache(type);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        static bool? DetectAndCache(Type type)
        {
            var usage = type.GetCustomAttributes<CacheObjectsAttribute>(inherit: true).FirstOrDefault();

            var result = DEFAULT_UNCONFIGURED;

            if (usage != null) result = usage.Enabled;

            try { return Cache[type] = result; }
            catch { return result; }
        }
    }
}