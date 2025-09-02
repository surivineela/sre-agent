using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Helpers;

public class DataTableResponseObjectCollection
{
    public required IEnumerable<DataTableResponseObject> Tables { get; set; }
}

public class DataTableResponseObject
{
    public required string TableName { get; set; }

    public required IEnumerable<DataTableResponseColumn> Columns { get; set; }

    public required string[][] Rows { get; set; }
}

public class DataTableResponseColumn
{
    public required string ColumnName { get; set; }
    public required string DataType { get; set; }
    public required string ColumnType { get; set; }

    public DataTableResponseColumn(string name, string dataType)
    {
        ColumnName = name;
        DataType = dataType;
    }
}

public static class DataTableExtensions
{
    public static DataTable ToDataTable(this DataTableResponseObject dataTableResponse)
    {
        if (dataTableResponse == null)
        {
            throw new ArgumentNullException("dataTableResponse");
        }

        var dataTable = new DataTable(dataTableResponse.TableName);

        dataTable.Columns.AddRange(dataTableResponse.Columns.Select(column => new DataColumn(column.ColumnName, GetColumnType(column.DataType))).ToArray());

        foreach (var row in dataTableResponse.Rows)
        {
            var rowWithCorrectTypes = new List<object?>();
            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                object? rowValueWithCorrectType = null;

                if (row[i] != null)
                {
                    rowValueWithCorrectType = Convert.ChangeType(row[i], dataTable.Columns[i].DataType, CultureInfo.InvariantCulture);

                    if (dataTable.Columns[i].DataType == typeof(DateTime))
                    {
                        var dateTimeString = row[i].ToString();

                        //check if the string is in UTC time
                        if (dateTimeString.EndsWith("Z", false, CultureInfo.InvariantCulture))
                        {
                            rowValueWithCorrectType = Convert.ToDateTime(row[i]).ToUniversalTime();
                        }
                        else
                        {
                            rowValueWithCorrectType = Convert.ToDateTime(row[i]);
                        }
                    }
                }

                rowWithCorrectTypes.Add(rowValueWithCorrectType);
            }

            dataTable.Rows.Add(rowWithCorrectTypes.ToArray());
        }

        return dataTable;
    }

    internal static Type GetColumnType(string datatype)
    {
        if (datatype.Equals("int", StringComparison.OrdinalIgnoreCase))
        {
            datatype = "int32";
        }

        return Type.GetType($"System.{datatype}", false, true) ?? typeof(string);
    }
}
