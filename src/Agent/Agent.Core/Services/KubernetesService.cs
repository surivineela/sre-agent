// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Core.Interfaces;
using k8s;
using k8s.Autorest;
using k8s.Models;

namespace Agent.Core.Services;
public abstract class KubernetesService : IKubernetesService
{
    public abstract Task<IKubernetes?> GetKubernetesClient(string resourceId);

    public async Task<V1ConfigMap?> GetConfigMapAsync(string resourceId, string ns, string name)
    {
        var client = await GetKubernetesClient(resourceId);

        try
        {
            var configMap = await client.CoreV1.ReadNamespacedConfigMapAsync(name, ns);
            return configMap;
        }
        catch (HttpOperationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<V1ConfigMapList> GetConfigMapsAsync(string resourceId, string ns, string? labelSelector = null)
    {
        var client = await GetKubernetesClient(resourceId);

        var configMaps = await client.CoreV1.ListNamespacedConfigMapAsync(ns, labelSelector: labelSelector);
        return configMaps;
    }

    public async Task<V1Deployment?> GetDeploymentAsync(string resourceId, string ns, string name)
    {
        var client = await GetKubernetesClient(resourceId);

        try
        {
            var deployment = await client.AppsV1.ReadNamespacedDeploymentAsync(name, ns);
            return deployment;
        }
        catch (HttpOperationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<V1DeploymentList> GetDeploymentsAsync(string resourceId, string ns, string? labelSelector = null)
    {
        var client = await GetKubernetesClient(resourceId);

        var deployments = await client.AppsV1.ListNamespacedDeploymentAsync(ns, labelSelector: labelSelector);
        return deployments;
    }

    public async Task<V1Namespace?> GetNamespaceAsync(string resourceId, string name)
    {
        var client = await GetKubernetesClient(resourceId);

        try
        {
            var namespaceObj = await client.CoreV1.ReadNamespaceAsync(name);
            return namespaceObj;
        }
        catch (HttpOperationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<V1NamespaceList> GetNamespacesAsync(string resourceId, string? labelSelector = null)
    {
        var client = await GetKubernetesClient(resourceId);

        var namespaces = await client.CoreV1.ListNamespaceAsync(labelSelector: labelSelector);
        return namespaces;
    }

    public async Task<V1PodList> GetPodsAsync(string resourceId, string ns, string? labelSelector = null)
    {
        var client = await GetKubernetesClient(resourceId);

        var pods = await client.CoreV1.ListNamespacedPodAsync(ns, labelSelector: labelSelector);
        return pods;
    }

    public async Task<V1Pod?> GetPodAsync(string resourceId, string ns, string name)
    {
        var client = await GetKubernetesClient(resourceId);

        try
        {
            var pod = await client.CoreV1.ReadNamespacedPodAsync(name, ns);
            return pod;
        }
        catch (HttpOperationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<V1ServiceList> GetServicesAsync(string resourceId, string ns, string? labelSelector = null)
    {
        var client = await GetKubernetesClient(resourceId);

        var services = await client.CoreV1.ListNamespacedServiceAsync(ns, labelSelector: labelSelector);
        return services;
    }

    public async Task<V1Service?> GetServiceAsync(string resourceId, string ns, string name)
    {
        var client = await GetKubernetesClient(resourceId);

        try
        {
            var service = await client.CoreV1.ReadNamespacedServiceAsync(name, ns);
            return service;
        }
        catch (HttpOperationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<V1PersistentVolumeList> GetPersistentVolumesAsync(string resourceId, string? labelSelector = null)
    {
        var client = await GetKubernetesClient(resourceId);

        var persistentVolumes = await client.CoreV1.ListPersistentVolumeAsync(labelSelector: labelSelector);
        return persistentVolumes;
    }

    public async Task<V1PersistentVolume> GetPersistentVolumeAsync(string resourceId, string ns, string name)
    {
        var client = await GetKubernetesClient(resourceId);

        try
        {
            var persistentVolume = await client.CoreV1.ReadPersistentVolumeAsync(name);
            return persistentVolume;
        }
        catch (HttpOperationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<V1PersistentVolumeClaimList> GetPersistentVolumeClaimsAsync(string resourceId, string ns, string? labelSelector = null)
    {
        var client = await GetKubernetesClient(resourceId);

        var persistentVolumeClaims = await client.CoreV1.ListNamespacedPersistentVolumeClaimAsync(ns, labelSelector: labelSelector);
        return persistentVolumeClaims;
    }

    public async Task<V1PersistentVolumeClaim> GetPersistentVolumeClaimAsync(string resourceId, string ns, string name)
    {
        var client = await GetKubernetesClient(resourceId);

        try
        {
            var persistentVolumeClaim = await client.CoreV1.ReadNamespacedPersistentVolumeClaimAsync(name, ns);
            return persistentVolumeClaim;
        }
        catch (HttpOperationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<V1SecretList> GetSecretsAsync(string resourceId, string ns, string? labelSelector = null)
    {
        var client = await GetKubernetesClient(resourceId);

        var secrets = await client.CoreV1.ListNamespacedSecretAsync(ns, labelSelector: labelSelector);
        return secrets;
    }

    public async Task<V1Secret?> GetSecretAsync(string resourceId, string ns, string name)
    {
        var client = await GetKubernetesClient(resourceId);

        try
        {
            var secret = await client.CoreV1.ReadNamespacedSecretAsync(name, ns);
            return secret;
        }
        catch (HttpOperationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}

