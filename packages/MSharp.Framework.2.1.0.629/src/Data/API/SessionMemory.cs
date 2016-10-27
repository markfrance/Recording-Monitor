namespace MSharp.Framework
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Remoting.Messaging;
    using System.Web;
    using AddedItem = System.Collections.Generic.KeyValuePair<System.DateTime, IEntity>;

    /// <summary>
    /// A repository of transient records in the user's session memory.
    /// </summary>
    [Serializable]
    public class SessionMemory
    {
        const string SESSION_KEY = "User.Session.Memory";
        public static bool IsSessionMemoryEnabled = Config.Get<bool>("Database.Session.Memory.Enabled", defaultValue: true);

        readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, AddedItem>> DataBags =
            new ConcurrentDictionary<Type, ConcurrentDictionary<string, AddedItem>>();

        static readonly ConcurrentDictionary<Type, Type> RootTypesCache = new ConcurrentDictionary<Type, Type>();

        static List<KeyValuePair<DateTime, SessionMemory>> SessionMemoryBags = new List<KeyValuePair<DateTime, SessionMemory>>();

        SessionMemory()
        {
            lock (SessionMemoryBags)
            {
                SessionMemoryBags.Add(new KeyValuePair<DateTime, SessionMemory>(LocalTime.Now, this));
            }
        }

       
        ConcurrentDictionary<string, AddedItem> GetBag(Type type)
        {
            var rootType = RootTypesCache.GetOrAdd(type, t => t.GetRootEntityType());

            return DataBags.GetOrAdd(rootType, t => new ConcurrentDictionary<string, AddedItem>());
        }

        /// <summary>
        /// Clears the old objects in session memory.
        /// </summary>
        public static void ClearOldObjects()
        {
            ClearOldObjects(TimeSpan.FromMinutes(15));
        }

        static string GetKey(IEntity instance)
        {
            return instance.GetType().FullName + "|" + instance.GetId();
        }

        /// <summary>
        /// Clears the old objects in session memory. This method is meant to be called periodically every few minutes.
        /// </summary>
        /// <param name="maxAge">The maximum age allowed to live in the memory. Any objects added before the specified time span will be cleared.</param>
        public static void ClearOldObjects(TimeSpan maxAge)
        {
            if (!IsSessionMemoryEnabled) return;

            var minDate = LocalTime.Now.Subtract(maxAge);

            lock (SessionMemoryBags)
            {
                foreach (var item in SessionMemoryBags.ToArray())
                {
                    var instance = item.Value;

                    var isEmpty = true;

                    lock (instance)
                    {
                        foreach (var bag in instance.DataBags.Values)
                        {
                            foreach (var record in bag.Values.Where(i => i.Key < minDate).Select(i => i.Value).ToArray())
                            {
                                bag.TryRemove(GetKey(record));
                            }

                            if (bag.Any()) isEmpty = false;
                        }
                    }

                    if (item.Key < minDate && isEmpty)
                    {
                        // dead session:
                        SessionMemoryBags.Remove(item);
                    }
                }
            }
        }


        /// <summary>
        /// Gets the current instance of the session memory, specific to the current ASP.NET user (or "Current Thread" when ASP.NET session is not available in the current context).
        /// </summary>
        static SessionMemory Current
        {
            get
            {
                var session = HttpContext.Current?.Session;

                if (session != null)
                {
                    if (session[SESSION_KEY] == null)
                        session[SESSION_KEY] = new SessionMemory();

                    return (SessionMemory)session[SESSION_KEY];
                }
                else // Use thread:
                {
                    if (CallContext.GetData(SESSION_KEY) == null)
                        CallContext.SetData(SESSION_KEY, new SessionMemory());

                    return (SessionMemory)CallContext.GetData(SESSION_KEY);
                }
            }
        }


        /// <summary>
        /// Gets a record from the session memory by its specified ID.        
        /// </summary>
        public static T Get<T>(Guid recordId) where T : class, IEntity
        {
            if (!IsSessionMemoryEnabled) return null;

            var idString = recordId.ToString();

            return Get(typeof(T), idString) as T;
        }

        /// <summary>
        /// Gets a record from the session memory by its specified ID and Type.        
        /// </summary>
        public static IEntity Get(Type type, object id)
        {
            if (!IsSessionMemoryEnabled) return null;

            var idString = id.ToString();

            if (type.IsInterface)
            {
                foreach (var bag in Current.DataBags.Values)
                {
                    var result = bag.Values.FirstOrDefault(x => x.Value.GetId().ToString() == idString && x.Value.GetType().IsA(type)).Value;
                    if (result != null) return result;
                }

                return null;
            }
            else
            {
                return Current.GetBag(type).Values.FirstOrDefault(x => x.Value.GetId().ToString() == idString).Value;
            }
        }

        /// <summary>
        /// Gets a list of objects of the specified type, matching the specified criteria.
        /// </summary>
        public static IEnumerable<T> GetList<T>() where T : class, IEntity
        {
            if (!IsSessionMemoryEnabled) return Enumerable.Empty<T>();

            return GetList<T>(criteria: null);
        }

        /// <summary>
        /// Gets a list of objects of the specified type, matching the specified criteria.
        /// </summary>
        public static IEnumerable<T> GetList<T>(Func<T, bool> criteria) where T : class, IEntity
        {
            if (!IsSessionMemoryEnabled) return default(IEnumerable<T>);

            var bag = Current.GetBag(typeof(T));

            //var result = bag.Select(i => i.Value as T).ExceptNull();
            var result = bag.Values.Select(i => i.Value as T).ExceptNull();

            if (criteria != null)
                result = result.Where(criteria);

            return result.ToArray();
        }

        /// <summary>
        /// Finds the first object matching the specified criteria.
        /// </summary>
        public static T Find<T>(Func<T, bool> criteria) where T : class, IEntity
        {
            if (!IsSessionMemoryEnabled) return null;

            return GetList<T>(criteria).FirstOrDefault();
        }

        /// <summary>
        /// Adds a specified records to the Session memory.
        /// </summary>
        public static void AddRange<T>(IEnumerable<T> instances) where T : IEntity
        {
            if (!IsSessionMemoryEnabled)
                throw new InvalidOperationException("Session Memory is disabled. To enable it, set AppSetting of 'Database.Session.Memory.Enabled' to 'true'.");

            instances.Do(i => Add(i));
        }

        /// <summary>
        /// Adds a specified record to the Session memory. If another object with the same ID already exists, the new object will replace it.
        /// </summary>
        public static T Add<T>(T instance) where T : IEntity
        {
            if (!IsSessionMemoryEnabled)
                throw new InvalidOperationException("Session Memory is disabled. To enable it, set AppSetting of 'Database.Session.Memory.Enabled' to 'true'.");

            if (instance == null)
                throw new ArgumentNullException("instance");

            var bag = Current.GetBag(instance.GetType());

            var key = GetKey(instance);
            bag.TryRemove(key);
            bag.TryAdd(key, new AddedItem(LocalTime.Now, instance));

            return instance;
        }

        /// <summary>
        /// Removes a specified record from the session memory.
        /// </summary>
        public static void Remove(IEntity instance)
        {
            if (!IsSessionMemoryEnabled) return;

            if (instance == null)
                throw new ArgumentNullException("instance");

            var bag = Current.GetBag(instance.GetType());

            bag.TryRemove(GetKey(instance));
        }
    }
}