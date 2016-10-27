using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using MSharp.Framework.Data.QueryOptions;

namespace MSharp.Framework.Data.Ado.Net
{

    public class SqlQueryBuilder<T> where T : IEntity
    {
        ICriterion[] Criteria;
        QueryOption[] QueryOptions;
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();
        Dictionary<string, string> PropertyMappings;
        Func<IEnumerable<PropertySubqueryMapping>> SubQueryMappings;

        /// <summary>
        /// Creates a new SqlQueryBuilder instance.
        /// </summary>
        public SqlQueryBuilder(IEnumerable<ICriterion> criteria, QueryOption[] options, Dictionary<string, string> propertyMappings, Func<IEnumerable<PropertySubqueryMapping>> subQueryMappings = null)
        {
            if (options.OfType<FullTextSearchQueryOption>().Any())
                throw new Exception("FullTextSearchQueryOption is not supported.");

            if (options.OfType<RangeQueryOption>().Any())
                throw new Exception("RangeQueryOption is not supported.");

            Criteria = criteria?.ToArray() ?? new ICriterion[0];
            QueryOptions = options;
            PropertyMappings = propertyMappings;

            SubQueryMappings = subQueryMappings;
        }

        /// <summary>
        /// Generates the sort statement from all the query options.
        /// </summary>
        string GenerateSort()
        {
            var result = QueryOptions.OfType<SortQueryOption>().Select(GenerateSort).ToList();

            result.AddRange(QueryOptions.OfType<PagingQueryOption>().Select(GenerateSort));

            return result.Trim().ToString(", ").WithPrefix(" ORDER BY ");
        }

        string GenerateSort(SortQueryOption option)
        {
            return PropertyMappings[option.Property] + " DESC".OnlyWhen(option.Descending);
        }

        string GenerateSort(PagingQueryOption option)
        {
            if (option.OrderBy.IsEmpty())
                throw new ArgumentException("Invalid PagingQueryOption specified. OrderBy is mandatory.");

            if (option.PageSize < 1)
                throw new ArgumentException("Invalid PagingQueryOption specified. PageSize should be a positive number.");

            return option.OrderBy + " OFFSET " + option.StartIndex + " ROWS FETCH NEXT " + option.PageSize + " ROWS ONLY";
        }

        /// <summary>
        /// Generates the criteria of this query.
        /// </summary>
        string GenerateWhere()
        {
            return " WHERE {0} IS NOT NULL".FormatWith(PropertyMappings["ID"])
                + Criteria.Select(BuildCriteria).Trim().Select(x => " AND " + x).ToString("");
        }

        string BuildCriteria(ICriterion criterion, Type type, Dictionary<string, string> propertyMappings)
        {
            string column;

            var key = criterion.PropertyName;

            if (propertyMappings.LacksKey(key) && key.EndsWith("Id")) key = key.TrimEnd("Id");

            try
            {
                column = propertyMappings[key];
            }
            catch (KeyNotFoundException)
            {
                var error = "There is no property mapping for '" + criterion.PropertyName +
                    "'. Only mapped properties can be queried upon in the database layer. Calculated properties should be evaluated in the application layer using .Where() method.";

                throw new Exception(error);
            }

            var value = criterion.Value;
            var function = criterion.FilterFunction;

            if (value == null)
            {
                return "{0} IS {1} NULL".FormatWith(column, "NOT".OnlyWhen(function != FilterFunction.Is));
            }

            object valueData = value;
            if (function == FilterFunction.Contains || function == FilterFunction.NotContains) valueData = "%{0}%".FormatWith(value);
            else if (function == FilterFunction.BeginsWith) valueData = "{0}%".FormatWith(value);
            else if (function == FilterFunction.EndsWith) valueData = "%{0}".FormatWith(value);
            else if (function == FilterFunction.In)
            {
                if (value == "()") return "1 = 0 /*" + column + " IN ([empty])*/";
                else return column + " " + function.GetDatabaseOperator() + " " + value;
            }
            else
            {
                DateTime asDate;
                if (DateTime.TryParse(value, out asDate))
                {
                    var property = type.GetProperty(criterion.PropertyName);
                    if (property == null)
                        throw new Exception("Property {0} not found on the type {1}".FormatWith(criterion.PropertyName, type.FullName));
                    var propertyType = property.PropertyType;
                    if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?)) valueData = asDate;
                }
            }

            var parameterName = GetUniqueParameterName(column);

            Parameters.Add(parameterName, valueData);

            return column + " " + function.GetDatabaseOperator() + " @" + parameterName;
        }

        /// <summary>
        /// Builds a criteria clause (and parameters) for a specified condition.
        /// </summary>
        string BuildCriteria(ICriterion criterion)
        {
            var asDirect = criterion as DirectDatabaseCriterion;
            if (asDirect != null)
            {
                if (asDirect.Parameters != null && asDirect.Parameters.Any())
                {
                    asDirect.Parameters.Do(x => Parameters.Add(x.Key, x.Value));
                }

                return asDirect.MapSqlCriteria(PropertyMappings);
            }

            //var asBinary = criterion as BinaryCriterion;
            //if (asBinary != null)
            //{
            //    var sql = ParseBinary(asBinary, criteria: string.Empty, opt: asBinary.Operator);

            //    sql = FixParenthesis(sql);

            //    return sql;
            //}

            return CreateSqlStatement(criterion);
        }

        //string FixParenthesis(string sql)
        //{
        //    if (sql.EndsWith("AND )")) sql = sql.Substring(0, sql.Length - 5) + ")";
        //    if (sql.EndsWith("OR )")) sql = sql.Substring(0, sql.Length - 4) + ")";
        //    var openParenthesis = sql.Count(it => it == '(');
        //    var closeParenthesis = sql.Count(it => it == ')');

        //    if (openParenthesis > closeParenthesis)
        //        sql = sql.Remove(0, openParenthesis - closeParenthesis);
        //    else if (closeParenthesis > openParenthesis)
        //        sql = sql.Remove(sql.Length - (closeParenthesis - openParenthesis) + 1, closeParenthesis - openParenthesis);
        //    return sql;
        //}

        //string ParseBinary(BinaryCriterion binary)
        //{
        //    if (binary == null)
        //    {
        //        // This is not a recognised SQL command. Eg. for calculated properties.
        //        // Hardcode TRUE here, since the application layer will run the whole condition again after the filetered result of the database anyway.
        //        return "(1 = 1)";
        //    }

        //    criteria += "(";
        //    if (binary.Left is BinaryCriterion)
        //        criteria = "(" +  ParseBinary(binary.Left as BinaryCriterion, criteria, (binary.Left as BinaryCriterion).Operator) +")";
        //    else
        //    {
        //        if (criteria.EndsWith(")"))
        //            criteria += $" {opt.ToString()} " + CreateSqlStatement(binary.Left) + $" {opt.ToString()} ";
        //        else
        //            criteria += CreateSqlStatement(binary.Left) + $" {opt.ToString()} ";
        //    }

        //    if (binary.Right is BinaryCriterion)
        //        criteria += ParseBinary(binary.Right as BinaryCriterion, criteria, (binary.Right as BinaryCriterion).Operator);
        //    else
        //    {
        //        if (criteria.EndsWith(")"))
        //            criteria += $" {opt.ToString()} " + CreateSqlStatement(binary.Right) + $" {opt.ToString()} ";
        //        else
        //            criteria += CreateSqlStatement(binary.Right) + $" {opt.ToString()} ";
        //    }

        //    criteria += ")";
        //    return FixParenthesis(criteria);
        //}

        //string ParseBinary1(BinaryCriterion binary, string criteria, BinaryOperator opt)
        //{
        //    if (binary == null)
        //    {
        //        // This is not a recognised SQL command. Eg. for calculated properties.
        //        // Hardcode TRUE here, since the application layer will run the whole condition again after the filetered result of the database anyway.
        //        return "(1 = 1)";
        //    }

        //    criteria += "(";
        //    if (binary.Left is BinaryCriterion)
        //        criteria += ParseBinary(binary.Left as BinaryCriterion, criteria, (binary.Left as BinaryCriterion).Operator);
        //    else
        //    {
        //        if (criteria.EndsWith(")"))
        //            criteria += $" {opt.ToString()} " + CreateSqlStatement(binary.Left) + $" {opt.ToString()} ";
        //        else
        //            criteria += CreateSqlStatement(binary.Left) + $" {opt.ToString()} ";
        //    }

        //    if (binary.Right is BinaryCriterion)
        //        criteria += ParseBinary(binary.Right as BinaryCriterion, criteria, (binary.Right as BinaryCriterion).Operator);
        //    else
        //    {
        //        if (criteria.EndsWith(")"))
        //            criteria += $" {opt.ToString()} " + CreateSqlStatement(binary.Right) + $" {opt.ToString()} ";
        //        else
        //            criteria += CreateSqlStatement(binary.Right) + $" {opt.ToString()} ";
        //    }

        //    criteria += ")";
        //    return FixParenthesis(criteria);
        //}

        string CreateSqlStatement(ICriterion criterion)
        {
            var asBinary = criterion as BinaryCriterion;
            if (asBinary != null)
                return $"({CreateSqlStatement(asBinary.Left)} {asBinary.Operator} {CreateSqlStatement(asBinary.Right)} )";

            return CreateSimpleSqlStatement(criterion);
        }

        string CreateSimpleSqlStatement(ICriterion criterion)
        {
            if (criterion == null) return "(1 = 1)";

            if (criterion.PropertyName.Contains("."))
            {
                var parts = criterion.PropertyName.Split('.');

                if (SubQueryMappings == null)
                    throw new NotSupportedException("The data provider class for '{0}' does not support Nested queries.".FormatWith(typeof(T).Name));

                var mappings = new List<PropertySubqueryMapping>();
                Criterion subCriterion = null;
                var type = typeof(T);

                if (parts.Count() > 2)
                {
                    mappings.Add(SubQueryMappings?.Invoke().FirstOrDefault(x => x.Properties == parts[0] + ".*"));

                    for (int i = 0; i < parts.Length - 2; i++)
                    {
                        type = type.GetProperty(parts[i]).PropertyType;

                        var dataProvider = typeof(T).Assembly.GetTypes().SingleOrDefault(t => t.Name == $"{type.Name}DataProvider" && t.Implements<IDataProvider>());

                        if (dataProvider == null) throw new Exception($"Could not find the DataProvider class for {type.FullName}.");

                        var getSubQueryMappingsMethod = dataProvider.GetMethod("GetSubQueryMappings", BindingFlags.Static | BindingFlags.NonPublic);
                        if (getSubQueryMappingsMethod == null) throw new Exception($"Could not find the method GetSubQueryMappings() on {dataProvider.FullName}.");

                        var mapping = (getSubQueryMappingsMethod.Invoke(null, null) as IEnumerable<PropertySubqueryMapping>)
                            .FirstOrDefault(x => x.Properties == parts[i + 1] + ".*");

                        if (mapping == null) throw new Exception($"{dataProvider.FullName}.GetSubQueryMappings() did not provide a mapping for: {parts[i + 1]}.*");

                        var subquerySplitted = mapping.Subquery.Split('=');
                        var tablePrefix = subquerySplitted.Last().Split('.')[0];
                        var aliasName = mappings[i].Details.First().Value.Split('.')[0];
                        mapping.Subquery = $"{subquerySplitted[0]} = {subquerySplitted[1].Replace(tablePrefix, aliasName)}";
                        mappings.Add(mapping);
                    }

                    subCriterion = new Criterion(parts[parts.Length - 1], criterion.FilterFunction, criterion.Value);
                    type = type.GetProperty(parts[parts.Length - 2]).PropertyType;
                    var criteria = new StringBuilder();
                    var parenthesis = "";
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        criteria.AppendLine($" EXISTS ({mappings[i].Subquery.WithSuffix(" AND ")}");
                        parenthesis += ")";
                    }
                    criteria.Append(BuildCriteria(subCriterion, type, mappings.Last().Details));
                    criteria.Append(parenthesis);

                    return criteria.ToString();
                }
                else
                {
                    var mapping = SubQueryMappings?.Invoke().FirstOrDefault(x => x.Properties == parts[0] + ".*");

                    if (mapping == null)
                        throw new NotSupportedException("The data provider class for '{0}' does not provide a sub-query mapping for '{1}'.".FormatWith(typeof(T).Name, parts[0]));

                    subCriterion = new Criterion(parts[1], criterion.FilterFunction, criterion.Value);
                    type = typeof(T).GetProperty(parts[0]).PropertyType;
                    return "EXISTS ({0}{1})".FormatWith(mapping.Subquery, BuildCriteria(subCriterion, type, mapping.Details).WithPrefix(" AND "));
                }
            }
            else
            {
                return BuildCriteria(criterion, typeof(T), PropertyMappings);
            }
        }


        string GetUniqueParameterName(string column)
        {
            var result = column.Remove("[").Remove("]").Replace(".", "_");

            if (Parameters.ContainsKey(result))
            {
                for (var i = 2; ; i++)
                {
                    var name = result + "_" + i;
                    if (!Parameters.ContainsKey(name))
                    {
                        return name;
                    }
                }
            }

            return result;
        }

        string GenerateTop()
        {
            var option = QueryOptions.OfType<ResultSetSizeQueryOption>().FirstOrDefault();
            if (option == null) return null;
            else return "TOP " + option.Number + " ";
        }

        public string GenerateQuery(string columnsList, string fromTables)
        {
            var r = new StringBuilder("SELECT ");

            r.Append(GenerateTop());

            r.Append(columnsList);

            r.AppendLine(" FROM " + fromTables);

            r.Append(GenerateWhere());

            r.Append(GenerateSort());

            return r.ToString();
        }

        public string GenerateCountQuery(string fromTables)
        {
            return "SELECT COUNT({0}) FROM {1}".FormatWith(PropertyMappings["ID"], fromTables) + GenerateWhere();
        }
    }
}