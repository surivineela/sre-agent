using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Data;

namespace FirstPartyAgent.Plugins
{
    public class GenericKustoPlugin
    {
        public static Kusto.Data.Common.ICslQueryProvider GetPublicClient(string cluster, string database)
        {
            string endpoint = "https://" + cluster + ".kusto.windows.net/";
            var cs = new Kusto.Data.KustoConnectionStringBuilder(endpoint, database).WithAadUserPromptAuthentication();

            return Kusto.Data.Net.Client.KustoClientFactory.CreateCslQueryProvider(cs);
        }

        public static Kusto.Data.Common.ICslAdminProvider GetPublicClientControl(string cluster, string database)
        {
            string endpoint = "https://" + cluster + ".kusto.windows.net/";
            var cs = new Kusto.Data.KustoConnectionStringBuilder(endpoint, database).WithAadUserPromptAuthentication();

            return Kusto.Data.Net.Client.KustoClientFactory.CreateCslDmAdminProvider(cs);
        }

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
                var kustoClient = GetPublicClient(cluster, database);
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
