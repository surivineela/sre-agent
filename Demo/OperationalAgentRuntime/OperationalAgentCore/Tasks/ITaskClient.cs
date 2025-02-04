using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentCore
{
    public interface ITaskClient
    {
        Task<List<RemediationTask>> GetPendingRemediationsAsync();
        Task ScheduleRemediationAsync(RemediationTask task);
        Task DeleteRemediationAsync(string id);
        Task UpdateRemediationAsync(RemediationTask task);
    }
}
