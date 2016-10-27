using System;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Transactions;

namespace MSharp.Framework.Services
{
    /// <summary>
    /// A data-reader style interface for reading Csv files.
    /// </summary>
    public static class CsvReader
    {
        /// <summary>
        /// Reads a CSV document into a data table. Note use the CastTo() method on the returned DataTable to gain fully-typed objects.
        /// </summary>
        public static DataTable Read(Document csvDocument, bool isFirstRowHeaders, int minimumFieldCount = 0)
        {
            return Read(csvDocument.GetContentText(), isFirstRowHeaders, minimumFieldCount);
        }

        /// <summary>
        /// Reads a CSV file into a data table. Note use the CastTo() method on the returned DataTable to gain fully-typed objects.
        /// </summary>
        public static DataTable Read(FileInfo csvFile, bool isFirstRowHeaders, int minimumFieldCount = 0)
        {
            return Read(File.ReadAllText(csvFile.FullName), isFirstRowHeaders, minimumFieldCount);
        }

        /// <summary>
        /// Reads a CSV piece of string into a data table using OleDb. Note use the CastTo() method on the returned DataTable to gain fully-typed objects.
        /// </summary>
        public static DataTable ReadUsingOleDb(string csvContent, bool isFirstRowHeaders)
        {
            var localFile = Path.GetTempFileName();
            try
            {
                localFile.AsFile().WriteAllText(csvContent);

                var folder = Path.GetDirectoryName(localFile);
                var hdr = isFirstRowHeaders ? "YES" : "NO";

                var connectionString = "Provider=Microsoft.Jet.OleDb.4.0; Data Source={0}; Extended Properties=\"Text;HDR={1};FMT=Delimited; IMEX=1;\"".FormatWith(folder, hdr);

                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    using (var conn = new OleDbConnection(connectionString))
                    {
                        using (var adapter = new OleDbDataAdapter("SELECT * FROM [{0}]".FormatWith(Path.GetFileName(localFile)), conn))
                        {
                            var result = new DataSet("CSV File");
                            adapter.Fill(result);
                            return result.Tables[0];
                        }
                    }
                }
            }
            finally
            {
                File.Delete(localFile);
            }
        }

        /// <summary>
        /// Reads a CSV piece of string into a data table. Note use the CastTo() method on the returned DataTable to gain fully-typed objects.
        /// </summary>
        public static DataTable Read(string csvContent, bool isFirstRowHeaders, int minimumFieldCount = 0)
        {
            var output = new DataTable();

            using (var csv = new LumenWorks.Framework.IO.Csv.CsvDataReader(new StringReader(csvContent), isFirstRowHeaders))
            {
                csv.MissingFieldAction = LumenWorks.Framework.IO.Csv.MissingFieldAction.ReplaceByNull;
                var fieldCount = Math.Max(csv.FieldCount, minimumFieldCount);
                var headers = csv.GetFieldHeaders();

                if (!isFirstRowHeaders)
                {
                    headers = Enumerable.Range(0, fieldCount).Select(i => "Column" + i).ToArray();
                }

                for (int i = 0; i < fieldCount; i++)
                {
                    output.Columns.Add(new DataColumn(headers[i], typeof(string)));
                }

                while (csv.ReadNextRecord())
                {
                    var row = output.NewRow();

                    for (int i = 0; i < fieldCount; i++) row[i] = csv[i];

                    output.Rows.Add(row);
                }
            }

            return output;
        }

        /// <summary>
        /// Gets the column names on the specified CSV document.
        /// </summary>
        public static string[] GetColumns(Document document)
        {
            return GetColumns(document.GetContentText());
        }

        /// <summary>
        /// Gets the column names on the specified CSV content.
        /// </summary>
        public static string[] GetColumns(string csvContent)
        {
            using (var csv = new LumenWorks.Framework.IO.Csv.CsvDataReader(new StringReader(csvContent), true))
            {
                return csv.GetFieldHeaders();
            }
        }
    }
}