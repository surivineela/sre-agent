using System.Threading.Tasks;
using Agent.Core.Models;

namespace Agent.Plugins
{
    public interface IMIConfigurationCheckPlugin
    {
        Task<SqlConnectionDescriptor> CheckSqlConnectionTypeAsync(string resourceId);
        Task<string> CheckSqlResourceIdForAppAsync(string resourceId);
    }
}
