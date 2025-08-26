// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;
using Agent.Logging;
using Azure.Core;

namespace Agent.Core.Interfaces;

public interface IAuthenticationService
{
    /// <summary>
    /// Get the credential to access the document db
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetDocumentDbCredential();

    /// <summary>
    /// Get the credential to access the dts
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetDtsCredential();

    /// <summary>
    /// Get the credential to access the search endpoint through workload identity
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetSearchEndpointCredential();

    /// <summary>
    /// Get the credential to access the search plugin
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetSearchPluginCredential();

    /// <summary>
    /// Get the credential to access the session pool
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetSessionPoolCredential();

    /// <summary>
    /// Get the credential to crawl resources of user tenant
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetCrawlerCredential();

    /// <summary>
    /// Get the credential to operate on ARM resources
    /// </summary>
    /// <returns></returns>
    public Task<TokenCredential> GetArmOperationCredential();

    /// <summary>
    /// Get the credential to access the azure monitor workspace
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetAzureMonitorWorkspaceCredential();

    /// <summary>
    /// Get the bearer token to access the grafana api.
    /// Could be admin api key or managed identity token.
    /// </summary>
    /// <returns></returns>
    public Task<string> GetGrafanaAccessToken();

    /// <summary>
    /// Get the credential to access the azure open ai service
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetAzureOpenAICredential();

    /// <summary>
    /// Get the credential to access Application Insights
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetAppInsightsCredential();

    /// <summary>
    /// Get the credential to access Log Analytics workspace
    /// </summary>
    /// <returns></returns>
    ///

    /// <summary>
    /// Gets credentials for accessing Azure Storage resources, such as Blob Storage or File Storage.
    /// </summary>
    public TokenCredential GetStorageCredential();

    public TokenCredential GetLogAnalyticsCredential();

    public Task<TokenCredential> GetKubernetesOperationCredential();

    public string? GetActionIdentity();

    public TokenCredential GetAgentMemoryBlobStorageCredential();
    public TokenCredential GetAgentMemoryAzureAISearchCredential();
    public TokenCredential GetObserverCredential();
    public TokenCredential GetAgentHelperCredential();

    public TokenCredential GetAzureDevOpsCredential();
    public Task<string> GetGitHubAccessToken();

    /// <summary>
    /// Get the credential to access Applens runtime host
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetApplensCredential();

    /// <summary>
    /// Get the scope for Applens based on the current tenant
    /// </summary>
    /// <returns></returns>
    public string GetApplensScope();

    /// <summary>
    /// Get the runtime host URL for Applens based on the environment
    /// </summary>
    /// <returns></returns>
    public string GetApplensRuntimeHostUrl();

    public TokenCredential Get1PAgentKeyVaultCredential(string managedIdentityId);

    public TokenCredential GetIcmApiCredential();

    public TokenCredential GetDataConnectorCredential(ConnectorAuthSettings connectorAuthSettings);

    public TokenCredential GetAzureSearchCredential();

    public TokenCredential GetEventHubTraceExportCredential(EventHubTraceExporterOptions options);
}
