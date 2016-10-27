namespace System.Transactions
{
    /// <summary>
    /// Provides extension methods for transaction classes.
    /// </summary>
    public static class TransactionExtensions
    {
        /// <summary>
        /// Creates a new transaction scope with this isolation level.
        /// </summary>
        public static TransactionScope CreateScope(this IsolationLevel isolationLevel)
        {
            return CreateScope(isolationLevel, TransactionScopeOption.Required);
        }

        /// <summary>
        /// Creates a new transaction scope with this isolation level.
        /// </summary> public static TransactionScope CreateScope(this IsolationLevel isolationLevel, TransactionScopeOption scopeOption)
        public static TransactionScope CreateScope(this IsolationLevel isolationLevel, TransactionScopeOption scopeOption)
        {
            var options = new TransactionOptions
            {
                IsolationLevel = isolationLevel,
                Timeout = TransactionManager.DefaultTimeout
            };
            return new TransactionScope(scopeOption, options);
        }
    }
}
