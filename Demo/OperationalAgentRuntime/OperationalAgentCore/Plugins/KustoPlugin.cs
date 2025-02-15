using Microsoft.SemanticKernel;
using System.ComponentModel;
using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using global::OperationalAgentCore.Helpers;

 namespace OperationalAgentCore
 {
        public class KustoPlugin
        {
            private static readonly ILogger? _logger;

            private static List<Dictionary<string, object>> ConvertDataTableToList(DataTable dataTable)
            {
                var list = new List<Dictionary<string, object>>();
                foreach (DataRow row in dataTable.Rows)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (DataColumn col in dataTable.Columns)
                    {
                        dict[col.ColumnName] = row[col];
                    }
                    list.Add(dict);
                }
                return list;
            }

            private static List<Dictionary<string, object>> ExecuteKustoQuery(string cluster, string database, string fullQuery)
            {
                try
                {
                    var kustoClient = KustoHelper.GetPublicClient(cluster, database);
                    var reader = kustoClient.ExecuteQuery(fullQuery);
                    var dataTable = new DataTable();

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        dataTable.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
                    }

                    while (reader.Read())
                    {
                        var row = dataTable.NewRow();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[i] = reader.GetValue(i);
                        }
                        dataTable.Rows.Add(row);
                    }

                    return ConvertDataTableToList(dataTable);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error while executing Kusto query: {ex.Message}");
                    throw;
                }
            }

            [KernelFunction("get_kusto_query_results")]
            [Description("Executes a fully qualified Kusto query and returns the table response")]
            public async Task<List<Dictionary<string, object>>> RunKustoQuery(string cluster, string database, string fullQuery)
            {
                return ExecuteKustoQuery(cluster, database, fullQuery);
            }
        }
}