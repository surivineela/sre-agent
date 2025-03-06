using System.Threading.Tasks;

namespace Agent.Plugins
{
    public interface IAppIdentityUpdatePlugin
    {
        Task<string> MigrateSqlToManagedIdentityAsync(string resourceId);
        Task<string> MigrateSqlToManagedIdentityAsync(string resourceId, string sqlServer, string database);
        Task<string> EnableSqlAdAuthAsync(string resourceId, string servicePrincipalId);
    }
} 