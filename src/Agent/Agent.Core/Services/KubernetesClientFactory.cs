// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Logging;
using Azure.Core;
using Azure.ResourceManager.ContainerService;
using Azure.ResourceManager.ContainerService.Models;
using Azure.ResourceManager.Resources;
using k8s;
using k8s.KubeConfigModels;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

public class KubernetesClientFactory : IKubernetesClientFactory
{
    private readonly ILogger<KubernetesClientFactory> _logger;
    private readonly IArmClientFactory _armClientFactory;
    private readonly IAuthenticationService _authService;
    private readonly ActionSettings _actionSettings;

    private readonly Dictionary<string, CachedK8sConfiguration> _configurationCache;

    public KubernetesClientFactory(
        ILogger<KubernetesClientFactory> logger,
        IArmClientFactory armClientFactory,
        IAuthenticationService authService,
        ActionSettings actionSettings)
    {
        _logger = logger;
        _armClientFactory = armClientFactory;
        _authService = authService;
        _actionSettings = actionSettings;

        _configurationCache = new Dictionary<string, CachedK8sConfiguration>();
    }

    public async Task<IKubernetes?> CreateKubernetesClientFromResourceIdForCrawlerAsync(string resourceId)
    {
        var id = new ResourceIdentifier(resourceId);
        var subscription = id.SubscriptionId;
        var resourceGroup = id.ResourceGroupName;
        var clusterName = id.Name;

        if (string.IsNullOrEmpty(subscription) || string.IsNullOrEmpty(resourceGroup) || string.IsNullOrEmpty(clusterName))
        {
            throw new ArgumentException($"Invalid resource Id: {resourceId}");
        }

        return await GetK8sClient(subscription, resourceGroup, clusterName, true);
    }

    public async Task<IKubernetes?> CreateKubernetesClientFromResourceIdAsync(string resourceId)
    {
        var id = new ResourceIdentifier(resourceId);
        var subscription = id.SubscriptionId;
        var resourceGroup = id.ResourceGroupName;
        var clusterName = id.Name;

        if (string.IsNullOrEmpty(subscription) || string.IsNullOrEmpty(resourceGroup) || string.IsNullOrEmpty(clusterName))
        {
            throw new ArgumentException($"Invalid resource Id: {resourceId}");
        }

        return await GetK8sClient(subscription, resourceGroup, clusterName, false);
    }

    public async Task<CachedK8sConfiguration?> GetOrAddCachedK8sConfiguration(string resourceId)
    {
        var id = new ResourceIdentifier(resourceId);
        var subscription = id.SubscriptionId;
        var resourceGroup = id.ResourceGroupName;
        var clusterName = id.Name;

        if (string.IsNullOrEmpty(subscription) || string.IsNullOrEmpty(resourceGroup) || string.IsNullOrEmpty(clusterName))
        {
            return null;
        }

        return await GetOrAddCachedK8sConfigurationInternal(subscription, resourceGroup, clusterName, false);
    }

    private async Task<IKubernetes?> GetK8sClient(string subscription, string resourceGroup, string clusterName, bool isCrawler)
    {
        var k8sConfig = await GetOrAddCachedK8sConfigurationInternal(subscription, resourceGroup, clusterName, isCrawler);

        if (k8sConfig == null)
        {
            return null;
        }

        var kubeConfig = KubernetesClientConfiguration.BuildConfigFromConfigObject(k8sConfig.Configuration);

        return new Kubernetes(kubeConfig);
    }

    private async Task<(K8SConfiguration, DateTimeOffset?)> GetK8sConfigurationFromArm(string subscription, string resourceGroup, string clusterName, TokenCredential cred, string? agentMode = null)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var rg = armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(subscription, resourceGroup));
        var resp = await rg.GetContainerServiceManagedClusterAsync(clusterName);

        if (resp == null || !resp.Value.HasData)
        {
            var msg = $"Failed to get cluster resource from ARM for {subscription}/{resourceGroup}/{clusterName}";
            _logger.LogInternalError(msg);
            throw new InvalidOperationException(msg);
        }

        var cluster = resp.Value;
        var isReadOnlyMode = string.Equals(agentMode, "ReadOnly", StringComparison.OrdinalIgnoreCase);
        if (cluster.Data.AadProfile != null)
        {
            _logger.LogInternalInformation($"User credential will be used for {subscription}/{resourceGroup}/{clusterName}");
            var credResp = await cluster.GetClusterUserCredentialsAsync();
            if (credResp == null)
            {
                var msg = $"Failed to list user credential for {subscription}/{resourceGroup}/{clusterName}";
                _logger.LogInternalError(msg);
                throw new InvalidOperationException(msg);
            }

            var mcCred = credResp.Value.Kubeconfigs.FirstOrDefault();
            if (mcCred == null)
            {
                var msg = $"Empty kube config for {subscription}/{resourceGroup}/{clusterName}";
                _logger.LogInternalError(msg);
                throw new InvalidOperationException(msg);
            }

            var kubeConfig = KubernetesClientConfiguration.LoadKubeConfig(new MemoryStream(mcCred.Value));
            var token = await cred.GetTokenAsync(new TokenRequestContext(["6dae42f8-4368-4678-94ff-3960e28e3630/.default"]), CancellationToken.None);

            var user = kubeConfig.Users.FirstOrDefault();
            if (user != null)
            {
                user.UserCredentials ??= new UserCredentials();
                user.UserCredentials.Token = token.Token;
                user.UserCredentials.AuthProvider = null; // remove AuthProvider for existing cluster since kubectl depreciate Azure provider
                user.UserCredentials.ExternalExecution = null; //remove exec since we do not need depend on exec during execution
                user.UserCredentials.Extensions = [
                    new NamedExtension{
                    Name = "UseAADAuth",
                    Extension = true,
                }
                ]; // for kubectl cli execution to know whether to use the obo token
            }

            return (kubeConfig, token.ExpiresOn);
        }
        else
        {
            Azure.Response<ManagedClusterCredentials> credResp;
            var credentialType = isReadOnlyMode ? "user" : "admin";

            if (isReadOnlyMode)
            {
                _logger.LogInternalInformation($"ReadOnly mode is enabled, user credential will be used for {subscription}/{resourceGroup}/{clusterName}");
                credResp = await cluster.GetClusterUserCredentialsAsync();
            }
            else
            {
                try
                {
                    _logger.LogInternalInformation($"Admin credential will be used for {subscription}/{resourceGroup}/{clusterName}");
                    credResp = await cluster.GetClusterAdminCredentialsAsync();
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 403)
                {
                    _logger.LogInternalInformation($"Does not have sufficient permissions to list admin credential, falling back to user credential for {subscription}/{resourceGroup}/{clusterName}");
                    credResp = await cluster.GetClusterUserCredentialsAsync();
                }
            }
            if (credResp?.Value?.Kubeconfigs == null || !credResp.Value.Kubeconfigs.Any())
            {
                var msg = $"Failed to retrieve {credentialType} credentials or empty kubeconfig list for {subscription}/{resourceGroup}/{clusterName}";
                _logger.LogInternalError(msg);
                throw new InvalidOperationException(msg);
            }

            var mcCred = credResp.Value.Kubeconfigs.FirstOrDefault();
            if (mcCred?.Value == null || mcCred.Value.Length == 0)
            {
                var msg = $"Empty kube config for {subscription}/{resourceGroup}/{clusterName}";
                _logger.LogInternalError(msg);
                throw new InvalidOperationException(msg);
            }

            var kubeConfig = KubernetesClientConfiguration.LoadKubeConfig(new MemoryStream(mcCred.Value));

            return (kubeConfig, null);
        }
    }

    private async Task<CachedK8sConfiguration?> GetOrAddCachedK8sConfigurationInternal(string subscription, string resourceGroup, string clusterName, bool isCrawler)
    {
        _logger.LogInternalInformation($"Getting k8s client for {subscription}/{resourceGroup}/{clusterName}. IsCrawler = {isCrawler}");
        var key = $"{subscription}/{resourceGroup}/{clusterName}{(isCrawler ? "/crawler" : "")}";
        if (isCrawler)
        {
            if (!_configurationCache.ContainsKey(key) ||
                _configurationCache[key].IsExpired())
            {
                var cred = _authService.GetCrawlerCredential();
                var (config, expiresOn) = await GetK8sConfigurationFromArm(subscription, resourceGroup, clusterName, cred, _actionSettings.Mode.ToString());
                _configurationCache[key] = new CachedK8sConfiguration(config, expiresOn);
            }
        }
        else
        {
            var cred = await _authService.GetKubernetesOperationCredential();
            if (!_configurationCache.ContainsKey(key))
            {
                var (config, expiresOn) = await GetK8sConfigurationFromArm(subscription, resourceGroup, clusterName, cred, _actionSettings.Mode.ToString());
                _configurationCache[key] = new CachedK8sConfiguration(config, expiresOn);
            }
            else
            {
                var cached = _configurationCache[key];
                // do not override token if admin credential is used (only the case AAD is not enabled on cluster)
                if (cached.ExpiresOn != null)
                {
                    // always the refresh token as the cred might change according to different context
                    var token = await cred.GetTokenAsync(new TokenRequestContext(["6dae42f8-4368-4678-94ff-3960e28e3630/.default"]), CancellationToken.None);
                    cached.Configuration.Users.First().UserCredentials.Token = token.Token;
                    _configurationCache[key] = cached;
                }
            }
        }

        return _configurationCache[key];
    }
}

