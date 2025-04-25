// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Azure.Core;
using Azure.ResourceManager.ContainerService;
using Azure.ResourceManager.Resources;
using k8s;
using k8s.KubeConfigModels;
using YamlDotNet.Core.Tokens;

namespace Agent.Core.Services;

public class KubernetesClientFactory : IKubernetesClientFactory
{
    private readonly IArmClientFactory _armClientFactory;
    private readonly IAuthenticationService _authService;

    private readonly Dictionary<string, CachedK8SConfiguration> _configurationCache;

    public KubernetesClientFactory(IArmClientFactory armClientFactory, IAuthenticationService authService)
    {
        _armClientFactory = armClientFactory;
        _authService = authService;

        _configurationCache = new Dictionary<string, CachedK8SConfiguration>();
    }

    public async Task<IKubernetes?> CreateKubernetesClientFromResourceIdAsync(string resourceId)
    {
        var id = new ResourceIdentifier(resourceId);
        var subscription = id.SubscriptionId;
        var resourceGroup = id.ResourceGroupName;
        var clusterName = id.Name;

        if (string.IsNullOrEmpty(subscription) || string.IsNullOrEmpty(resourceGroup) || string.IsNullOrEmpty(clusterName))
        {
            return null;
        }

        if (!_configurationCache.ContainsKey($"{subscription}/{resourceGroup}/{clusterName}") ||
            _configurationCache[$"{subscription}/{resourceGroup}/{clusterName}"].IsExpired())
        {
            (var config, var expiresOn) = await GetK8SConfiguration(subscription, resourceGroup, clusterName);
            if (config == null)
            {
                return null;
            }
            _configurationCache[$"{subscription}/{resourceGroup}/{clusterName}"] = new CachedK8SConfiguration(config, expiresOn);
        }

        var k8sConfig = _configurationCache[$"{subscription}/{resourceGroup}/{clusterName}"];

        if (k8sConfig.Configuration == null)
        {
            return null;
        }

        var kubeConfig = KubernetesClientConfiguration.BuildConfigFromConfigObject(k8sConfig.Configuration);
        return new Kubernetes(kubeConfig);
    }

    private async Task<(K8SConfiguration?, DateTimeOffset?)> GetK8SConfiguration(string subscription, string resourceGroup, string clusterName)
    {
        var armClient = _armClientFactory.GetArmClient();
        var rg = armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(subscription, resourceGroup));
        var resp = await rg.GetContainerServiceManagedClusterAsync(clusterName);

        if (resp == null || !resp.Value.HasData)
        {
            return (null, null);
        }

        var cluster = resp.Value;
        if (cluster.Data.AadProfile != null && (cluster.Data.AadProfile.IsAzureRbacEnabled ?? false))
        {
            var credResp = await cluster.GetClusterUserCredentialsAsync();
            if (credResp == null)
            {
                return (null, null);
            }

            var mcCred = credResp.Value.Kubeconfigs.FirstOrDefault();
            if (mcCred == null)
            {
                return (null, null);
            }

            var kubeConfig = KubernetesClientConfiguration.LoadKubeConfig(new MemoryStream(mcCred.Value));
            var cred = _authService.GetCrawlerCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext(["6dae42f8-4368-4678-94ff-3960e28e3630/.default"]), CancellationToken.None);

            var user = kubeConfig.Users.FirstOrDefault();
            if (user != null)
            {
                user.UserCredentials ??= new UserCredentials();
                user.UserCredentials.Token = token.Token;
                user.UserCredentials.AuthProvider = null; // remove AuthProvider for existing cluster since kubectl depreciate Azure provider
                user.UserCredentials.ExternalExecution = null; //remove exec since we do not need depend on exec during execution
            }

            return (kubeConfig, token.ExpiresOn);
        }
        else
        {
            var credResp = await cluster.GetClusterAdminCredentialsAsync();
            if (credResp == null)
            {
                return (null, null);
            }

            var mcCred = credResp.Value.Kubeconfigs.FirstOrDefault();
            if (mcCred == null)
            {
                return (null, null);
            }

            var kubeConfig = KubernetesClientConfiguration.LoadKubeConfig(new MemoryStream(mcCred.Value));

            return (kubeConfig, null);
        }
    }

    public class CachedK8SConfiguration
    {
        public K8SConfiguration Configuration { get; set; }
        public DateTimeOffset? ExpiresOn { get; set; }
        public bool IsExpired() => ExpiresOn != null && DateTimeOffset.UtcNow >= ExpiresOn?.AddMinutes(-5);

        public CachedK8SConfiguration(K8SConfiguration configuration, DateTimeOffset? expiresOn)
        {
            Configuration = configuration;
            ExpiresOn = expiresOn;
        }
    }
}

