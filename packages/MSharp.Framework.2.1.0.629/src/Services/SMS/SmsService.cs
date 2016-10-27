using System;

namespace MSharp.Framework.Services
{
    public static class SmsService
    {
        /// <summary>
        /// Occurs when an exception happens when sending an sms. Sender parameter will be the ISmsQueueItem instance that couldn't be sent.
        /// </summary>
        public static event EventHandler<EventArgs<Exception>> SendError;
        static void OnSendError(ISmsQueueItem email, Exception error)
        {
            SendError?.Invoke(email, new EventArgs<Exception>(error));
        }

        /// <summary>
        /// Sends the specified SMS item.
        /// It will try several times to deliver the message. The number of retries can be specified in AppConfig of "SMS.Maximum.Retries".
        /// If it is not declared in web.config, then 3 retires will be used.
        /// Note: The actual SMS Sender component must be implemented as a public type that implements ISMSSender interface.
        /// The assembly qualified name of that component, must be specified in AppConfig of "SMS.Sender.Type".
        /// </summary>
        public static bool Send(ISmsQueueItem smsItem)
        {
            if (smsItem.Retries > Config.Get<int>("SMS.Maximum.Retries", 3))
                return false;
            try
            {
                ISMSSender sender;
                try
                {
                    sender = Activator.CreateInstance(Type.GetType(Config.Get("SMS.Sender.Type"))) as ISMSSender;

                    if (sender == null)
                        throw new Exception("Type is not defined, or it does not implement ISMSSender");
                }
                catch (Exception ex)
                {
                    Log.Error("Can not instantiate the sms sender from App config of " + Config.Get("SMS.Sender.Type"), ex);
                    return false;
                }

                sender.Deliver(smsItem);

                Database.Update(smsItem, o => o.DateSent = LocalTime.Now);
                return true;
            }
            catch (Exception ex)
            {
                OnSendError(smsItem, ex);
                Log.Error("Can not send the SMS queue item.", ex);
                smsItem.RecordRetry();
                return false;
            }
        }

        public static void SendAll()
        {
            foreach (var sms in Database.GetList<ISmsQueueItem>(i => i.DateSent == null))
            {
                sms.Send();
            }
        }
    }
}