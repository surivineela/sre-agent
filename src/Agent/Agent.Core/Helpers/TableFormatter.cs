// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Newtonsoft.Json;

namespace Agent.Core.Helpers;

public static class TableFormatter
{
    public static string DataTableResponseStreamToTsv(Stream stream)
    {
        using var streamReader = new StreamReader(stream);
        using var jsonReader = new JsonTextReader(streamReader);
        var result = JsonSerializer.CreateDefault().Deserialize<DataTableResponseObjectCollection>(jsonReader);
        return DataTableResponseToTsv(result);
    }

    public static string DataTableResponseToTsv(DataTableResponseObjectCollection? result)
    {
        var sb = new StringBuilder();

        if (result is not null)
        {
            foreach (var table in result.Tables)
            {
                if (table.Columns.Length == 0 || table.Rows.Length == 0)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine($"Result table name: {table.Name}");
                sb.AppendLine($"Column count: {table.Columns.Length}");
                sb.AppendLine($"Row count: {table.Rows.Length}");

                for (var i = 0; i < table.Columns.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append('\t');
                    }

                    sb.Append($"{table.Columns[i].Name} ({table.Columns[i].Type})");
                }

                sb.AppendLine();

                foreach (var row in table.Rows)
                {
                    for (var i = 0; i < row.Length; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append('\t');
                        }

                        var value = row[i];

                        if (value is not null)
                        {
                            sb.Append(value);
                        }
                    }

                    sb.AppendLine();
                }
            }
        }

        if (sb.Length == 0)
        {
            return "ZERO_ROWS_RETURNED";
        }

        return sb.ToString();
    }
}
