// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using k8s;

namespace Agent.Core.Interfaces
{
    public interface IKubernetesClientFactory
    {
        /// <summary>
        /// Creates kube client for crawling purpose
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public Task<IKubernetes?> CreateKubernetesClientFromResourceIdForCrawlerAsync(string resourceId);

        /// <summary>
        /// Creates kube client for action purpose
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public Task<IKubernetes?> CreateKubernetesClientFromResourceIdAsync(string resourceId);

        public Task<CachedK8sConfiguration?> GetOrAddCachedK8sConfiguration(string resourceId);
    }
}

