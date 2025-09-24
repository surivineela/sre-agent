using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Plugins.Interface;
using Agent.Plugins.Services;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Agent.Plugins.Implementation;

public sealed class AzureDevOpsWorkItemPlugin : IAzureDevOpsWorkItemPlugin
{
    private readonly ILogger<AzureDevOpsWorkItemPlugin> _logger;
    private readonly TsgCrawlerSettings _tsgCrawlerSettings;
    private readonly IAuthenticationService _authenticationService;
    private readonly IThreadRepository _threadRepository;
    private readonly IGraphDatabaseClient _graphDatabaseClient;
    private readonly static HttpClient _httpClient = new HttpClient();

    public AzureDevOpsWorkItemPlugin(ILogger<AzureDevOpsWorkItemPlugin> logger,
                                     TsgCrawlerSettings tsgCrawlerSettings,
                                     IAuthenticationService authenticationService,
                                     IThreadRepository threadRepository,
                                     IGraphDatabaseClient graphDatabaseClient)
    {
        _logger = logger;
        _tsgCrawlerSettings = tsgCrawlerSettings;
        _authenticationService = authenticationService;
        _threadRepository = threadRepository;
        _graphDatabaseClient = graphDatabaseClient;
    }

    internal static string ProcessResourceId(string resourceId)
        => resourceId.Replace("/", "_").ToLowerInvariant();

    private async Task<(bool, AzureDevOpsAccessToken?)> IsAzureDevOpsWorkItemPluginConfiguredAndValid(string resourceId)
    {
        resourceId = ProcessResourceId(resourceId);
        // Check if the Azure DevOps Personal Access Token is configured
        AzureDevOpsAccessToken? azdoAccessToken = await _threadRepository.GetAzureDevOpsAccessTokenAsync(resourceId);
        return (azdoAccessToken != null &&
               !string.IsNullOrEmpty(azdoAccessToken.AccessToken) &&
               (azdoAccessToken.ExpiresOn is null || azdoAccessToken.ExpiresOn > DateTime.UtcNow), azdoAccessToken);
    }

    public async Task<string> FindConnectedRepository(string resourceId)
    {
        resourceId = ProcessResourceId(resourceId);
        string repoQuery = $@"g.V().has('id', '{resourceId}').has('isDeleted', false)
                                .outE('SERVES_CODE').inV().has('isDeleted', false)
                                .values('resourceId')";
        var repoResults = await _graphDatabaseClient.Query<string>(repoQuery);
        string repoUrl = repoResults?.FirstOrDefault()?.ToString() ?? "";
        return repoUrl;
    }

    private async Task<string> CreateWorkItemInternal(string repositoryUrl, string accessToken, string title, string description, string[]? tags, string assignedTo = "", string areaPath = "", string iterationPath = "", string workItemType = "Task", string priority = "Medium", string severity = "None", string state = "New")
    {
        // Parse the repository URL
        var (orgUrl, project, _) = ParseRepositoryUrl(repositoryUrl);

        // Create a connection to Azure DevOps
        using var connection = new VssConnection(
            new Uri(orgUrl),
            new VssBasicCredential(string.Empty, accessToken));

        // Get a client for work item tracking
        using var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

        // Create the patch document
        var patchDocument = new JsonPatchDocument
        {
            new JsonPatchOperation()
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/System.Title",
                Value = title
            },
            new JsonPatchOperation()
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/System.Description",
                Value = description
            },
            new JsonPatchOperation()
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/System.Tags",
                Value = string.Join(";", tags ?? Array.Empty<string>())
            }
        };

        // Add optional fields if provided
        if (!string.IsNullOrEmpty(assignedTo))
        {
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/System.AssignedTo",
                Value = assignedTo
            });
        }

        if (!string.IsNullOrEmpty(areaPath))
        {
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/System.AreaPath", 
                Value = areaPath
            });
        }

        if (!string.IsNullOrEmpty(iterationPath))
        {
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/System.IterationPath",
                Value = iterationPath
            });
        }

        if (!string.IsNullOrEmpty(priority) && priority != "Medium")
        {
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/Microsoft.VSTS.Common.Priority",
                Value = priority
            });
        }

        if (!string.IsNullOrEmpty(severity) && severity != "None")
        {
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/Microsoft.VSTS.Common.Severity",
                Value = severity
            });
        }

        if (!string.IsNullOrEmpty(state) && state != "New")
        {
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/System.State",
                Value = state
            });
        }

        // Create the work item
        var workItem = await witClient.CreateWorkItemAsync(patchDocument, project, workItemType);

        // Extract the HTML URL (user-friendly work item link)
        var htmlUrl = workItem.Links.Links["html"] as ReferenceLink;

        _logger.LogInternalInformation($"Work item created successfully. ID: {workItem.Id}, URL: {htmlUrl?.Href}");
        return htmlUrl?.Href ?? string.Empty;
    }

    public async Task<string> CreateWorkItemWithoutResourceLinkage(string repositoryUrl, string title, string description, string[]? tags, string assignedTo = "", string areaPath = "", string iterationPath = "", string workItemType = "Task", string priority = "Medium", string severity = "None", string state = "New")
    {
        try
        {
            // Validate the repository URL
            if (string.IsNullOrEmpty(repositoryUrl))
            {
                throw new ArgumentException("Repository URL cannot be null or empty.", nameof(repositoryUrl));
            }

            var azdoRepoRegex = new Regex(GraphService.AzDoRepoRegexPattern, RegexOptions.Compiled);
            var match = azdoRepoRegex.Match(repositoryUrl);
            if (!match.Success)
            {
                throw new ArgumentException("Invalid Azure DevOps repository URL.", nameof(repositoryUrl));
            }

            _logger.LogInternalInformation($"Creating work item: '{title}' for repository: {repositoryUrl}");

            // Get token for Azure DevOps authentication
            var token = await GetToken();

            // Create the work item using the shared internal method
            return await CreateWorkItemInternal(repositoryUrl, token.AccessToken, title, description, tags, assignedTo, areaPath, iterationPath, workItemType, priority, severity, state);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error creating work item without resource linkage: {ex.Message}");
            throw;
        }
    }

    public async Task<string> CreateWorkItem(string resourceId, string title, string description, string[]? tags, string assignedTo = "", string areaPath = "", string iterationPath = "", string workItemType = "Task", string priority = "Medium", string severity = "None", string state = "New")
    {
        try
        {
            resourceId = ProcessResourceId(resourceId);
            (bool isValid, AzureDevOpsAccessToken? token) = await IsAzureDevOpsWorkItemPluginConfiguredAndValid(resourceId);
            if (!isValid)
            {
                throw new InvalidOperationException("Azure DevOps Personal Access Token is not configured. Please authenticate via Azure DevOps authentication flow.");
            }

            _logger.LogInternalInformation($"Creating work item: '{title}' for resource: {resourceId}");
            string repoUrl = await FindConnectedRepository(resourceId);

            if (string.IsNullOrEmpty(repoUrl))
            {
                throw new InvalidOperationException($"No connected repository found for resource ID: {resourceId}");
            }

            // Create the work item using the shared internal method
            return await CreateWorkItemInternal(repoUrl, token!.AccessToken, title, description, tags, assignedTo, areaPath, iterationPath, workItemType, priority, severity, state);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error creating work item: {ex.Message}");
            throw;
        }
    }

    private async Task<string> GetIaCTypeFromFiles(string token, string repoUrl, string branch = "main", string fileMatches = "*bicep,*yaml,*yml,*json,*tf*")
    {
        Dictionary<string, string> StaticFileMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [".bicep"] = "Bicep",
            [".tf"] = "Terraform",
            [".tf.json"] = "Terraform",
        };

        Dictionary<string, List<string>> detectedTools = new();

        // Get the files from the repo
        Dictionary<string, string> filesContent = await GetFilesFromRepo(token, repoUrl, branch, fileMatches);

        foreach (var f in filesContent)
        {
            string fileName = f.Key;
            string fileContent = f.Value;
            string ext = Path.GetExtension(fileName);

            // Detect Helm
            if (fileName.Equals("Chart.yaml", StringComparison.OrdinalIgnoreCase) ||
                fileContent.Contains($"{Path.DirectorySeparatorChar}templates{Path.DirectorySeparatorChar}"))
            {
                if (!detectedTools.ContainsKey("Helm"))
                    detectedTools["Helm"] = new List<string>();
                detectedTools["Helm"].Add(fileName);
            }

            // Static file map (Terraform, Bicep)
            if (StaticFileMap.TryGetValue(ext, out var tool))
            {
                if (tool == "Terraform" && fileContent.Contains("provider \"azurerm\""))
                {
                    if (!detectedTools.ContainsKey("Terraform (Azure)"))
                        detectedTools["Terraform (Azure)"] = new List<string>();
                    detectedTools["Terraform (Azure)"].Add(fileName);
                }
                else if (tool == "Bicep")
                {
                    if (!detectedTools.ContainsKey("Bicep"))
                        detectedTools["Bicep"] = new List<string>();
                    detectedTools["Bicep"].Add(fileName);
                }
            }

            // ARM Templates
            else if (ext is ".json")
            {
                if (fileContent.Contains("\"$schema\"") && fileContent.Contains("management.azure.com"))
                {
                    if (!detectedTools.ContainsKey("ARM Template"))
                        detectedTools["ARM Template"] = new List<string>();
                    detectedTools["ARM Template"].Add(fileName);
                }
            }

            // Ansible (Azure)
            else if (ext is ".yaml" or ".yml")
            {
                if (Regex.IsMatch(fileContent, @"azure_rm_|community\.azure"))
                {
                    if (!detectedTools.ContainsKey("Ansible (Azure)"))
                        detectedTools["Ansible (Azure)"] = new List<string>();
                    detectedTools["Ansible (Azure)"].Add(fileName);
                }
            }

            // Pulumi - C#
            else if (ext is ".cs")
            {
                if (fileContent.Contains("Pulumi") && fileContent.Contains("Azure"))
                {
                    if (!detectedTools.ContainsKey("Pulumi (C#)"))
                        detectedTools["Pulumi (C#)"] = new List<string>();
                    detectedTools["Pulumi (C#)"].Add(fileName);
                }
            }

            // Pulumi - TypeScript/JavaScript
            else if (ext is ".ts" or ".js")
            {
                if (Regex.IsMatch(fileContent, @"@pulumi/azure", RegexOptions.IgnoreCase))
                {
                    if (!detectedTools.ContainsKey("Pulumi (TypeScript/JavaScript)"))
                        detectedTools["Pulumi (TypeScript/JavaScript)"] = new List<string>();
                    detectedTools["Pulumi (TypeScript/JavaScript)"].Add(fileName);
                }
            }

            // Pulumi - Python
            else if (ext is ".py")
            {
                if (Regex.IsMatch(fileContent, @"import pulumi_azure", RegexOptions.IgnoreCase))
                {
                    if (!detectedTools.ContainsKey("Pulumi (Python)"))
                        detectedTools["Pulumi (Python)"] = new List<string>();
                    detectedTools["Pulumi (Python)"].Add(fileName);
                }
            }
        }

        if (detectedTools.Count > 0)
        {
            var results = detectedTools.Select(kvp =>
                $"{kvp.Key}: {string.Join(", ", kvp.Value.Take(5))}{(kvp.Value.Count > 5 ? $" (and {kvp.Value.Count - 5} more)" : "")}");
            return string.Join("\n", results);
        }

        else
        {
            return "No IaC tools found";
        }
    }

    public async Task<Dictionary<string, string>> GetFilesFromRepo(string token, string repoUrl, string branch = "main", string fileMatches = "*bicep")
    {
        try
        {
            _logger.LogInternalInformation($"Downloading files matching pattern '{fileMatches}' from branch '{branch}'");

            // Parse the repository URL
            var (orgUrl, project, repoName) = ParseRepositoryUrl(repoUrl);

            // API URL for items
            string apiVersion = "7.0";
            string apiUrl = $"{orgUrl}/{project}/_apis/git/repositories/{repoName}/items?recursionLevel=Full&versionDescriptor.version={branch}&versionDescriptor.versionType=branch&api-version={apiVersion}";

            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}"));
            var itemsRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            itemsRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            itemsRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Get all items in the repository
            var response = await _httpClient.SendAsync(itemsRequest);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var itemsResponse = JsonSerializer.Deserialize<GitItemsResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (itemsResponse?.Value == null)
            {
                throw new Exception("Failed to retrieve repository items");
            }

            // Helper method to match on a particular file pattern.
            bool IsWildcardMatch(string path, string pattern)
            {
                string fileName = Path.GetFileName(path);

                if (pattern.StartsWith("*"))
                {
                    string suffix = pattern.Substring(1);
                    return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
                }

                else if (pattern.EndsWith("*"))
                {
                    string prefix = pattern.Substring(0, pattern.Length - 1);
                    return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                }

                else if (pattern.Contains("*"))
                {
                    string[] parts = pattern.Split('*');
                    return fileName.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase) &&
                           fileName.EndsWith(parts[1], StringComparison.OrdinalIgnoreCase);
                }

                return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
            }

            // Filter blobs by file patterns using wildcard matching.
            IEnumerable<string> matches = fileMatches.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(pattern => pattern.Trim())
                .Where(pattern => !string.IsNullOrWhiteSpace(pattern));

            var filteredBlobs = itemsResponse.Value.Where(t => matches.Any(pattern => IsWildcardMatch(t?.Path ?? string.Empty, pattern)));

            if (filteredBlobs == null || !filteredBlobs.Any())
            {
                _logger.LogInternalInformation($"No files matching '{fileMatches}' found in repository {repoUrl} on branch {branch}");
                return new Dictionary<string, string>();
            }

            Dictionary<string, string> fileContents = new(StringComparer.OrdinalIgnoreCase);

            // Download each file
            foreach (var file in filteredBlobs)
            {
                try
                {
                    // Remove leading slash and convert to OS-specific path
                    var relativePath = (file.Path ?? string.Empty)
                        .TrimStart('/')
                        .Replace('/', Path.DirectorySeparatorChar);

                    var fileDownloadRequest = new HttpRequestMessage(HttpMethod.Get, file.Url);
                    fileDownloadRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                    _logger.LogInternalInformation($"Downloading file: {file.Url}");
                    var fileResponse = await _httpClient.SendAsync(fileDownloadRequest);
                    fileResponse.EnsureSuccessStatusCode();
                    string fileContent = await fileResponse.Content.ReadAsStringAsync();
                    var pathKey = file.Path ?? string.Empty;
                    fileContents[pathKey] = fileContent;

                }
                catch (Exception fileEx)
                {
                    _logger.LogInternalError(fileEx, $"Error downloading file {file.Path}: {fileEx.Message}");
                }
            }

            return fileContents;
        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error downloading files from {repoUrl} on branch {branch}: {ex.Message}");
            throw;
        }
    }

    internal static (string orgUrl, string project, string repo) ParseRepositoryUrl(string repoUrl)
    {
        try
        {
            var uri = new Uri(repoUrl);
            string orgUrl, project, repo;

            if (uri.Host == "dev.azure.com" || uri.Host.Contains(".dev.azure.com"))
            {
                // Format: https://dev.azure.com/{org}/{project}/_git/{repo}
                var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 4 || parts[parts.Length - 2] != "_git")
                {
                    throw new ArgumentException("Invalid repository URL format");
                }

                var org = parts[0];
                project = parts[1];
                repo = parts[parts.Length - 1];
                orgUrl = $"https://dev.azure.com/{org}";
            }
            else if (uri.Host.EndsWith("visualstudio.com"))
            {
                // Format: https://{org}.visualstudio.com/{project}/_git/{repo}
                var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 3 || parts[1] != "_git")
                {
                    throw new ArgumentException("Invalid repository URL format");
                }

                var org = uri.Host.Split('.')[0];
                project = parts[0];
                repo = parts[2];
                orgUrl = $"https://{uri.Host}";
            }
            else
            {
                throw new ArgumentException("Unsupported repository URL format");
            }

            return (orgUrl, project, repo);
        }

        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse repository URL: {ex.Message}", ex);
        }
    }

    public async Task<AzureDevOpsAccessToken> GetToken()
    {
        string token = "";
        DateTime? expiresOnUTC = null;

        if (!string.IsNullOrEmpty(_tsgCrawlerSettings.DevOpsRepoSettings.PersonalAccessToken))
        {
            token = _tsgCrawlerSettings.DevOpsRepoSettings.PersonalAccessToken;
            _logger.LogInternalInformation("Using personal access token for Azure DevOps authentication.");
        }

        else
        {
            TokenCredential credentials = await _authenticationService.GetArmOperationCredential();
            const string scope = "499b84ac-1321-427f-aa17-267ca6975798/.default";
            var tokenRequestContext = new TokenRequestContext(new[] { scope });
            var accessToken = await credentials.GetTokenAsync(tokenRequestContext, CancellationToken.None);
            token = accessToken.Token;
            expiresOnUTC = accessToken.ExpiresOn.UtcDateTime;
        }

        return new AzureDevOpsAccessToken(token, expiresOnUTC);
    }

    public async Task<string> GetIaCForAzureDevOps(string resourceId, string branch, string fileMatches)
    {
        try
        {
            (bool isValid, AzureDevOpsAccessToken? token) = await IsAzureDevOpsWorkItemPluginConfiguredAndValid(resourceId);
            if (!isValid)
            {
                throw new InvalidOperationException("Azure DevOps Personal Access Token is not configured. Please authenticate via Azure DevOps authentication flow.");
            }

            string repoUrl = await FindConnectedRepository(resourceId);
            return await GetIaCTypeFromFiles(token?.AccessToken ?? string.Empty, repoUrl, branch, fileMatches);
        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error getting IaC for Azure DevOps: {ex.Message}");
            throw;
        }
    }

    public async Task<string> LinkRepository(string resourceId, string repoUrl, string @namespace = "", string resourceName = "", string subType = "")
    {
        try
        {
            var appNodeId = resourceId = ProcessResourceId(resourceId);
            string vertexFilter = $"hasId('{appNodeId}')";
            string query = $@"g.V().{vertexFilter}.has('isDeleted', false)";

            // if app has a namespace and subType starts with "k8s", this is a k8s resource
            if (!string.IsNullOrEmpty(@namespace) && !string.IsNullOrEmpty(resourceName) && !string.IsNullOrEmpty(subType) && subType.StartsWith("k8s", StringComparison.OrdinalIgnoreCase))
            {
                // for AKS resources, resourceId is the AKS cluster resource id, not the specific object resource id in graph
                query = $@"g.V().has('resourceName','{resourceName}').has('namespace','{@namespace}').has('resourceType','{subType}').has('clusterResourceId','{resourceId}').has('isDeleted', false).values('id')";
                var appResult = await _graphDatabaseClient.Query(query);
                var appidList = appResult.ToList();
                if (appidList.Count == 0)
                {
                    throw new ArgumentException($"the resource {resourceId} {resourceName} is not found.");
                }
                appNodeId = appidList[0].ToString();
            }
            else
            {
                var appNodeResults = await _graphDatabaseClient.Query(query);
                if (!appNodeResults.Any())
                {
                    throw new ArgumentException($"the resource {resourceId} {resourceName} is not found.");
                }
            }

            var sourceCodeNode = new SourceCodeRepoNode(repoUrl);
            var sourceCodeNodeResults = await _graphDatabaseClient.Query($"g.V('{sourceCodeNode.GetNodeId()}').hasLabel('{sourceCodeNode.GetNodeLabel()}').has('isDeleted', false)");

            if (!sourceCodeNodeResults.Any())
            {
                await _graphDatabaseClient.AddOrUpdateNodeAsync(sourceCodeNode);
            }

            var edge = new NonCrawledEdge(appNodeId, sourceCodeNode.GetNodeId(), "SERVES_CODE");
            await _graphDatabaseClient.AddOrUpdateEdgeAsync(edge);
            return $"Source code repo with url: {repoUrl} linked successfully for resourceId: {resourceId}.";
        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error linking source code");
            throw;
        }
    }

    public async Task<string> ConnectRepository(string resourceId, string repositoryUrl)
    {
        var azdoRepoRegex = new Regex(GraphService.AzDoRepoRegexPattern, RegexOptions.Compiled);
        bool AzdoRegexMatch(string url) => !string.IsNullOrEmpty(url) && azdoRepoRegex.IsMatch(url);

        if (!AzdoRegexMatch(repositoryUrl))
        {
            throw new ArgumentException("Repository URL must be a valid Azure DevOps HTTPS Git URL.", nameof(repositoryUrl));
        }

        string connectedRepository = await FindConnectedRepository(resourceId);
        bool alreadyConnectedToSameRepository = connectedRepository.Equals(repositoryUrl, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(connectedRepository) && !alreadyConnectedToSameRepository)
        {
            string message = $"Resource Id: {resourceId} is already connected to: {connectedRepository}. Please disconnect the repository before connecting again.";
            throw new ArgumentException(message);
        }

        // Check if there is already an AzDo token available.
        resourceId = ProcessResourceId(resourceId);
        var azdoAccessToken = await _threadRepository.GetAzureDevOpsAccessTokenAsync(resourceId);
        var azdoAccessTokenConfigured = azdoAccessToken != null &&
            !string.IsNullOrEmpty(azdoAccessToken.AccessToken) &&
            (azdoAccessToken.ExpiresOn is null || azdoAccessToken.ExpiresOn > DateTime.UtcNow);

        // If not, create one.
        if (!azdoAccessTokenConfigured)
        {
            string linkedRepository = await LinkRepository(resourceId, repositoryUrl);
            AzureDevOpsAccessToken authToken = await GetToken();
            azdoAccessToken = await _threadRepository.CreateOrUpdateAzureDevOpsAccessTokenAsync(new(authToken.AccessToken, ExpiresOn: authToken.ExpiresOn), resourceId);
        }

        if (!await CanCreateWorkItemsAsync(repositoryUrl, azdoAccessToken?.AccessToken ?? string.Empty))
        {
            throw new InvalidOperationException("The provided Azure DevOps token has been linked but does not have permission to create work items in the repository. Please check the permissions.");
        }

        DateTime? localTimeOfTokenExpiration = azdoAccessToken?.ExpiresOn.GetValueOrDefault().ToLocalTime();
        string action = alreadyConnectedToSameRepository ? "relinked i.e., refreshed token" : "linked";
        string loggedMessage = $"Successfully {action}: {resourceId} -> {repositoryUrl} - the expiration time of the token is: {azdoAccessToken?.ExpiresOn.GetValueOrDefault()} UTC or {localTimeOfTokenExpiration} Local Time.";
        _logger.LogInternalInformation(loggedMessage);
        return loggedMessage;
    }

    public async Task<bool> CanCreateWorkItemsAsync(string repositoryUrl, string token)
    {
        // Regex to extract organization and project from AzDO URL
        var match = Regex.Match(repositoryUrl, GraphService.AzDoRepoRegexPattern, RegexOptions.IgnoreCase);

        if (!match.Success)
            throw new ArgumentException("Invalid Azure DevOps repository URL format.", nameof(repositoryUrl));

        string organization = match.Groups["organization"].Value;
        string project = match.Groups["project"].Value;

        var url = $"https://dev.azure.com/{organization}/{project}/_apis/wit/workitemtypes?api-version=7.1-preview.2";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<string> DisconnectRepository(string resourceId)
    {
        // First get the connected repository url.
        string connectedRepositoryUrl = await FindConnectedRepository(resourceId);
        if (string.IsNullOrEmpty(connectedRepositoryUrl))
        {
            return $"No connected repository found for resource ID: {resourceId} to disconnect.";
        }

        resourceId = ProcessResourceId(resourceId);

        // First unlink the repository from the resource and then, delete the Azure DevOps access token.
        await _graphDatabaseClient.SoftDeleteConnectedRepositoryByResourceId(resourceId);
        string connectedRepository = await FindConnectedRepository(resourceId);
        bool noConnectedRepository = string.IsNullOrEmpty(connectedRepository);

        bool successfullyDeletedToken = await _threadRepository.DeleteAzureDevOpsAccessTokenAsync(resourceId);
        if (noConnectedRepository && successfullyDeletedToken)
        {
            string loggedMessage = $"Successfully unlinked / disconnected {resourceId} to {connectedRepositoryUrl}";
            _logger.LogInternalInformation(loggedMessage);
            return loggedMessage;
        }

        else
        {
            string errorMessage = $"Failed to unlink / disconnect {resourceId} from {connectedRepositoryUrl}. The Azure DevOps access token may not exist or may have already been deleted.";
            _logger.LogInternalError(errorMessage);
            return errorMessage;
        }
    }

    // Classes to deserialize the JSON response
    internal class GitItemsResponse
    {
        public List<GitItem>? Value { get; set; }
    }

    internal class GitItem
    {
        [JsonPropertyName("objectId")]
        public string? ObjectId { get; set; }

        [JsonPropertyName("gitObjectType")]
        public string? GitObjectType { get; set; }

        [JsonPropertyName("commitId")]
        public string? CommitId { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("_links")]
        public Links? Links { get; set; }
    }

    internal class Links
    {
        [JsonPropertyName("self")]
        public Link? Self { get; set; }

        [JsonPropertyName("repository")]
        public Link? Repository { get; set; }

        [JsonPropertyName("blob")]
        public Link? Blob { get; set; }
    }

    internal class Link
    {
        [JsonPropertyName("href")]
        public string? Href { get; set; }
    }
}
