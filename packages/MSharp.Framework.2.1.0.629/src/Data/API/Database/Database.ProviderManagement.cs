namespace MSharp.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Reflection;
    using System.Transactions;
    using MSharp.Framework.Data;

    partial class Database
    {
        static Database()
        {
            AssemblyProviderFactories = new Dictionary<Assembly, IDataProviderFactory>();
            TypeProviderFactories = new Dictionary<Type, IDataProviderFactory>();

            // Load from configuration:
            var configSection = ConfigurationManager.GetSection("dataProviderModel") as DataProviderModelConfigurationSection;

            if (configSection != null)
            {
                if (configSection.Providers != null)
                {
                    foreach (var factoryInfo in configSection.Providers)
                        RegisterDataProviderFactory(factoryInfo);
                }

                if (configSection.FileDependancyPath.HasValue())
                    ExternalCacheDependancy.CreateDependancy(configSection.FileDependancyPath);

                if (configSection.SyncFilePath.HasValue())
                {
                    Updated += delegate
                    {
                        ExternalCacheDependancy.UpdateSyncFile(configSection.SyncFilePath);
                    };
                }
            }
        }

        #region Updated event
        /// <summary>
        /// It's raised when any record is saved or deleted in the system.
        /// </summary>
        public static event EventHandler<EventArgs<IEntity>> Updated;
        static void OnUpdated(EventArgs<IEntity> e)
        {
            Updated?.Invoke(e.Data, e);
        }
        #endregion

        static object DataProviderSyncLock = new object();
        public static void RegisterDataProviderFactory(DataProviderFactoryInfo factoryInfo)
        {
            if (factoryInfo == null) throw new ArgumentNullException(nameof(factoryInfo));

            lock (DataProviderSyncLock)
            {
                var type = factoryInfo.GetMappedType();
                var assembly = factoryInfo.GetAssembly();

                // var providerFactoryType = Type.GetType(factoryInfo.ProviderFactoryType); HAS A PROIBLEM WITH VERSIONING
                var providerFactoryType = assembly.GetTypes().FirstOrDefault(t => t.AssemblyQualifiedName == factoryInfo.ProviderFactoryType);
                if (providerFactoryType == null) providerFactoryType = assembly.GetType(factoryInfo.ProviderFactoryType);
                if (providerFactoryType == null) providerFactoryType = Type.GetType(factoryInfo.ProviderFactoryType);

                if (providerFactoryType == null)
                    throw new Exception("Could not find the type " + factoryInfo.ProviderFactoryType + " as specified in configuration.");

                var providerFactory = (IDataProviderFactory)Activator.CreateInstance(providerFactoryType, factoryInfo);

                if (type != null)
                {
                    TypeProviderFactories[type] = providerFactory;
                }
                else if (assembly != null && providerFactory != null)
                {
                    AssemblyProviderFactories[assembly] = providerFactory;
                }

                EntityFinder.ResetCache();
            }
        }

        internal static Dictionary<Assembly, IDataProviderFactory> AssemblyProviderFactories;
        static Dictionary<Type, IDataProviderFactory> TypeProviderFactories;

        /// <summary>
        /// Gets the assemblies for which a data provider factory has been registered in the current domain.
        /// </summary>
        public static IEnumerable<Assembly> GetRegisteredAssemblies()
        {
            return TypeProviderFactories.Keys.Select(t => t.Assembly).Concat(AssemblyProviderFactories.Keys).Distinct().ToArray();
        }

        public static IDataProvider GetProvider<T>() where T : IEntity => GetProvider(typeof(T));

        public static IDataProvider GetProvider(IEntity item) => GetProvider(item.GetType());

        public static IDataProvider GetProvider(Type type)
        {
            if (TypeProviderFactories.ContainsKey(type))
                return TypeProviderFactories[type].GetProvider(type);

            // Strange bug: 
            if (AssemblyProviderFactories.Any(x => x.Key == null))
                AssemblyProviderFactories = new Dictionary<Assembly, IDataProviderFactory>();

            if (!AssemblyProviderFactories.ContainsKey(type.Assembly))
                throw new InvalidOperationException("There is no registered 'data provider' for the assembly: " + type.Assembly.FullName);

            return AssemblyProviderFactories[type.Assembly].GetProvider(type);
        }

        /// <summary>
        /// Creates a transaction scope.
        /// </summary>
        public static ITransactionScope CreateTransactionScope(DbTransactionScopeOption option = DbTransactionScopeOption.Required)
        {
            var isolationLevel = Config.Get("Default.Transaction.IsolationLevel", System.Data.IsolationLevel.Serializable);

            var typeName = Config.Get<string>("Default.TransactionScope.Type");

            if (typeName.HasValue())
            {
                var type = Type.GetType(typeName);
                if (type == null) throw new Exception("Cannot load type: " + typeName);

                return (ITransactionScope)type.CreateInstance(new object[] { isolationLevel, option });
            }

            // Fall back to TransactionScope:                
            var oldOption = option.ToString().To<TransactionScopeOption>();
            return new TransactionScopeWrapper(isolationLevel.ToString().To<IsolationLevel>().CreateScope(oldOption));
        }

        [Obsolete("Use DbTransactionScopeOption instead.")]
        public static ITransactionScope CreateTransactionScope(TransactionScopeOption option)
        {
            return CreateTransactionScope(option.ToString().To<DbTransactionScopeOption>());
        }
    }
}