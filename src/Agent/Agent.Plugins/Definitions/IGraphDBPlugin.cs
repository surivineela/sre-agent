using Agent.Core.Models;
using Gremlin.Net.Driver;

namespace Agent.Plugins
{
    public interface IGraphDBPlugin
    {
        Task<ResultSet<dynamic>> Query(string query);
        Task<string> FindAllNetworkConnectedResources(string resourceId = "");
    }
} 