// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Azure.Core;
using Azure.ResourceManager;

namespace Agent.Graph.Extensions;

internal static class ArmClientExtensions
{
    public static async Task<ArmResourceNode?> FindGenericArmResource(this ArmClient armClient, string subscriptionId, string resourceType, string? resourceName)
    {
        if (string.IsNullOrEmpty(resourceName))
        {
            return null;
        }

        var subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
        await foreach (var resource in subscription.GetGenericResourcesAsync(filter: $"resourceType eq '{resourceType}'"))
        {
            if (!resource.Data.Name.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(resourceType, "Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase))
            {
                var rawKind = resource.Data.Kind;
                var normalizedKind = ResourceKindHelper.getResourceKind(resourceType, rawKind);

                var appServiceNode = new AppServiceNode(
                    resourceType: resourceType,
                    resourceId: resource.Data.Id.ToString(),
                    subscriptionId: subscriptionId,
                    resourceGroupName: resource.Data.Id.ResourceGroupName!,
                    resourceName: resource.Data.Name,
                    resourceKind: normalizedKind,
                    location: resource.Data.Location);

                appServiceNode.Kind = rawKind;

                return appServiceNode;
            }
            else
            {
                return new ArmResourceNode(
                    resourceType: resourceType,
                    resourceId: resource.Data.Id.ToString(),
                    subscriptionId: subscriptionId,
                    resourceGroupName: resource.Data.Id.ResourceGroupName!,
                    resourceName: resource.Data.Name,
                    location: resource.Data.Location);
            }
        }

        return null;
    }
}
