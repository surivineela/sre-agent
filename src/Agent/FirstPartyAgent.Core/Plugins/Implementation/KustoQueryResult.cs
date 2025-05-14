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
        public string Result { get; set; } = string.Empty;

        public ChatMessage? Message;

        public KustoQueryResult()
        {
        }

        public KustoQueryResult(IDataReader reader, string query)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (reader.FieldCount == 0) return;

            var sb = new StringBuilder();

            bool firstRow = true;
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
            for (int i = 0; i < reader.FieldCount; i++)
            {
                yield return reader[i]?.ToString() ?? string.Empty;
            }
        }
    }
}
