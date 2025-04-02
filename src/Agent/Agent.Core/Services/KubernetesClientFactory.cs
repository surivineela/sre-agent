using System.Text;
using Agent.Core.Interfaces;
using Azure.Core;
using Azure.ResourceManager.ContainerService;
using Azure.ResourceManager.Resources;
using k8s;
using k8s.KubeConfigModels;
using Octokit;

namespace Agent.Core.Services;

public class KubernetesClientFactory : IKubernetesClientFactory
{
    private readonly IArmClientFactory _armClientFactory;
    private readonly IAuthenticationService _authService;

    private readonly Dictionary<string, K8SConfiguration> _configurationCache;

    public KubernetesClientFactory(IArmClientFactory armClientFactory, IAuthenticationService authService)
    {
        _armClientFactory = armClientFactory;
        _authService = authService;

        _configurationCache = new Dictionary<string, K8SConfiguration>();
    }

    public async Task<IKubernetes?> CreateKubernetesClientForCrawlerAsync(string resourceId)
    {
        var id = new ResourceIdentifier(resourceId);
        var subscription = id.SubscriptionId;
        var resourceGroup = id.ResourceGroupName;
        var clusterName = id.Name;

        if (string.IsNullOrEmpty(subscription) || string.IsNullOrEmpty(resourceGroup) || string.IsNullOrEmpty(clusterName))
        {
            return null;
        }

        K8SConfiguration? k8sConfig;
        if (!_configurationCache.ContainsKey($"{subscription}/{resourceGroup}/{clusterName}"))
        {
            k8sConfig = await GetK8SConfiguration(subscription, resourceGroup, clusterName);
            if (k8sConfig == null)
            {
                return null;
            }
            _configurationCache[$"{subscription}/{resourceGroup}/{clusterName}"] = k8sConfig;
        }
        else
        {
            k8sConfig = _configurationCache[$"{subscription}/{resourceGroup}/{clusterName}"];
            // TODO: check if accessToken expires
        }

        if (k8sConfig == null)
        {
            return null;
        }   

        var kubeConfig = KubernetesClientConfiguration.BuildConfigFromConfigObject(k8sConfig);
        return new Kubernetes(kubeConfig);
    }

    private async Task<K8SConfiguration?> GetK8SConfiguration(string subscription, string resourceGroup, string clusterName)
    {
        var armClient = _armClientFactory.GetArmClient();
        var rg = armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(subscription, resourceGroup));
        var resp = await rg.GetContainerServiceManagedClusterAsync(clusterName);

        if (resp == null || !resp.Value.HasData)
        {
            return null;
        }

        var cluster = resp.Value;
        if (cluster.Data.AadProfile != null && (cluster.Data.AadProfile.IsAzureRbacEnabled ?? false))
        {
            var credResp = await cluster.GetClusterUserCredentialsAsync();
            if (credResp == null)
            {
                return null;
            }

            var mcCred = credResp.Value.Kubeconfigs.FirstOrDefault();
            if (mcCred == null)
            {
                return null;
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

            return kubeConfig;
        }
        else
        {
            var credResp = await cluster.GetClusterAdminCredentialsAsync();
            if (credResp == null)
            {
                return null;
            }

            var mcCred = credResp.Value.Kubeconfigs.FirstOrDefault();
            if (mcCred == null)
            {
                return null;
            }

            return KubernetesClientConfiguration.LoadKubeConfig(new MemoryStream(mcCred.Value));
        }
    }
}
