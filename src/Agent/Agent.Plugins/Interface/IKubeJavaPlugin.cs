// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using k8s;
using k8s.Models;

namespace Agent.Plugins.Interface
{
    public interface IKubeJavaPlugin
    {

        Task<string> AnalyzeJavaApplicationAsync(string resourceId, IKubernetes client, V1Pod pod, string targetContainerName, IKubePlugin kubePlugin);
    }
}
