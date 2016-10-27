namespace MSharp.Framework
{
    using System;

    /// <summary>
    /// Records information in the ApplicationEvents table.
    /// </summary>
    public static class Log
    {
        public static IApplicationEvent Error(Exception ex)
        {
            return ApplicationEventManager.CurrentApplicationEventManager.RecordException(ex);
        }

        public static IApplicationEvent Error(string description, Exception ex = null)
        {
            if (ex == null)
                return Record("Exception", description);
            else
                return ApplicationEventManager.CurrentApplicationEventManager.RecordException(description, ex);
        }

        public static IApplicationEvent Warning(string description, IEntity relatedObject = null, string userId = null, string userIp = null)
        {
            return Record("Warning", description, relatedObject, userId, userIp);
        }

        public static IApplicationEvent Debug(string description, IEntity relatedObject = null, string userId = null, string userIp = null)
        {
            return Record("Debug", description, relatedObject, userId, userIp);
        }

        public static IApplicationEvent Info(string description, IEntity relatedObject = null, string userId = null, string userIp = null)
        {
            return Record("Info", description, relatedObject, userId, userIp);
        }

        public static IApplicationEvent Audit(string description, IEntity relatedObject = null, string userId = null, string userIp = null)
        {
            return Record("Audit", description, relatedObject, userId, userIp);
        }

        public static IApplicationEvent Record(string eventTitle, string description, IEntity relatedObject = null, string userId = null, string userIp = null)
        {
            return ApplicationEventManager.CurrentApplicationEventManager.Log(eventTitle, description, relatedObject, userId, userIp);
        }
    }
}
