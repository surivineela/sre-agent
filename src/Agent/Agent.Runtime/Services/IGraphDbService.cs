using Agent.Data.DatabaseClients.GraphDbClient;

namespace Agent.Runtime.Services
{
    public interface IGraphDbService
    {
        Task<List<ArmResourceNode>> GetAllResourceNodes();
    }
}
