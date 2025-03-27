using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gremlin.Net.Driver;

namespace Agent.Plugins.Mocks
{
    public class MockGraphDBPlugin : IGraphDBPlugin
    {
        public Task<string> FindAllNetworkConnectedResources(string resourceId = "")
        {
            throw new NotImplementedException();
        }

        public Task<ResultSet<dynamic>> Query(string query)
        {
            throw new NotImplementedException();
        }
    }
}
