// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using k8s.KubeConfigModels;

namespace Agent.Core.Models
{
    public class CachedK8sConfiguration
    {
        public K8SConfiguration Configuration { get; set; }
        public DateTimeOffset? ExpiresOn { get; set; }
        public bool IsExpired() => ExpiresOn != null && DateTimeOffset.UtcNow >= ExpiresOn?.AddMinutes(-5);

        public CachedK8sConfiguration(K8SConfiguration configuration, DateTimeOffset? expiresOn)
        {
            Configuration = configuration;
            ExpiresOn = expiresOn;
        }
    }
}

