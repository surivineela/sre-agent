using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Storage;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class StorageAccountConnectionStringHelper
{
    private readonly ILogger _logger;
    private readonly ArmClient _armClient;

    public StorageAccountConnectionStringHelper(ILogger logger, ArmClient armClient)
    {
        _logger = logger;
        _armClient = armClient;
    }

    public async Task<ArmResourceNode?> GetStorageAccountResourceFromSettingAsync(string subscriptionId, string value)
    {
        // Try to parse as connection string first
        var accountName = ParseAccountName(value);
        if (!string.IsNullOrEmpty(accountName))
        {
            var subscription = _armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            await foreach (var storageAccount in subscription.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Storage/storageAccounts'"))
            {
                if (storageAccount.Data.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase))
                {
                    var storageResourceId = storageAccount.Data.Id.ToString();
                    var storageNode = new ArmResourceNode(
                        resourceType: "Microsoft.Storage/storageAccounts",
                        resourceId: storageResourceId,
                        subscriptionId: subscriptionId,
                        resourceGroupName: ArmHelper.ExtractResourceGroupNameFromId(storageAccount.Data.Id!)!,
                        resourceName: storageAccount.Data.Name,
                        location: storageAccount.Data.Location);

                    _logger.LogDebug($"Found Storage Account {storageResourceId}");
                    return storageNode;
                }
            }

            _logger.LogInternalWarning($"Storage account with name {accountName} was not found in the subscription {subscriptionId}.");
            return null;
        }

        // If not a connection string, treat as access key (cannot resolve account)
        _logger.LogInternalWarning($"Storage account appears to be configured with access key, not a connection string. Unable to resolve storage account");
        return null;
    }

    private string? ParseAccountName(string value)
    {
        // Typical connection string: AccountName=xxxx;
        var match = Regex.Match(value, @"AccountName=([^;]+)", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value;
        return null;
    }
}
