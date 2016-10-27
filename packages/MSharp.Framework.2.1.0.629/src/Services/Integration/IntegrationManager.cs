namespace MSharp.Framework.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using MSharp.Framework.Data;
    using Newtonsoft.Json;

    /// <summary>
    /// Provides services for integration services.
    /// </summary>
    public class IntegrationManager
    {
        static Type IntegrationQueueItemType;

        /// <summary>
        /// Key = TRequest|TResponse
        /// Value = TService
        /// </summary>
        internal static Dictionary<string, Type> IntegrationServices = new Dictionary<string, Type>();

        static object PickLock = new object();

        /// <summary>
        /// Will find a Service Registered to process the item.
        /// </summary>
        public static IIntegrationQueueItem Process(IIntegrationQueueItem item)
        {
            if (item.ResponseDate != null)
            {
                // Already processed:
                return item;
            }

            #region Pick the item

            Type serviceType;

            lock (PickLock)
            {
                if (item.DatePicked != null)
                {
                    // Already picked, let the other thread finish its job:
                    return null;
                }

                if (!item.IsNew) item = (IIntegrationQueueItem)item.Clone();

                serviceType = IntegrationServices.GetOrDefault(item.IntegrationService);
                if (serviceType == null) return null;

                item.DatePicked = LocalTime.Now;
                Database.Save(item);
            }
            // TOOD: This is not thread safe in multi-server (web farm) scenarios.
            // To make it completely safe, we need a single StoredProc or SQL command that will do both at the same time, with a lock at the DB level.
            #endregion

            var service = serviceType.CreateInstance();

            var serviceInterface = serviceType.GetInterfaces().Single(x => x.Name.Contains("IServiceImplementor"));

            var typeOfRequest = serviceInterface.GetGenericArguments().First();

            var request = JsonConvert.DeserializeObject(item.Request, typeOfRequest);
            //var request = new JavaScriptSerializer().Deserialize(item.Request, typeOfRequest);

            var method = serviceType.GetMethod("GetResponse", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            try
            {
                var response = method.Invoke(service, new[] { request });
                item.Response = JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                item.ErrorResponse = ex.ToString();
            }

            item.ResponseDate = LocalTime.Now;
            Database.Save(item);
            return item;
        }

        public static IIntegrationQueueItem Process(string id)
        {
            return Process(Database.Get<IIntegrationQueueItem>(id));
        }

        /// <summary>
        ///  Uses the right Integration Service to process the outstanding items in all queues.
        ///  This should be called as an Automated Task in the application.
        /// </summary>
        public static IEnumerable<IIntegrationQueueItem> ProcessOutstandingItems()
        {
            var result = new List<IIntegrationQueueItem>();

            foreach (var serviceType in IntegrationServices.Keys)
            {
                result.AddRange(ProcessOutstandingItems(serviceType));
            }

            return result;
        }

        /// <summary>
        ///  Uses the right Integration Service to process the next item in that queue.
        /// </summary>
        public static IEnumerable<IIntegrationQueueItem> ProcessOutstandingItems(string serviceName)
        {
            var serviceType = IntegrationServices.GetOrDefault(serviceName);
            if (serviceType == null) throw new Exception("Integration service not registered:" + serviceName);

            var result = new List<IIntegrationQueueItem>();

            foreach (var item in Database.GetList<IIntegrationQueueItem>(x => x.IntegrationService == serviceName && x.DatePicked == null))
            {
                Database.Update(item, x => x.DatePicked = LocalTime.Now);
                // TODO: Use T-SQL to fetch an item and return it too.

                Process(item);
                result.Add(item);
            }

            return result;
        }

        static void DiscoverQueueType()
        {
            if (IntegrationQueueItemType != null) return;

            try
            {
                IntegrationQueueItemType = typeof(IIntegrationQueueItem).FindImplementerClasses().Single(x => !x.IsAbstract);
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot find the correct implementation type for IIntegrationQueueItem.", ex);
            }
        }

        static string GetServiceKey(Type request, Type response)
        {
            return request.Name + "|" + response.Name;
        }

        /// <summary>
        /// Registers an integration service.
        /// </summary>
        public static void Register<TRequest, TResponse, TService>() where TService : IServiceImplementor<TRequest, TResponse>
        {
            DiscoverQueueType();

            var key = GetServiceKey(typeof(TRequest), typeof(TResponse));

            IntegrationServices.Add(key, typeof(TService));

        }

        /// <summary>
        /// Inserts a queu item to call this service and waits until the item is processed.
        /// Then it will return the response.
        /// </summary>
        public static TResponse Request<TRequest, TResponse>(TRequest request)
        {
            var item = RequestAsync<TRequest, TResponse>(request);

            return AwaitResponse<TResponse>(item);
        }

        /// <summary>
        /// Inserts a request in the queue and immediately returns without waiting for a response.
        /// It will return the token string for this request, that can be queried later on for a response (using Await Response).
        /// </summary>
        public static string RequestAsync<TRequest, TResponse>(TRequest request)
        {
            DiscoverQueueType();

            var item = IntegrationQueueItemType.CreateInstance<IIntegrationQueueItem>();
            item.IntegrationService = GetServiceKey(request.GetType(), typeof(TResponse));
            //item.Request = new JavaScriptSerializer().Serialize(request);
            item.Request = JsonConvert.SerializeObject(request);

            item.RequestDate = LocalTime.Now;
            Database.Save(item);

            return item.ID.ToString();
        }

        /// <summary>
        /// It will wait until a response is provided by another thread to the integration queue item specified by its token.
        /// </summary>
        public static TResponse AwaitResponse<TResponse>(string requestToken, int waitIntervals = 50)
        {
            while (true)
            {
                //var item = Database.Get<IIntegrationQueueItem>(requestToken);

                var processedItem = Process(requestToken);

                if (processedItem != null)
                {
                    //var result = new JavaScriptSerializer().Deserialize(processedItem.Response, typeof(TResponse));
                    var result = JsonConvert.DeserializeObject(processedItem.Response, typeof(TResponse));
                    return (TResponse)result;
                }

                Thread.Sleep(waitIntervals);
            }
        }

        ///// <summary>
        ///// Inserts a queu item to call this service and waits until the item is processed.
        ///// Then it will return the response.
        ///// </summary>
        //public static object Request(object request, Type responseType)
        //{
        //    // Insert a queue item in the database.
        //    var item = IntegrationQueueItemType.CreateInstance<IIntegrationQueueItem>();
        //    item.IntegrationService = GetServiceKey(request.GetType(), responseType);
        //    item.Request = new JavaScriptSerializer().Serialize(request);
        //    item.RequestDate = LocalTime.Now;
        //    Database.Save(item);

        //    while (true)
        //    {
        //        var processedItem = Process(item);

        //        if (processedItem != null)
        //        {
        //            var result = new JavaScriptSerializer().Deserialize(processedItem.Response, responseType);
        //            return result;
        //        }
        //        Thread.Sleep(50);
        //    }
        //}

        /// <summary>
        /// Injects an asyncronous waiter which will inject the provided response for one potential future request.
        /// It will check every 5 milliseconds to see if a request item is inserted in the queue, and in that case respond to it.
        /// </summary>
        public static void InjectResponse<TRequest, TResponse>(TResponse injectedResponse)
        {
            var service = GetServiceKey(typeof(TRequest), typeof(TResponse));

            // Get queue item:
            while (true)
            {
                var latestCondition = QueryOption.OrderByDescending<IIntegrationQueueItem>(x => x.RequestDate);

                var item = Database.Find<IIntegrationQueueItem>(x => x.IntegrationService == service && x.ResponseDate == null, latestCondition);

                if (item != null)
                {
                    item = item.Clone() as IIntegrationQueueItem;

                    //item.Response = new JavaScriptSerializer().Serialize(injectedResponse);
                    item.Response = JsonConvert.SerializeObject(injectedResponse);
                    item.ResponseDate = LocalTime.Now;

                    Database.Save(item);

                    return;
                }

                Thread.Sleep(5);
            }
        }
    }
}