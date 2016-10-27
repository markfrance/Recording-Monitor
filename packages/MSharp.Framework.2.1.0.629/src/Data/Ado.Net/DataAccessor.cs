namespace MSharp.Framework.Data
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;

    /// <summary>
    /// Provides a DataAccessor implementation for System.Data.SqlClient 
    /// </summary>
    public class DataAccessor : DataAccessor<SqlConnection, SqlDataAdapter> { }

    /// <summary>
    /// ADO.NET Facade for submitting single method commands.
    /// </summary>
    public class DataAccessor<TConnection, TDataAdapter>
        where TConnection : IDbConnection, new()
        where TDataAdapter : IDbDataAdapter, new()
    {
        #region Manage connection
        /// <summary>
        /// Creates a new DB Connection to database with the given connection string.
        /// </summary>		
        public static TConnection CreateConnection(string connectionString)
        {
            var result = new TConnection { ConnectionString = connectionString };

            result.Open();

            return result;
        }

        /// <summary>
        /// Creates a connection object.
        /// </summary>
        public static TConnection CreateConnection()
        {
            var result = DbTransactionScope.Root?.GetDbConnection();
            if (result != null) return (TConnection)result;
            else return CreateActualConnection();
        }

        public static string GetCurrentConnectionString()
        {
            string result;

            if (DatabaseContext.Current != null) result = DatabaseContext.Current.ConnectionString;
            else result = Config.GetConnectionString("AppDatabase");

            if (result.IsEmpty())
                throw new ConfigurationErrorsException("No 'AppDatabase' connection string is specified in the application config file.");

            return result;
        }

        /// <summary>
        /// Creates a connection object.
        /// </summary>
        internal static TConnection CreateActualConnection()
        {
            return CreateConnection(GetCurrentConnectionString());
        }
        #endregion

        static IDbCommand CreateCommand(CommandType type, string commandText, params IDataParameter[] @params)
        {
            return CreateCommand(type, commandText, default(TConnection), @params);
        }

        static IDbCommand CreateCommand(CommandType type, string commandText, TConnection connection, params IDataParameter[] @params)
        {
            if (connection == null) connection = CreateConnection();

            var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.CommandType = type;

            command.Transaction = DbTransactionScope.Root?.GetDbTransaction() ?? command.Transaction;

            command.CommandTimeout = DatabaseContext.Current?.CommandTimeout ?? (Config.TryGet<int?>("Sql.Command.TimeOut")) ?? command.CommandTimeout;

            foreach (var param in @params)
                command.Parameters.Add(param);

            return command;
        }

        /// <summary>
        /// Executes the specified command text as nonquery.
        /// </summary>
        public static int ExecuteNonQuery(string commandText)
        {
            return ExecuteNonQuery(commandText, CommandType.Text);
        }

        static DataAccessProfiler.Watch StartWatch(string command)
        {
            if (DataAccessProfiler.IsEnabled) return DataAccessProfiler.Start(command);
            else return null;
        }

        /// <summary>
        /// Executes the specified command text as nonquery.
        /// </summary>
        public static int ExecuteNonQuery(string command, CommandType commandType, params IDataParameter[] @params)
        {
            var dbCommand = CreateCommand(commandType, command, @params);

            var watch = StartWatch(command);

            try
            {
                return dbCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Error in running Non-Query SQL command.", ex).AddData("Command", command)
                    .AddData("Parameters", @params.Get(l => l.Select(p => p.ParameterName + "=" + p.Value).ToString(" | ")))
                    .AddData("ConnectionString", dbCommand.Connection.ConnectionString);
            }
            finally
            {
                dbCommand.Parameters.Clear();

                CloseConnection(dbCommand.Connection);

                if (watch != null) DataAccessProfiler.Complete(watch);
            }
        }

        static void CloseConnection(IDbConnection connection)
        {
            if (DbTransactionScope.Root == null)
            {
                if (connection.State != ConnectionState.Closed)
                    connection.Close();
            }
        }

        /// <summary>
        /// Executes the specified command text as nonquery.
        /// </summary>
        public static int ExecuteNonQuery(CommandType commandType, List<KeyValuePair<string, IDataParameter[]>> commands)
        {
            var connection = CreateConnection();
            var result = 0;

            try
            {
                foreach (var c in commands)
                {
                    var watch = StartWatch(c.Key);

                    IDbCommand dbCommand = null;
                    try
                    {
                        dbCommand = CreateCommand(commandType, c.Key, connection, c.Value);
                        result += dbCommand.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error in executing SQL command.", ex).AddData("Command", c.Key)
                            .AddData("Parameters", c.Value.Get(l => l.Select(p => p.ParameterName + "=" + p.Value).ToString(" | ")));
                    }
                    finally
                    {
                        dbCommand?.Parameters.Clear();

                        if (watch != null) DataAccessProfiler.Complete(watch);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("Error in running Non-Query SQL commands.", ex).AddData("ConnectionString", connection.ConnectionString);
            }
            finally
            {
                CloseConnection(connection);
            }
        }

        /// <summary>
        /// Executes the specified command text against the database connection of the context and builds an IDataReader.
        /// Make sure you close the data reader after finishing the work.
        /// </summary>
        public static IDataReader ExecuteReader(string command, CommandType commandType, params IDataParameter[] @params)
        {
            var watch = StartWatch(command);

            var dbCommand = CreateCommand(commandType, command, @params);

            try
            {
                if (DbTransactionScope.Root != null) return dbCommand.ExecuteReader();
                else return dbCommand.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch (Exception ex)
            {
                throw new Exception("Error in running SQL Query.", ex).AddData("Command", command)
                    .AddData("Parameters", @params.Get(l => l.Select(p => p.ParameterName + "=" + p.Value).ToString(" | ")))
                    .AddData("ConnectionString", dbCommand.Connection.ConnectionString);
            }
            finally
            {
                dbCommand.Parameters.Clear();
                if (watch != null) DataAccessProfiler.Complete(watch);
            }
        }

        /// <summary>
        /// Executes the specified command text against the database connection of the context and returns the single value of the type specified.
        /// </summary>
        public static T ExecuteScalar<T>(string commandText)
        {
            return (T)ExecuteScalar(commandText);
        }

        /// <summary>
        /// Executes the specified command text against the database connection of the context and returns the single value.
        /// </summary>
        public static object ExecuteScalar(string commandText)
        {
            return ExecuteScalar(commandText, CommandType.Text);
        }

        /// <summary>
        /// Executes the specified command text against the database connection of the context and returns the single value.
        /// </summary>
        public static object ExecuteScalar(string command, CommandType commandType, params IDataParameter[] @params)
        {
            var watch = StartWatch(command);
            var dbCommand = CreateCommand(commandType, command, @params);

            try
            {
                return dbCommand.ExecuteScalar();
            }
            catch (Exception ex)
            {
                throw new Exception("Error in running Scalar SQL Command.", ex).AddData("Command", command)
                    .AddData("Parameters", @params.Get(l => l.Select(p => p.ParameterName + "=" + p.Value).ToString(" | ")))
                    .AddData("ConnectionString", dbCommand.Connection.ConnectionString);
            }
            finally
            {
                dbCommand.Parameters.Clear();

                CloseConnection(dbCommand.Connection);

                if (watch != null) DataAccessProfiler.Complete(watch);
            }
        }

        /// <summary>
        /// Executes a database query and returns the result as a data set.
        /// </summary>        
        public static DataSet ReadData(string databaseQuery, params IDataParameter[] @params)
        {
            return ReadData(databaseQuery, CommandType.Text, @params);
        }

        /// <summary>
        /// Executes a database query and returns the result as a data set.
        /// </summary>        
        public static DataSet ReadData(string databaseQuery, CommandType commandType, params IDataParameter[] @params)
        {
            using (var command = CreateCommand(commandType, databaseQuery, @params))
            {
                var watch = StartWatch(databaseQuery);
                try
                {
                    var result = new DataSet();
                    var adapter = new TDataAdapter { SelectCommand = command };
                    adapter.Fill(result);
                    command.Parameters.Clear();

                    return result;
                }
                catch (Exception ex)
                {
                    throw new Exception("Error in running SQL Query.", ex).AddData("Command", command.CommandText)
                    .AddData("Parameters", @params.Get(l => l.Select(p => p.ParameterName + "=" + p.Value).ToString(" | ")))
                    .AddData("ConnectionString", command.Connection.ConnectionString);
                }
                finally
                {
                    command.Parameters.Clear();

                    CloseConnection(command.Connection);

                    if (watch != null) DataAccessProfiler.Complete(watch);
                }
            }
        }
    }
}