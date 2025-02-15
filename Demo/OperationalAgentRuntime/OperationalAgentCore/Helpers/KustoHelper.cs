using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Kusto.Data;

namespace OperationalAgentCore.Helpers
{
    public static class KustoHelper
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
    }
}