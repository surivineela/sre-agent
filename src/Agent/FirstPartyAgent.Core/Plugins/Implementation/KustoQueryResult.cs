// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Data;
using System.Text;
using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Plugins
{
    public class KustoQueryResult
    {
        public int RowCount;
        public string Query = string.Empty;
        public string Result = string.Empty;
        public ChatMessage? Message;

        public static KustoQueryResult Error = new KustoQueryResult(0, "", "An error occurred while executing query.", null);

        public KustoQueryResult(IDataReader reader, string query)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (reader.FieldCount == 0) return;

            var sb = new StringBuilder();
            while (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    sb.Append(reader[i]?.ToString() ?? string.Empty);
                    sb.Append("\t");
                }
                RowCount++;
                sb.AppendLine();
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
    }
}
