namespace MSharp.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Principal;
    using System.Xml.Linq;
    /// <summary>
    /// Provides services for application events and general logging.
    /// </summary>
    public static class ApplicationEventManager
    {
        internal static DefaultApplicationEventManager CurrentApplicationEventManager;

        static ApplicationEventManager()
        {
            var provider = Config.Get("MSharp.ApplicationEventManager");
            if (provider.HasValue())
            {
                CurrentApplicationEventManager = (DefaultApplicationEventManager)Type.GetType(provider).CreateInstance();
            }
            else
            {
                CurrentApplicationEventManager = new DefaultApplicationEventManager();
            }
        }

        internal static void RecordSave(IEntity entity, SaveMode saveMode)
        {
            CurrentApplicationEventManager.RecordSave(entity, saveMode);
        }

        /// <summary>
        /// Gets the changes XML for a specified object. That object should be in its OnSaving event state.
        /// </summary>
        public static string GetChangesXml(IEntity entityBeingSaved)
        {
            return CurrentApplicationEventManager.GetChangesXml(entityBeingSaved);
        }

        /// <summary>
        /// Gets the changes applied to the specified object.
        /// Each item in the result will be {PropertyName, { OldValue, NewValue } }.
        /// </summary>
        public static IDictionary<string, Tuple<string, string>> GetChanges(IEntity original, IEntity updated)
        {
            return CurrentApplicationEventManager.GetChanges(original, updated);
        }

        public static void RecordDelete(IEntity entity)
        {
            CurrentApplicationEventManager.RecordDelete(entity);
        }

        public static Dictionary<string, string> GetDataToLog(IEntity entity)
        {
            return CurrentApplicationEventManager.GetDataToLog(entity);
        }

        public static string ToChangeXml(IDictionary<string, Tuple<string, string>> changes)
        {
            return CurrentApplicationEventManager.ToChangeXml(changes);
        }

        /// <summary>
        /// Records the execution result of a scheduled task. 
        /// </summary>
        /// <param name="task">The name of the scheduled task.</param>
        /// <param name="startTime">The time when this task was started.</param>
        public static void RecordScheduledTask(string task, DateTime startTime)
        {
            CurrentApplicationEventManager.RecordScheduledTask(task, startTime);
        }

        /// <summary>
        /// Records the execution result of a scheduled task. 
        /// </summary>
        /// <param name="task">The name of the scheduled task.</param>
        /// <param name="startTime">The time when this task was started.</param>
        /// <param name="error">The Exception that occurred during the task execution.</param>
        public static void RecordScheduledTask(string task, DateTime startTime, Exception error)
        {
            CurrentApplicationEventManager.RecordScheduledTask(task, startTime, error);
        }

        /// <summary>
        /// Loads the item recorded in this event.
        /// </summary>
        public static IEntity LoadItem(this IApplicationEvent applicationEvent)
        {
            return CurrentApplicationEventManager.LoadItem(applicationEvent);
        }

        public static string ToAuditDataHtml(this IApplicationEvent applicationEvent)
        {
            if (applicationEvent.Event == "Insert" && applicationEvent.Data.OrEmpty().StartsWith("<Data>"))
            {
                return applicationEvent.Data.To<XElement>().Elements().Select(p => $"<div class='prop'><span class='key'>{p.Name}</span>: <span class='val'>{p.Value.HtmlEncode()}</span></div>").ToLinesString();
            }

            if (applicationEvent.Event == "Update" && applicationEvent.Data.OrEmpty().StartsWith("<DataChange>"))
            {
                var data = applicationEvent.Data.To<XElement>();
                var old = data.Element("old");
                var newData = data.Element("new");
                var propertyNames = old.Elements().Select(x => x.Name.LocalName)
                    .Concat(newData.Elements().Select(x => x.Name.LocalName)).Distinct().ToArray();

                return propertyNames.Select(p => $"<div class='prop'>Changed <span class='key'>{p}</span> from <span class='old'>\"{ old.GetValue<string>(p).HtmlEncode() }\"</span> to <span class='new'>\"{ newData.GetValue<string>(p).HtmlEncode() }\"</span></div>").ToLinesString();
            }

            if (applicationEvent.Event == "Delete" && applicationEvent.Data.OrEmpty().StartsWith("<DataChange>"))
            {
                var data = applicationEvent.Data.To<XElement>();
                var old = data.Element("old");

                var propertyNames = old.Elements().Select(x => x.Name.LocalName).ToArray();

                return propertyNames.Select(p => $"<div class='prop'><span class='key'>{p}</span> was <span class='old'>\"{old.GetValue<string>(p).HtmlEncode() }\"</span></div>").ToLinesString();
            }

            return applicationEvent.Data.OrEmpty().HtmlEncode();
        }

        /// <summary>
        /// Gets the current user id.
        /// </summary>
        public static string GetCurrentUserId(IPrincipal principal)
        {
            return CurrentApplicationEventManager.GetCurrentUserId(principal);
        }

        /// <summary>
        /// Gets the IP address of the current user.
        /// </summary>
        public static string GetCurrentUserIP()
        {
            return CurrentApplicationEventManager.GetCurrentUserIP();
        }

        /// <summary>
        /// Records the provided exception in the database.
        /// </summary>
        [Obsolete("Use Log.Error() instead")]
        public static IApplicationEvent RecordException(Exception exception)
        {
            return Framework.Log.Error(exception);
        }

        /// <summary>
        /// Records the provided exception in the database.
        /// </summary>
        [Obsolete("Use Log.Error() instead")]
        public static IApplicationEvent RecordException(string description, Exception exception)
        {
            return MSharp.Framework.Log.Error(description, exception);
        }

        /// <summary>
        /// Logs the specified event as a record in the ApplicationEvents database table.
        /// </summary>
        /// <param name="eventTitle">The event title.</param>
        /// <param name="details">The details of the event.</param>
        /// <param name="owner">The record for which this event is being logged (optional).</param>
        /// <param name="userId">The ID of the user involved in this event (optional). If not specified, the current ASP.NET context user will be used.</param>
        /// <param name="userIp">The IP address of the user involved in this event (optional). If not specified, the IP address of the current Http context (if available) will be used.</param>
        [Obsolete("Use Log.Record() instead")]
        public static IApplicationEvent Log(string eventTitle, string details, IEntity owner = null, string userId = null, string userIp = null)
        {
            return MSharp.Framework.Log.Record(eventTitle, details, owner, userId, userIp);
        }
    }
}