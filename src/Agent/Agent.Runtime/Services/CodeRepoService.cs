// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Runtime.Interfaces;
using Azure.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

/// <summary>
/// Service for code repository operations.
/// </summary>
public class CodeRepoService : ICodeRepoService
{
    private readonly ILogger<CodeRepoService> _logger;
    private readonly ICodeRepoRepository _repository;
    private readonly IConnectorResolver _connectorResolver;
    private readonly IOAuthTokenService _oauthTokenService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IHostEnvironment _hostEnvironment;

    public CodeRepoService(
        ILogger<CodeRepoService> logger,
        ICodeRepoRepository repository,
        IConnectorResolver connectorResolver,
        IOAuthTokenService oauthTokenService,
        IAuthenticationService authenticationService,
        IHostEnvironment hostEnvironment)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _connectorResolver = connectorResolver ?? throw new ArgumentNullException(nameof(connectorResolver));
        _oauthTokenService = oauthTokenService ?? throw new ArgumentNullException(nameof(oauthTokenService));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
    }

    public async Task<CodeRepoDocumentModel> CreateOrUpdateCodeRepoAsync(
        string repoName, CodeRepoDocumentModel model)
    {
        var operationId = Guid.NewGuid().ToString();
        _logger.LogInternalInformation("Creating or updating code repository: {RepoName}, OperationId: {OperationId}",
            repoName, operationId);

        var repo = await _repository.UpsertCodeRepoAsync(model, operationId);

        return repo;
    }

    public async Task<CodeRepoDocumentModel?> GetCodeRepoAsync(string repoName)
    {
        _logger.LogInternalInformation("Retrieving code repository: {RepoName}", repoName);
        return await _repository.GetCodeRepoByNameAsync(repoName);
    }

    public async Task<CodeRepoDocumentModel[]> GetCodeReposAsync()
    {
        _logger.LogInternalInformation("Getting all code repositories");
        var repos = await _repository.GetAllCodeReposAsync();
        return repos.ToArray();
    }

    public async Task<CodeRepoDocumentModel?> DeleteCodeRepoAsync(string repoName)
    {
        _logger.LogInternalInformation("Deleting code repository: {RepoName}", repoName);

        var repo = await _repository.GetCodeRepoByNameAsync(repoName);
        if (repo == null)
        {
            return null;
        }

        await _repository.DeleteCodeRepoAsync(repoName);

        return repo;
    }

    public async Task<string?> GetCodeRepoTokenAsync(string repoUrl, RepoType repoType)
    {
        _logger.LogInternalInformation("Getting token for repository: {RepoUrl}, Type: {RepoType}", repoUrl, repoType);

        if (repoType == RepoType.GitHub)
        {
            // For GitHub, use OAuth token service
            var token = await _oauthTokenService.GetValidGitHubTokenAsync();
            return token?.AccessToken;
        }
        else if (repoType == RepoType.AzureDevOps)
        {
            // Find the connector for this repository
            var connector = FindAuthConnector(repoUrl, repoType);
            if (connector == null)
            {
                _logger.LogInternalWarning("No auth connector found for Azure DevOps URL: {Url}", repoUrl);
                return null;
            }

            // Check if managed identity is configured
            if (!string.IsNullOrEmpty(connector.Identity))
            {
                try
                {
                    _logger.LogInternalInformation("Using managed identity for Azure DevOps authentication");

                    // Create connector auth settings from the connector's managed identity
                    var authSettings = ConnectorAuthSettings.CreateFromManagedIdentity(
                        connector.Identity,
                        _hostEnvironment.IsDevelopment(),
                        connector.UseManagedIdentityAsFic,
                        connector.FederatedClientId,
                        connector.FederatedTenantId);

                    // Get credential and acquire token
                    var credential = _authenticationService.GetDataConnectorCredential(authSettings);
                    var tokenResult = await credential.GetTokenAsync(
                        new TokenRequestContext(new[] { Constants.AzureDevOpsScope }),
                        CancellationToken.None);

                    return tokenResult.Token;
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Error getting managed identity token for Azure DevOps");
                    return null;
                }
            }
            else
            {
                // Use OAuth token service
                var organization = CodeRepoUrlHelper.ExtractAzureDevOpsOrganization(repoUrl);
                if (string.IsNullOrEmpty(organization))
                {
                    _logger.LogInternalWarning("Could not extract organization from Azure DevOps URL: {Url}", repoUrl);
                    return null;
                }

                _logger.LogInternalInformation("Using OAuth token for Azure DevOps organization: {Organization}", organization);

                var token = await _oauthTokenService.GetValidAzureDevOpsTokenAsync(organization);
                return token?.AccessToken;
            }
        }

        _logger.LogInternalWarning("Unsupported repository type: {RepoType}", repoType);
        return null;
    }

    public DataConnectorBasicInfo? FindAuthConnector(string repoUrl, RepoType repoType)
    {
        try
        {
            var connectors = _connectorResolver.GetAllDataConnectors();

            if (repoType == RepoType.GitHub)
            {
                // For GitHub, find the global GitHubOAuth connector
                var gitHubConnector = connectors.FirstOrDefault(c =>
                    c.ConnectorType.Equals("GitHubOAuth", StringComparison.OrdinalIgnoreCase));

                return gitHubConnector;
            }
            else if (repoType == RepoType.AzureDevOps)
            {
                // For AzDo, extract organization from URL and find matching connector
                var organization = CodeRepoUrlHelper.ExtractAzureDevOpsOrganization(repoUrl);

                if (string.IsNullOrEmpty(organization))
                {
                    _logger.LogInternalWarning("Could not extract organization from Azure DevOps URL: {Url}", repoUrl);
                    return null;
                }

                // Find AzDo connector with matching organization in extended properties
                var azdoConnector = connectors.FirstOrDefault(c =>
                    c.ConnectorType.Equals("AzureDevOpsOAuth", StringComparison.OrdinalIgnoreCase) &&
                    HasMatchingOrganization(c, organization));

                return azdoConnector;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error finding auth connector for repository URL: {Url}", repoUrl);
            return null;
        }
    }

    private static bool HasMatchingOrganization(DataConnectorBasicInfo connector, string organization)
    {
        // Check if connector has extended properties with matching organization
        if (connector.ExtendedProperties?.TryGetValue("organization", out var orgValue) == true)
        {
            var connectorOrg = orgValue.GetString();
            return string.Equals(connectorOrg, organization, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
