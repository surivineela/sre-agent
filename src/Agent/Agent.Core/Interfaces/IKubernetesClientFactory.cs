// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using k8s;

namespace Agent.Core.Interfaces
{
    public interface IKubernetesClientFactory
    {
        public Task<IKubernetes?> CreateKubernetesClientFromResourceIdAsync(string resourceId);
    }
}

