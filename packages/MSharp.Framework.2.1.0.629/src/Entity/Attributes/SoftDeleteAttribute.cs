using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MSharp.Framework
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SoftDeleteAttribute : Attribute
    {
        static Dictionary<Type, bool> Cache = new Dictionary<Type, bool>();

        /// <summary>
        /// Determines if soft delete is enabled for a given type.
        /// </summary>
        public static bool IsEnabled(Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            bool result;

            if (Cache.TryGetValue(type, out result)) return result;

            return DetectAndCache(type);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        static bool DetectAndCache(Type type)
        {
            var result = type.IsDefined(typeof(SoftDeleteAttribute), inherit: true); 

            try { return Cache[type] = result; }
            catch { return result; }
        }

        public static bool RequiresSoftdeleteQuery<T>()
        {
            if (!IsEnabled(typeof(T))) return false;

            return !Context.ShouldByPassSoftDelete();
        }

        /// <summary>
        /// Provides support for bypassing softdelete rule.
        /// </summary>
        public class Context : IDisposable
        {
            bool BypassSoftdelete;

            Context ParentContext;

            [ThreadStatic]
            public static Context Current = null;

            /// <summary>
            /// Creates a new Context instance.
            /// </summary>
            public Context(bool bypassSoftdelete)
            {
                this.BypassSoftdelete = bypassSoftdelete;

                // Get from current thread:

                if (Current != null)
                    this.ParentContext = Current;
                Current = this;
            }

            public void Dispose()
            {
                Current = ParentContext;
            }

            /// <summary>
            /// Determines if SoftDelete check should the bypassed in the current context.
            /// </summary>
            public static bool ShouldByPassSoftDelete()
            {
                if (Current == null) return false;
                else return Current.BypassSoftdelete;
            }


        }
    }
}