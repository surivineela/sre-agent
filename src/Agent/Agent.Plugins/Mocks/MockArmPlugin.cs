// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Kusto.Cloud.Platform.Utils;

namespace Agent.Plugins.Mocks;

public class MockArmPlugin : IArmPlugin
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, TlsStatus> _tlsStatuses = new();
    private readonly Dictionary<string, AppReliability> _reliabilityStatuses = new();

    public Guid? ThreadId { get; set; }

    public MockArmPlugin(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void ConfigureTlsStatus(
        IReadOnlyDictionary<string, TlsStatus> tlsStatuses)
    {
        _tlsStatuses.Clear();
        _tlsStatuses.AddOrSetRange(tlsStatuses);
    }

    public string GetTlsStatus(string appResourceId)
    {
        if (!_tlsStatuses.ContainsKey(appResourceId))
        {
            throw new ArgumentException($"Resource {appResourceId} not found");
        }
        var status = _tlsStatuses[appResourceId];
        return status.MinimumTlsVersion ?? string.Empty;
    }

    public Task<string> SetMinimumTlsVersion(string appResourceId, string minimumTlsVersion)
    {
        if (!_tlsStatuses.ContainsKey(appResourceId))
        {
            throw new ArgumentException($"Resource {appResourceId} not found");
        }

        _tlsStatuses[appResourceId] = _tlsStatuses[appResourceId] with
        {
            MinimumTlsVersion = minimumTlsVersion
        };
        var msg = $"Resource {appResourceId} updated with minimum TLS version set to {minimumTlsVersion} at {_timeProvider.GetUtcNow():o}";
        return Task.FromResult(msg);
    }

    public Task<bool> RestartWebApp(string appResourceId)
    {
        return Task.FromResult(true);
    }

    public Task<bool> StartWebApp(string appResourceId)
    {
        return Task.FromResult(true);
    }

    public void ConfigureReliability(
        IReadOnlyDictionary<string, AppReliability> statuses)
    {
        _reliabilityStatuses.Clear();
        _reliabilityStatuses.AddOrSetRange(statuses);
    }

    public Tuple<bool, bool, bool, int> GetAppReliability(string appResourceId)
    {
        if (!_tlsStatuses.ContainsKey(appResourceId))
        {
            throw new ArgumentException($"Resource {appResourceId} not found");
        }
        var ar = _reliabilityStatuses[appResourceId];
        var status = new Tuple<bool, bool, bool, int>(ar.AlwaysOnEnabled, ar.HealthCheckEnabled, ar.AutoHealEnabled, ar.NumberOfWorkers);
        return status;
    }

    public Task<List<TlsStatus>> GetTlsSettings(List<string> resourceIds)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CheckIfResourceExists(string appResourceId)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetArmResourceAsJson(string resourceId)
    {
        throw new NotImplementedException();
    }

    public Task<RemediationResult> PowerOnVirtualMachine(string resourceId)
    {
        return Task.FromResult(new RemediationResult(
                Success: true,
                Action: "Power On Azure Virtual Machine",
                Details: "Virtual machine powered on successfully",
                OperationId: null,
                FinishedTime: DateTime.Now));
    }

    public Task<IReadOnlyDictionary<string, string>> GetVirtualMachineBootDiagnostics(string resourceId)
    {
        return Task.FromResult((IReadOnlyDictionary<string, string>)new Dictionary<string, string>());
    }

    public Task<string> CheckConnectivityToAzureWebJobsStorage(string resourceId, string providerType)
    {
        return Task.FromResult<string>("true");
    }

    public Task<string> CheckTcpConnectivity(string resourceId, string host, int port)
    {
        return Task.FromResult<string>("true");
    }

    public Task<string> CheckDnsResolution(string resourceId, string destinationUrl)
    {
        return Task.FromResult<string>("true");
    }

    public Task<List<string>> GetDeploymentSlotsResourceIdsAsync(string resourceId)
    {
        return Task.FromResult(new List<string>());
    }

    public Task<IDictionary<string, string>> GetAppSetting(string resourceId, string appSettingKey)
    {
        return Task.FromResult((IDictionary<string, string>)new Dictionary<string, string>());
    }

    public Task<bool> ListKeysAndUpdateAppSettingsAsync(string storageResourceId, string appServiceResourceId, string appSettingKey)
    {
        return Task.FromResult<bool>(true);
    }

    public Task<bool> ConfigureAppSettingsForManagedIdentityStorage(string resourceId, string storageAccountName, bool useUserAssignedManagedIdentity = false, string userManagedIdentityClientId = "")
    {
        return Task.FromResult(true);
    }

    public Task<bool> UpdateAppSettingsAsync(string resourceId, IDictionary<string, string> appSettings)
    {
        return Task.FromResult(true);
    }

    public Task<CliToolExecutionResult> RunAzCliReadCommandsAsync(string command)
    {
        return Task.FromResult(new CliToolExecutionResult(new CliExecutionResult { Output = "Command executed", ErrorType = CliErrorType.None }, null));
    }

    public Task<CliToolExecutionResult> RunAzCliWriteCommandsAsync(string command)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetAzCliHelpAsync(string helpTopic, string grepPattern = "")
    {
        // Mock implementation for testing
        var mockHelp = $"Mock help for Azure CLI topic: {helpTopic}";
        if (!string.IsNullOrEmpty(grepPattern))
        {
            mockHelp += $"\nFiltered by pattern: {grepPattern}";
        }
        return Task.FromResult(mockHelp);
    }

    public Task<string> GetResourceIdFromStorageServiceUri(string storageServiceUri, string subscriptionId)
    {
        if (string.IsNullOrWhiteSpace(storageServiceUri))
        {
            return Task.FromResult($"Error: Storage Service URI cannot be null or empty");
        }

        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return Task.FromResult($"Error: Subscription ID cannot be null or empty");
        }

        try
        {
            // For mock implementation, create a consistent resource ID based on the URI
            if (!Uri.TryCreate(storageServiceUri, UriKind.Absolute, out var uri))
            {
                return Task.FromResult($"Error: Invalid storage service URI format: {storageServiceUri}");
            }

            // Extract storage account name from URI host
            var host = uri.Host;
            var hostParts = host.Split('.');

            if (hostParts.Length < 4 || !host.Contains(".blob.core.windows.net"))
            {
                return Task.FromResult($"Error: URI does not appear to be a valid Azure Blob Storage URI: {storageServiceUri}");
            }

            var storageAccountName = hostParts[0];

            // Generate a mock resource ID for the storage account using the provided subscription ID
            return Task.FromResult($"/subscriptions/{subscriptionId}/resourceGroups/mock-resource-group/providers/Microsoft.Storage/storageAccounts/{storageAccountName}");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: Exception occurred while processing storage service URI: {ex.Message}");
        }
    }

    public Task<(bool, string)> EnableTrafficManagerEndpoint(string subscriptionId, string resourceGroupName, string profileName, string endpointName, string endpointType)
    {
        return Task.FromResult((true, $"Mock: Traffic Manager endpoint {endpointName} enabled successfully"));
    }

    public Task<(bool, string)> DisableTrafficManagerEndpoint(string subscriptionId, string resourceGroupName, string profileName, string endpointName, string endpointType)
    {
        return Task.FromResult((true, $"Mock: Traffic Manager endpoint {endpointName} disabled successfully"));
    }

    public Task<string> GetAllTrafficManagerEndpointsStatus(string subscriptionId, string resourceGroupName, string profileName)
    {
        return Task.FromResult("Mock: All Traffic Manager endpoints are healthy");
    }

    public Task<(bool, string)> EnableAzureFrontDoorEndpointOrigin(string subscriptionId, string resourceGroupName, string frontDoorProfileName, string endpointNameOrHostName, string originName)
    {
        return Task.FromResult((true, $"Mock: Azure Front Door endpoint origin {originName} enabled successfully for endpoint {endpointNameOrHostName}"));
    }

    public Task<(bool, string)> DisableAzureFrontDoorEndpointOrigin(string subscriptionId, string resourceGroupName, string frontDoorProfileName, string endpointNameOrHostName, string originName)
    {
        return Task.FromResult((true, $"Mock: Azure Front Door endpoint origin {originName} disabled successfully for endpoint {endpointNameOrHostName}"));
    }

    public Task<string> GetAllAzureFrontDoorEndpointOriginsStatus(string subscriptionId, string resourceGroupName, string frontDoorProfileName)
    {
        return Task.FromResult("Mock: All Azure Front Door endpoint origins are healthy");
    }

    public Task<(bool, string)> RunAzureDataFactoryPipeline(string subscriptionId, string resourceGroupName, string dataFactoryName, string pipelineName)
    {
        return Task.FromResult((true, $"Mock: Azure Data Factory pipeline {pipelineName} started successfully"));
    }

    public Task<(bool, string)> StopAzureDataFactoryPipeline(string subscriptionId, string resourceGroupName, string dataFactoryName, string pipelineName)
    {
        return Task.FromResult((true, $"Mock: Azure Data Factory pipeline {pipelineName} stopped successfully"));
    }

    public Task<(bool, string)> RestartAzureDataFactoryPipeline(string subscriptionId, string resourceGroupName, string dataFactoryName, string pipelineName)
    {
        return Task.FromResult((true, $"Mock: Azure Data Factory pipeline {pipelineName} restarted successfully"));
    }

    public Task<string> GetAllAzureDataFactoryPipelinesStatus(string subscriptionId, string resourceGroupName, string dataFactoryName)
    {
        return Task.FromResult("Mock: All Azure Data Factory pipelines are running successfully");
    }

    Task<string> IArmPlugin.GetVirtualMachineBootStateAsJson(string resourceId)
    {
        throw new NotImplementedException();
    }
}
