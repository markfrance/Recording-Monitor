namespace MSharp.Framework
{
    using System;
    using System.Reflection;

    /// <summary>
    /// When applied to a class, indicates whether data access events should be logged for instances of that type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public sealed class LogEventsAttribute : Attribute
    {
        const bool DEFAULT_UNCONFIGURED = true;

        public bool Log { get; private set; }

        /// <summary>
        /// Creates a new LogEventsAttribute instance.
        /// </summary>
        public LogEventsAttribute(bool shouldLog)
        {
            Log = shouldLog;
        }

        internal static bool ShouldLog(Type type)
        {
            var definedAttribute = type.GetCustomAttribute<LogEventsAttribute>(inherit: true);

            return definedAttribute?.Log ?? DEFAULT_UNCONFIGURED;
        }

    }
}
