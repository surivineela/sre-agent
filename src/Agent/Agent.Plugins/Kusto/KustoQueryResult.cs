// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Data;
using System.Text;
using Microsoft.Extensions.AI;

namespace Agent.Plugins.Kusto;

public class KustoQueryResult
{
    public int RowCount { get; set; }

    public string Query { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public bool Success { get; set; } = true;

    public ChatMessage? Message { get; set; }

    public KustoQueryResult()
    {
    }

    public KustoQueryResult(IDataReader reader, string query)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        if (reader.FieldCount == 0)
        {
            return;
        }

        var sb = new StringBuilder();

        // Append column names as the first row
        var columnNames = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columnNames.Add(reader.GetName(i));
        }

        sb.AppendLine(string.Join('\t', columnNames));

        var firstRow = true;
        while (reader.Read())
        {
            if (!firstRow)
            {
                sb.AppendLine();
            }

            firstRow = false;

            sb.Append(string.Join('\t', EnumerateFields(reader)));
            RowCount++;
        }

        Result = sb.ToString();
        Query = query;
    }

    public KustoQueryResult(int rowCount, string query, string result, ChatMessage? message)
    {
        RowCount = rowCount;
        Query = query;
        Result = result;
        Message = message;
    }

    private IEnumerable<string> EnumerateFields(IDataReader reader)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            yield return reader[i]?.ToString() ?? string.Empty;
        }
    }
}
