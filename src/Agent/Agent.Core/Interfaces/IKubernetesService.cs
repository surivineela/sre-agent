// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using k8s.Models;

namespace Agent.Core.Interfaces;
public interface IKubernetesService
{
    public Task<V1NamespaceList> GetNamespacesAsync(string resourceId, string? labelSelector = null);
    public Task<V1Namespace?> GetNamespaceAsync(string resourceId, string name);
    public Task<V1PodList> GetPodsAsync(string resourceId, string ns, string? labelSelector = null);
    public Task<V1Pod?> GetPodAsync(string resourceId, string ns, string name);
    public Task<V1ServiceList> GetServicesAsync(string resourceId, string ns, string? labelSelector = null);
    public Task<V1Service?> GetServiceAsync(string resourceId, string ns, string name);
    public Task<V1DeploymentList> GetDeploymentsAsync(string resourceId, string ns, string? labelSelector = null);
    public Task<V1Deployment?> GetDeploymentAsync(string resourceId, string ns, string name);
    public Task<V1PersistentVolumeList> GetPersistentVolumesAsync(string resourceId, string? labelSelector = null);
    public Task<V1PersistentVolume> GetPersistentVolumeAsync(string resourceId, string ns, string name);

    public Task<V1PersistentVolumeClaimList> GetPersistentVolumeClaimsAsync(string resourceId, string ns, string? labelSelector = null);
    public Task<V1PersistentVolumeClaim> GetPersistentVolumeClaimAsync(string resourceId, string ns, string name);
    public Task<V1ConfigMapList> GetConfigMapsAsync(string resourceId, string ns, string? labelSelector = null);
    public Task<V1ConfigMap?> GetConfigMapAsync(string resourceId, string ns, string name);
    public Task<V1SecretList> GetSecretsAsync(string resourceId, string ns, string? labelSelector = null);
    public Task<V1Secret?> GetSecretAsync(string resourceId, string ns, string name);
}

