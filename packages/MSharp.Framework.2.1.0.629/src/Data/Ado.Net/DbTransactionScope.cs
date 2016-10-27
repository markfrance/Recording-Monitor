namespace MSharp.Framework.Data
{
    using System;
    using System.Collections.Generic;
    using System.Data;

    // TODO: If it's a Suppress, then simply in the GetDbTransaction return null.
    // And test to see if the command will pass in case where other commands in a transaction in the same connection exist, 
    // and are rolled back.

    public class DbTransactionScope : ITransactionScope
    {
        IsolationLevel IsolationLevel;
        DbTransactionScopeOption ScopeOption;

        [ThreadStatic]
        public static DbTransactionScope Root;

        [ThreadStatic]
        public static DbTransactionScope Current, Parent;

        // Per unique connection string, one record is added to this.
        Dictionary<string, Tuple<IDbConnection, IDbTransaction>> Connections = new Dictionary<string, Tuple<IDbConnection, IDbTransaction>>();


        bool IsCompleted, IsAborted;

        public DbTransactionScope() : this(GetDefaultIsolationLevel()) { }

        public DbTransactionScope(DbTransactionScopeOption scopeOption) : this(GetDefaultIsolationLevel(), scopeOption) { }

        public DbTransactionScope(IsolationLevel isolationLevel, DbTransactionScopeOption scopeOption = DbTransactionScopeOption.Required)
        {
            IsolationLevel = isolationLevel;
            ScopeOption = scopeOption;

            Parent = Root;
            Current = this;

            if (Root == null) Root = this;
        }

        public Guid ID { get; } = Guid.NewGuid();

        #region TransactionCompletedEvent

        event EventHandler TransactionCompleted;

        /// <summary>
        /// Attaches an event handler to be invoked when the current (root) transaction is completed.
        /// </summary>
        public void OnTransactionCompleted(Action eventHandler)
        {
            Root.TransactionCompleted += (s, e) => eventHandler?.Invoke();
        }

        #endregion

        static IsolationLevel GetDefaultIsolationLevel()
        {
            return Config.Get<IsolationLevel>("Default.Transaction.IsolationLevel", IsolationLevel.ReadUncommitted);
        }

        internal IDbTransaction GetDbTransaction()
        {
            var connectionString = DataAccessor.GetCurrentConnectionString();

            Setup(connectionString);

            return Connections[connectionString].Item2;
        }

        internal IDbConnection GetDbConnection()
        {
            var connectionString = DataAccessor.GetCurrentConnectionString();

            Setup(connectionString);

            return Connections[connectionString].Item1;
        }

        void Setup(string connectionString)
        {
            if (!Connections.ContainsKey(connectionString))
            {
                var connection = (IDbConnection)DataAccessor.CreateActualConnection();
                var transaction = connection.BeginTransaction(IsolationLevel);

                Connections.Add(connectionString, Tuple.Create(connection, transaction));
            }
        }

        public void Dispose()
        {
            if (IsAborted) return;

            if (this == Root) // Root
            {
                Root = null;

                if (IsCompleted)
                {
                    // Happy scenario:
                    Connections.Do(x => x.Value.Item1.Close());
                }
                else // Root is not completed.
                {
                    IsAborted = true;

                    Connections.Do(x => x.Value.Item2.Rollback());
                    Connections.Do(x => x.Value.Item2.Dispose());
                    Connections.Do(x => x.Value.Item1.Close());
                }
            }
            else
            {
                Current = Parent;

                if (IsCompleted)
                {
                    // A Sub-transaction has been happily completed.
                    // Just wait for the parent.
                }
                else
                {
                    // A sub transaction is not completed.
                    Root?.Dispose();
                }
            }
        }

        public void Complete()
        {
            if (IsAborted)
                throw new Exception("This transaction is already aborted, probably due to a nested transaction not being completed.");

            IsCompleted = true;

            if (Root == this)
            {
                // I'm the root:                
                Connections.Do(x => x.Value.Item2.Commit());
                TransactionCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // Ignore, and wait for the parent Completion.
            }
        }
    }
}