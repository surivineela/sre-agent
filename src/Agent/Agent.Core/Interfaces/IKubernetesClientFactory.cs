using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using k8s;

namespace Agent.Core.Interfaces
{
    public interface IKubernetesClientFactory
    {
        public Task<IKubernetes?> CreateKubernetesClientForCrawlerAsync(string subscription, string resourceGroup, string clusterName);
    }
}
