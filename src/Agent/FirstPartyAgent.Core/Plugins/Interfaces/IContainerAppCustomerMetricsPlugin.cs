using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Plugins.Interfaces
{
    public interface IContainerAppCustomerMetricsPlugin
    {
        Task<string> GetMetricsMdmCount(string region, DateTime fromDate, DateTime toDate, string metricName, string containerAppArmId);

        Task<string> GetMdmPodHeartbeatMissedTimes(string region, DateTime fromDate, DateTime toDate, string managedClusterName);

        Task<string> GetMissedMdmMetricTimes(string region, DateTime fromDate, DateTime toDate, string metricName, string containerAppArmId);

        Task<string> GetBillingPodLeaderElection(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    }
}
