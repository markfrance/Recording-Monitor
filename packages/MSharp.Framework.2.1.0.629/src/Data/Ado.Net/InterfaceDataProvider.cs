namespace MSharp.Framework.Data.Ado.Net
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Provides data access for Interface types.
    /// </summary>
    public class InterfaceDataProvider<TImplementationDataProvider> : IDataProvider where TImplementationDataProvider : IDataProvider
    {
        static ConcurrentDictionary<Type, List<Type>> ImplementationsCache = new ConcurrentDictionary<Type, List<Type>>();

        static List<Type> GetImplementers(Type interfaceType)
        {
            return ImplementationsCache.GetOrAdd(interfaceType, FindImplementers);
        }

        static List<Type> FindImplementers(Type interfaceType)
        {
            var result = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.References(interfaceType.Assembly)))
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type == interfaceType) continue;
                        if (type.IsInterface) continue;

                        if (type.Implements(interfaceType))
                        {
                            result.Add(type);
                        }
                    }
                }
                catch
                {
                    // Can't load assembly
                }
            }

            // For any type, if it's parent is in the list, exclude it:

            var typesWithParentsIn = result.Where(x => result.Contains(x.BaseType)).ToArray();

            foreach (var item in typesWithParentsIn)
                result.Remove(item);

            return result;
        }

        public int Count(Type type, IEnumerable<ICriterion> conditions, params QueryOption[] options)
        {
            return GetList(type, conditions, options).Count();
        }
        public IEnumerable<IEntity> GetList(Type type, IEnumerable<ICriterion> criteria, params QueryOption[] options)
        {
            return GetImplementers(type).SelectMany(x => Database.GetList(x, criteria, options)).ToList();
        }

        public IEntity Get(Type type, object objectID)
        {
            foreach (var actual in GetImplementers(type))
            {
                try
                {
                    var result = Database.Get(objectID, actual) as Entity;
                    if (result != null) return result;
                }
                catch
                {
                    continue;
                }
            }

            throw new Exception("There is no {0} record with the ID of '{1}'".FormatWith(type.Name, objectID));
        }

        public IEnumerable<string> ReadManyToManyRelation(IEntity instance, string property)
        {
            throw new NotSupportedException("IDataProvider.ReadManyToManyRelation() is not supported for Interfaces");
        }

        public void Save(IEntity record)
        {
            throw new NotSupportedException("IDataProvider.Save() is irrelevant to Interfaces");
        }

        public void Delete(IEntity record)
        {
            throw new NotSupportedException("IDataProvider.Delete() is irrelevant to Interfaces");
        }

        #region IDataProvider Members


        public IEnumerable<object> GetIdsList(Type type, IEnumerable<ICriterion> criteria)
        {
            throw new NotSupportedException("IDataProvider.Delete() is irrelevant to Interfaces");
        }

        public IDictionary<string, Tuple<string, string>> GetUpdatedValues(IEntity original, IEntity updated)
        {
            throw new NotSupportedException("GetUpdatedValues() is irrelevant to Interfaces");
        }

        public int ExecuteNonQuery(string command)
        {
            throw new NotSupportedException("ExecuteNonQuery() is irrelevant to Interfaces");
        }

        public object ExecuteScalar(string command)
        {
            throw new NotSupportedException("ExecuteScalar() is irrelevant to Interfaces");
        }

        public bool SupportValidationBypassing()
        {
            throw new NotSupportedException("SupportValidationBypassing() is irrelevant to Interfaces");
        }

        public void BulkInsert(IEntity[] entities, int batchSize)
        {
            throw new NotSupportedException("BulkInsert() is irrelevant to Interfaces");
        }

        public void BulkUpdate(IEntity[] entities, int batchSize)
        {
            throw new NotSupportedException("BulkInsert() is irrelevant to Interfaces");
        }

        public string ConnectionString { get; set; }

        public string ConnectionStringKey { get; set; }

        #endregion
    }
}