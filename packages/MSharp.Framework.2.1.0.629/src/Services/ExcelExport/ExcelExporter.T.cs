using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MSharp.Framework.Services
{

    public partial class ExcelExporter<T>
    {
        public static string LinkSeperator { get { return Convert.ToChar(166).ToString(); } }

        /// <summary>
        /// Creates a new ExcelExporter instance.
        /// </summary>
        public ExcelExporter(string documentName)
        {
            DocumentName = documentName;
            HeaderGroupBackgroundColor = HeaderBackGroundColor = "#CCCCCC";
            HeaderFontName = "Arial";

        }

        /// <summary>
        /// Creates a new ExcelExporter instance for a data table.
        /// It automatically configures the exporter for all columns and rows of the data table.
        /// </summary>
        public ExcelExporter(System.Data.DataTable dataTable)
        {
            if (dataTable == null)
                throw new ArgumentNullException("dataTable");

            DocumentName = dataTable.TableName;
            HeaderBackGroundColor = "#CCCCCC";

            foreach (System.Data.DataColumn column in dataTable.Columns)
                AddColumn(column.ColumnName);// TODO: Add data type when necessary

            foreach (System.Data.DataRow row in dataTable.Rows)
                AddRow(row.ItemArray);
        }

        #region DocumentName
        /// <summary>
        /// Gets or sets the DocumentName of this ExcelExporter.
        /// </summary>
        public string DocumentName { get; set; }
        #endregion

        #region HeaderBackGroundColor
        /// <summary>
        /// Gets or sets the HeaderBackGroundColor of this ExcelExporter.
        /// </summary>
        public string HeaderBackGroundColor { get; set; }
        #endregion

        #region HeaderFontName
        /// <summary>
        /// Gets or sets the HeaderFontName of this ExcelExporter.
        /// </summary>
        public string HeaderFontName { get; set; }
        #endregion

        #region HeaderGroupBackgroundColor
        /// <summary>
        /// Gets or sets the HeaderGroupBackgroundColor of this ExcelExporter.
        /// </summary>
        public string HeaderGroupBackgroundColor { get; set; }
        #endregion

        public bool FreezeHeader { get; set; }

        public bool FreezeFirstColumn { get; set; }

        public double DefaultColumnWidth { get; set; }

        /// <summary>
        /// Gets or sets the IncludeHeader of this ExcelExporter.
        /// </summary>
        public bool ExcludeHeader { get; set; }

        public List<ExcelColumn<T>> Columns = new List<ExcelColumn<T>>();
        public List<object[]> DataRows = new List<object[]>();

        public ExcelColumn<T> GetColumn(string headerText)
        {
            return Columns.FirstOrDefault(x => x.HeaderText == headerText);
        }


        /// <summary>
        /// Adds a header cell.
        /// </summary>
        public ExcelColumn<T> AddColumn(string headerText)
        {
            return AddColumn(headerText, "String");
        }

        /// <summary>
        /// Adds a header cell.
        /// </summary>
        public ExcelColumn<T> AddColumn(string headerText, string type)
        {
            return AddColumn(headerText, type, default(Func<T, object>));
        }

        /// <summary>
        /// Adds a header cell.
        /// </summary>
        public ExcelColumn<T> AddColumn(string headerText, string type, Func<T, object> data)
        {
            if (headerText.IsEmpty())
                throw new ArgumentNullException("headerText");

            if (type.IsEmpty())
                throw new ArgumentNullException("type");

            var result = new ExcelColumn<T>(headerText, type) { Data = data };
            Columns.Add(result);
            return result;
        }

        /// <summary>
        /// Removes the column with the specified header text.
        /// </summary>
        public void RemoveColumn(string headerText)
        {
            var columns = Columns.Where(c => c.HeaderText == headerText);
            if (columns.Count() > 1)
                throw new ArgumentException("There are {0} columns with header text of '{1}'. Please use RemoveColumn(index) instead.".FormatWith(columns.Count(), headerText));

            if (columns.None())
                throw new ArgumentException("There is no column with header text of '{0}'.".FormatWith(headerText));

            RemoveColumn(Columns.IndexOf(columns.Single()));
        }

        public void RemoveColumn(ExcelColumn<T> column)
        {
            RemoveColumn(Columns.IndexOf(column));
        }

        /// <summary>
        /// Removes the column at the specified index.
        /// </summary>
        public void RemoveColumn(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex > Columns.Count - 1)
                throw new ArgumentException("columnIndex should be between 0 and " + (Columns.Count - 1));

            Columns.RemoveAt(columnIndex);

            for (var i = 0; i < DataRows.Count; i++)
            {
                DataRows[i] = DataRows[i].Where((r, ind) => ind != columnIndex).ToArray();
            }
        }

        /// <summary>
        /// Adds a data row to the excel output.
        /// <param name="dataCells">Either ExcelCell instances or value objects.</param>
        /// </summary>
        public void AddRow(params object[] dataCells)
        {
            if (dataCells == null)
                throw new ArgumentNullException("dataCells");

            if (Columns.All(x => x.GroupName.IsEmpty()))
            {
                if (dataCells.Length != Columns.Count())
                    throw new ArgumentException("The number of row cell values does not match the number of columns (" + dataCells.Length + " <> " +
                        Columns.Count() + ")");
            }
            else
            {
                // Do we need validation for grouping mode?
            }

            DataRows.Add(dataCells);
        }

        public void AddRows(IEnumerable<T> dataItems)
        {
            if (dataItems == null)
                throw new ArgumentNullException("dataItems");

            foreach (var column in Columns.Where(c => c.Data == null))
                throw new Exception("ExcelColumn.Data should be specified for ExcelExporter.AddRows() method to work. For '{0}' it is null.".FormatWith(column.HeaderText));

            foreach (var item in dataItems)
            {
                var dataCells = new List<object>();

                foreach (var column in Columns)
                {
                    try
                    {
                        dataCells.Add(column.Data(item));
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Invoking the Data evaluator for excel column '{0}' failed on {1} instance: '{2}'".FormatWith(
                            column.HeaderText, item.GetType().Name, item.ToString()), ex);
                    }
                }

                AddRow(dataCells.ToArray());
            }
        }

        public ExcelColumn<T> AddDropDownColumn(string headerText, string type, string enumerationName, IEnumerable<object> possibleValues)
        {
            if (headerText.IsEmpty())
                throw new ArgumentNullException("headerText");

            if (type.IsEmpty())
                throw new ArgumentNullException("type");

            if (possibleValues == null)
                throw new ArgumentNullException("possibleValues");

            var result = new ExcelDropDownColumn<T>(headerText, type, enumerationName, possibleValues.ToArray());
            Columns.Add(result);

            return result;
        }

        /// <summary>
        /// Generates the content of the output Excel file.
        /// </summary>
        public string Generate(ExcelExporter.Output output)
        {
            switch (output)
            {
                case ExcelExporter.Output.Csv:
                    return GenerateCsv();
                case ExcelExporter.Output.ExcelXml:
                    return GenerateExcelXml(this);
                default:
                    throw new NotSupportedException();
            }
        }

        string GenerateCsv()
        {
            var r = new StringBuilder();

            // Header row:
            if (!ExcludeHeader)
            {
                r.AppendLine(Columns.Select(c => EscapeCsvValue(c.HeaderText)).ToString(","));
            }

            // Data rows:

            foreach (var row in DataRows)
            {
                var fields = new List<string>();
                for (int i = 0; i < row.Length; i++)
                {
                    var cell = row[i];
                    var column = Columns[i];

                    var value = cell?.ToString().OrEmpty();

                    if (column.DataType == "Link")
                    {
                        if (value.IsEmpty())
                            fields.Add(value);
                        else
                        {
                            var parts = value.Split(LinkSeperator.ToCharArray().Single());

                            if (parts.Length != 2)
                                throw new Exception("Invalid Link value for ExporttoExcel: " + value);

                            fields.Add(parts[0] + ": " + parts[1]);
                        }
                    }
                    else
                    {
                        fields.Add(value);
                    }
                }

                r.AppendLine(fields.Select(f => EscapeCsvValue(f)).ToString(","));
            }

            return r.ToString();
        }

        static string EscapeCsvValue(string value)
        {
            if (value.IsEmpty()) return string.Empty;

            value = value.ReplaceAll("\r", "").Replace("\n", "\r\n");

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                value = "\"{0}\"".FormatWith(value.Replace("\"", "\"\""));
            }

            return value;
        }

        #region Generate Excel Xml

        string GenerateExcelWorksheet()
        {
            var r = new StringBuilder();
            r.AddFormattedLine(@"<Worksheet ss:Name=""{0}"">",
                DocumentName.Remove("/", @"\", "?", "*", ":", "[", "]", "\r", "\n").ReplaceAll("  ", " ").Summarize(31, enforceMaxLength: true).XmlEncode());
            r.AddFormattedLine(@"<Table DefaultColumnWidth=""{0}"">", DefaultColumnWidth);

            r.AppendLine(Columns.Select((h, i) => GenerateColumnTag(h, i + 1)).Trim().ToLinesString());

            r.AppendLine(GenerateHeaderGroupings());

            if (!ExcludeHeader)
            {
                r.AppendLine(GenerateSheetHeaderRow());
            }

            r.AppendLine(GenerateDataRows());

            r.AppendLine(@"</Table>");

            r.AppendLine(GenerateDropDownDataValidation());

            r.AppendLine(GenerateWorksheetSettings());

            r.AppendLine(@"</Worksheet>");

            return r.ToString();
        }

        internal string GenerateColumnTag(ExcelColumn<T> column, int index)
        {
            if (column.Width == null) return null;

            var r = new StringBuilder();

            r.Append($"<Column ss:Index=\"{index}\"");

            if (column.Width.HasValue)
                r.Append($" ss:Width=\"{column.Width}\"");

            r.Append("/>");

            return r.ToString();
        }

        private string GenerateWorksheetSettings()
        {

            var frozenHeaderSetting = "<SplitHorizontal>1</SplitHorizontal><TopRowBottomPane>1</TopRowBottomPane>";
            var frozenFirstColumnSetting = "<SplitVertical>1</SplitVertical><LeftColumnRightPane>1</LeftColumnRightPane>";

            return @"<WorksheetOptions xmlns=""urn:schemas-microsoft-com:office:excel"">
                     <Unsynced/>
                     <Selected/>
                     <FreezePanes/>
                     <FrozenNoSplit/>
                     {0}
                     {1}
                     <ProtectObjects>False</ProtectObjects>
                     <ProtectScenarios>False</ProtectScenarios>
                    </WorksheetOptions>"
                    .FormatWith(
                    frozenHeaderSetting.OnlyWhen(FreezeHeader),
                    frozenFirstColumnSetting.OnlyWhen(FreezeFirstColumn));
        }

        private string GenerateHeaderGroupings()
        {
            var groups = GetGroups();

            if (groups.None()) return string.Empty;

            var r = new StringBuilder();

            r.AppendLine(@"<Row>");

            foreach (var g in groups)
                r.AddFormattedLine("<Cell ss:StyleID=\"{0}\" ss:MergeAcross=\"{1}\"><Data ss:Type=\"String\" >{2}</Data></Cell>", g.Style.GetStyleId(), g.Quantity, g.GroupName);

            r.AppendLine("</Row>");


            return r.ToString();
        }

        private IEnumerable<ColumnGroup> GetGroups()
        {
            var result = new List<ColumnGroup>();

            if (Columns.All(i => i.GroupName.IsEmpty())) return result; // No grouping has been provided.

            foreach (var column in Columns)
            {
                var previousGroup = result.LastOrDefault(r => r.GroupName == column.GroupName);

                if (previousGroup != null)
                {
                    previousGroup.Quantity++;
                }
                else
                {
                    previousGroup = new ColumnGroup { GroupName = column.GroupName, Quantity = 0, Style = column.GroupingStyle };
                    result.Add(previousGroup);
                }
            }

            return result;
        }

        class ColumnGroup
        {
            internal string GroupName;
            internal int Quantity;
            internal ExcelCellStyle Style;
        }

        public static string GenerateExcelXml(params ExcelExporter<T>[] sheets)
        {
            if (sheets == null || sheets.None())
                throw new ArgumentException("No excel sheets specified.");

            if (sheets.GroupBy(s => s.DocumentName).Any(x => x.Count() > 1))
                throw new ArgumentException("Sheet names should be unique. At least 2 sheets in the provided list have the same DocumentName.");

            var r = new StringBuilder();

            r.AppendLine(@"<?xml version=""1.0""?><?mso-application progid=""Excel.Sheet""?>");
            r.AppendLine(@"<Workbook xmlns=""urn:schemas-microsoft-com:office:spreadsheet""");
            r.AppendLine(@"xmlns:o=""urn:schemas-microsoft-com:office:office""");
            r.AppendLine(@"xmlns:x=""urn:schemas-microsoft-com:office:excel""");
            r.AppendLine(@"xmlns:ss=""urn:schemas-microsoft-com:office:spreadsheet""");
            r.AppendLine(@"xmlns:html=""http://www.w3.org/TR/REC-html40"">");

            // Generate styles
            r.AppendLine(GenerateStyles(sheets));

            // NamedRanges:
            var namedRanges = sheets.SelectMany(s => s.Columns.OfType<ExcelDropDownColumn<T>>()).Distinct(c => c.EnumerationName);
            var nameRangeNodes = namedRanges.Select(c => "<NamedRange ss:Name=\"{0}\" ss:RefersTo=\"={0}!R1C1:R{1}C1\"/>".FormatWith(c.EnumerationName, c.PossibleValues.Length));
            r.AddFormattedLine("<Names>{0}</Names>", nameRangeNodes.ToLinesString());

            foreach (var sheet in sheets)
                r.AppendLine(sheet.GenerateExcelWorksheet());

            r.AppendLine(namedRanges.Select(c => GenerateDropDownSourceSheet(c)).ToLinesString());

            r.AppendLine(@"</Workbook>");

            return r.ToString();
        }



        private static string GenerateStyles(params ExcelExporter<T>[] sheets)
        {
            var r = new StringBuilder();

            r.AppendLine("<Styles>");

            // Link style
            r.AddFormattedLine(@"<Style ss:ID=""linkStyle"">
                                 <Font ss:Color=""#0000FF"" ss:Underline=""Single""/>
                                 </Style>", sheets.First().HeaderBackGroundColor);

            // Merge settings:
            sheets.Do(s => s.MergeStyles());

            var uniqueStyles = sheets.SelectMany(x => x.GetAllStyles()).Distinct(x => x.GetStyleId()).ToList();
            foreach (var style in uniqueStyles)
            {
                r.AppendLine(style.GenerateStyle());
            }


            r.AppendLine("</Styles>");

            return r.ToString();
        }

        private IEnumerable<ExcelCellStyle> GetAllStyles()
        {
            var header = Columns.SelectMany(x => new[] { x.HeaderStyle, x.RowStyle });
            var rows = DataRows.SelectMany(x => x.ExceptNull().OfType<ExcelCell>()).Select(x => x.Style);
            var groupings = Columns.Where(c => c.GroupName.HasValue()).Select(x => x.GroupingStyle);

            return header.Concat(rows).Concat(groupings).Distinct(x => x.GetStyleId()).ToArray();
        }

        void MergeStyles()
        {
            foreach (var row in DataRows)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    var cell = row[i] as ExcelCell;

                    if (cell == null) continue;

                    var column = Columns[i];

                    cell.Style = column.RowStyle.OverrideWith(cell.Style);
                }
            }
        }


        /// <summary>
        /// Generates Hidden Worksheets that contain Possible Values for each DropDown
        /// </summary>
        static string GenerateDropDownSourceSheet(ExcelDropDownColumn<T> column)
        {
            var rows = column.PossibleValues.
                       Select(v => @"<Row><Cell><Data ss:Type=""{0}"">{1}</Data><NamedCell ss:Name=""{2}""/></Cell></Row>".FormatWith(column.DataType, v, column.EnumerationName));

            return @"<Worksheet ss:Name=""{0}"">
                                    <Table>
                                        {1}
                                    </Table>
                                    <WorksheetOptions xmlns=""urn:schemas-microsoft-com:office:excel"">
                                        <Visible>SheetHidden</Visible>
                                    </WorksheetOptions>
                                </Worksheet>".FormatWith(column.EnumerationName, rows.ToLinesString());
        }

        /// <summary>
        /// DataValidation assigns a DropDown for each cell and restrics possible values to that drop down
        /// </summary>
        /// <returns></returns>
        string GenerateDropDownDataValidation()
        {
            return Columns.OfType<ExcelDropDownColumn<T>>().Select(c =>
                   @"<DataValidation xmlns=""urn:schemas-microsoft-com:office:excel"">
                        <Type>List</Type>
                        <Range>R1C{0}:R{1}C{0}</Range>
                        <Value>{2}</Value>
                     </DataValidation>".FormatWith(Columns.IndexOf(c) + 1, DataRows.Count + 1, c.EnumerationName)).ToLinesString();

            //var r = new StringBuilder();

            //for (int column = 0; column < Columns.Count; column++)
            //{
            //    var dropDownColumn = Columns[column] as ExcelDropDownColumn;
            //    if (dropDownColumn != null)
            //    {
            //        r.AppendLine("<DataValidation xmlns=\"urn:schemas-microsoft-com:office:excel\">");
            //        r.AddFormattedLine("<Range>R1C{0}:R{1}C{0}</Range>", column + 1, DataRows.Count);
            //        r.AppendLine("<Type>List</Type>");
            //        r.AddFormattedLine("<Value>{0}</Value>", dropDownColumn.EnumerationName);
            //        r.AppendLine("</DataValidation>");
            //    }
            //}

            //return r.ToString();
        }

        string GenerateSheetHeaderRow()
        {
            var r = new StringBuilder();

            r.AppendLine(@"<Row>");

            foreach (var c in Columns)
            {
                r.AppendFormat("<Cell ss:StyleID=\"{0}\">", c.HeaderStyle.GetStyleId());
                r.AddFormattedLine("<Data ss:Type=\"String\">{0}</Data>", c.HeaderText.XmlEncode());
                r.AppendLine("</Cell>");
            }

            r.AppendLine("</Row>");

            return r.ToString();
        }

        string GenerateDataRows()
        {
            var r = new StringBuilder();

            foreach (var row in DataRows)
            {
                r.AppendLine(@"<Row>");

                for (int i = 0; i < row.Length; i++)
                {
                    var cell = row[i];
                    var column = Columns[i];

                    var cellInfo = cell as ExcelCell;

                    var value = cell?.ToString().OrEmpty();

                    if (column.DataType == "Link")
                    {
                        if (value.IsEmpty())
                        {
                            r.AppendLine("<Cell><Data ss:Type=\"String\"></Data></Cell>");
                        }
                        else
                        {
                            var parts = value.Split(LinkSeperator.ToCharArray().Single());

                            if (parts.Length != 2)
                                throw new Exception("Invalid Link value for ExporttoExcel: " + value);

                            r.AddFormattedLine("<Cell ss:StyleID=\"linkStyle\" ss:HRef=\"{0}\"><Data ss:Type=\"String\">{1}</Data></Cell>",
                                parts[1].XmlEncode(),
                                parts[0].XmlEncode());
                        }
                    }
                    else
                    {
                        if (value.HasValue()) value = value.XmlEncode();

                        r.Append("<Cell");

                        var style = cellInfo?.Style ?? column.RowStyle;
                        r.AppendFormat(" ss:StyleID=\"{0}\"", style.GetStyleId());

                        if (column.Formula.HasValue())
                            r.AppendFormat(" ss:Formula=\"{0}\"", column.Formula);

                        r.Append(">");

                        if (value.HasValue())
                            r.AddFormattedLine("<Data ss:Type=\"{0}\">{1}</Data>", column.DataType, value);

                        r.AppendLine("</Cell>");
                    }
                }

                r.AppendLine("</Row>");
            }

            return r.ToString();
        }

        #endregion

        /// <summary>
        /// Gets the file extension for a specified output format.
        /// </summary>
        public string GetFileExtension(ExcelExporter.Output output)
        {
            switch (output)
            {
                case ExcelExporter.Output.ExcelXml:
                    return ".xls";
                case ExcelExporter.Output.Csv:
                    return ".csv";
                default:
                    throw new NotSupportedException();
            }
        }

        public Document ToDocument(ExcelExporter.Output type)
        {
            return new Document(Generate(type).ToBytesWithSignature(Encoding.UTF8), DocumentName + GetFileExtension(type));
        }
    }
}