using System.Runtime.Caching;
using System.Text.Json.Serialization;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Author = Agent.Core.Models.Api.v1.Author;
using Message = Agent.Core.Models.Api.v1.Message;

namespace Agent.Plugins;

public class GenevaActionsPlugin : IGenevaActionsPlugin
{
    private readonly ICMWorkflowClient _icmWorkflowClient;
    private readonly KustoClient _kustoClient;
    private readonly ILogger<GenevaActionsPlugin> _logger;
    private readonly CosmosClient _cosmosDBService;
    private readonly CosmosDBSettings _cosmosDBSettings;
    private readonly GenevaActionsSettings _genevaActionsSettings;
    private readonly IICMAPIClient _icmAPIClient;

    private readonly bool _icmWorkflowReadOnly;
    private const string _genevaActionSecretName = "GenevaActionConfigs";

    private Lazy<Task<List<GenevaActionConfig>>> _lazyGenevaActions;
    private OneBranchApprovalService _oneBranchApprovalService;
    private IKeyVaultService _keyVaultService;
    private IThreadRepository _threadRepository;
    private IAgentOutboundCommunicationService _agentOutboundCommunicationService;

    // Static MemoryCache for approval requests shared across all instances
    private static readonly MemoryCache _approvalRequestsCache = MemoryCache.Default;

    public Guid? ThreadId { get; set; }

    public GenevaActionsPlugin(
        ICMWorkflowClient icmWorkflowClient,
        KustoClient kustoPlugin,
        ILogger<GenevaActionsPlugin> logger,
        CosmosClient cosmosDBService,
        CosmosDBSettings cosmosDBSettings,
        GenevaActionsSettings genevaActionsSettings,
        ICMWorkflowSettings iCMWorkflowSettings,
        OneBranchApprovalService oneBranchApprovalService,
        IICMAPIClient iCMAPIClient,
        IKeyVaultService keyVaultService,
        IThreadRepository threadRepository,
        IAgentOutboundCommunicationService agentOutboundCommunicationService)
    {
        _logger = logger;
        _icmWorkflowClient = icmWorkflowClient;
        _kustoClient = kustoPlugin;
        _cosmosDBService = cosmosDBService;
        _cosmosDBSettings = cosmosDBSettings;
        _genevaActionsSettings = genevaActionsSettings;
        _icmWorkflowReadOnly = iCMWorkflowSettings.ReadOnly;
        _lazyGenevaActions = new Lazy<Task<List<GenevaActionConfig>>>(() => InitializeGenevaActionsConfig());
        _oneBranchApprovalService = oneBranchApprovalService;
        _icmAPIClient = iCMAPIClient;
        _keyVaultService = keyVaultService;
        _threadRepository = threadRepository;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;

    }

    private async Task<List<GenevaActionConfig>> InitializeGenevaActionsConfig()
    {
        var allGenevaActions = new List<GenevaActionConfig>();
        _logger.LogInternalInformation("[GenevaActionsPlugin] Initializing Geneva Actions Config");


        try
        {
            if (!_keyVaultService.IsEnabled)
            {
                _logger.LogInternalWarning("[GenevaActionsPlugin] Key Vault Service is not enabled. Geneva Actions Config will not be loaded.");
                return allGenevaActions;
            }

            string json = await _keyVaultService.ReadSecretAsync(_genevaActionSecretName);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogInternalWarning($"[GenevaActionsPlugin] Geneva Actions Config not found in Key Vault with name {_genevaActionSecretName}. Returning empty list.");
                return allGenevaActions;
            }

            var genevaActionsConfigs = JsonConvert.DeserializeObject<List<GenevaActionConfig>>(json);

            if (genevaActionsConfigs == null || genevaActionsConfigs.Count == 0)
            {
                _logger.LogInternalWarning("[GenevaActionsPlugin] No Geneva Actions Config found in CosmosDB. Returning empty list.");
                return allGenevaActions;
            }

            allGenevaActions = genevaActionsConfigs;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[GenevaActionsPlugin] Error reading alert details from CosmosDB: {ex.Message}");
        }

        return allGenevaActions;
    }

    private async Task<List<GenevaActionConfig>> GetGenevaActions()
    {
        return await _lazyGenevaActions.Value;
    }

    private async Task<string> ExecuteGenevaActionWorkflow(GenevaActionConfig genevaActionConfig, Dictionary<string, string> inputParameters)
    {
        try
        {
            var payload = JsonConvert.SerializeObject(inputParameters);
            var response = await _icmWorkflowClient.SendICMWorkflowRequest(genevaActionConfig.WorkflowName, payload, genevaActionConfig.TenantId);
            _logger.LogInternalInformation($"[GenevaActionsPlugin] [execute_geneva_action_workflow] - workflowName: {genevaActionConfig.WorkflowName}, statusCode: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                string errorMessage = await response.Content.ReadAsStringAsync();
                return errorMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[GenevaActionsPlugin] Failed to execute geneva action: {genevaActionConfig.ActionName} with parameters: {JsonConvert.SerializeObject(inputParameters)}");
            throw;
        }
    }

    public async Task<string> ListInputParametersForGenevaAction(string actionName)
    {
        var logMessage = $"[list_input_parameters_for_geneva_action] Invoked with actionName {actionName}.";
        _logger.LogInternalInformation(logMessage);
        var genevaAction = (await GetGenevaActions()).Where(x => x.ActionName == actionName).FirstOrDefault();
        if (genevaAction == null)
        {
            _logger.LogInternalWarning($"[GenevaActionsPlugin] No Geneva Action found for actionName: {actionName}");
            var availableActions = string.Join(", ", (await GetGenevaActions()).Select(x => x.ActionName));
            _logger.LogInternalInformation($"[GenevaActionsPlugin] Available Geneva Actions: {availableActions}");
            return $"No Geneva Action found for actionName: {actionName}";
        }
        return $"For actionName: {actionName}. Required parameters are: {string.Join(", ", genevaAction.WorkflowInputParameters)}";
    }

    private async Task<string> GetApprovalStatus(string documentId)
    {
        Agent.Core.Models.OneBranchApprovalStatus? approvalStatus = null;
        var logMessage = $"[get_approval_status] Invoked with documentId {documentId}. Checking approval status.";
        string? message = null;
        _logger.LogInternalInformation(logMessage);

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return "Document ID is required to check approval status.";
        }

        if (!_oneBranchApprovalService.IsEnabled)
        {
            return "OneBranch Approval Service is not enabled. Cannot check approval status.";
        }

        var approvalRequestDetails = GetApprovalRequestDetails(documentId);

        if (approvalRequestDetails == null)
        {
            return $@"
No approval request found for document ID: {documentId}. Please ensure the document ID is correct and the approval request has been created; otherwise, execute a Geneva Action with the same parameters to create a new approval request.
";
        }


        if (approvalRequestDetails.ApprovalStatus == OnebranchApprovalStatus.NotStarted)
        {
            int delayAmountInSeconds = 30;
            var approvalStatusTask = _oneBranchApprovalService.PollForApprovalAsync(documentId);

            while (!approvalStatusTask.IsCompleted || !approvalStatusTask.IsFaulted)
            {
                var delayTask = Task.Delay(TimeSpan.FromSeconds(delayAmountInSeconds), CancellationToken.None);
                var finishedTask = await Task.WhenAny(delayTask, approvalStatusTask);

                if (finishedTask == approvalStatusTask)
                {
                    approvalStatus = await approvalStatusTask;
                    break;
                }

                delayAmountInSeconds *= 2; // Exponential backoff
                await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                    ThreadId!.Value,
                    string.Empty,
                    new ChatMessage(ChatRole.Assistant, $"Still waiting for approval, please approve the action. Will check again in {delayAmountInSeconds} seconds."));
            }
            string? status = approvalStatus?.Data?.ApprovalDocumentCompleteDetails?.Action;
            if (status != "Approve")
            {
                message = $"Geneva Action execution was rejected by {approvalStatus?.Data?.ApprovalDocumentCompleteDetails?.Principal}. Status: {status}. Comments: {approvalStatus?.Data?.ApprovalDocumentCompleteDetails?.Comments}";
                _logger.LogInternalInformation(message);

                // Update the approval status in our cache
                var cachedRequestDetails = GetApprovalRequestDetails(documentId);
                if (cachedRequestDetails != null)
                {
                    cachedRequestDetails.ApprovalStatus = status?.ToLowerInvariant() switch
                    {
                        "cancelled" => OnebranchApprovalStatus.Cancelled,
                        "denied" => OnebranchApprovalStatus.Denied,
                        _ => OnebranchApprovalStatus.Denied
                    };
                    UpdateApprovalRequestStatus(ThreadId!.Value, cachedRequestDetails.ActionName, cachedRequestDetails.InputParameters, documentId, cachedRequestDetails, _genevaActionsSettings);
                }

                return message;
            }
        }

        message = $"Geneva Action approved by {approvalStatus?.Data?.ApprovalDocumentCompleteDetails?.Principal}. Proceeding with execution.";

        // Update the approval status in our cache to approved
        var approvedRequestDetails = GetApprovalRequestDetails(documentId);
        if (approvedRequestDetails != null)
        {
            approvedRequestDetails.ApprovalStatus = OnebranchApprovalStatus.Approved;
            UpdateApprovalRequestStatus(ThreadId!.Value, approvedRequestDetails.ActionName, approvedRequestDetails.InputParameters, documentId, approvedRequestDetails, _genevaActionsSettings);
        }

        await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            ThreadId!.Value,
            string.Empty,
            new ChatMessage(ChatRole.Assistant, message!));
        return message;
    }

    private async Task<bool> IsSubscriptionInternal(string subscriptionId)
    {
        var logMessage = $"[is_subscription_internal] Checking if subscription {subscriptionId} is internal.";
        _logger.LogInternalInformation(logMessage);
        var kustoQuery = $@"DataStudio_ServiceTree_AzureSubscription_Snapshot
                | where SubscriptionId == '{subscriptionId}'
                | project ServiceName, SubscriptionId, ServiceId, Environment
                | take 1";

        var reader = await _kustoClient.PerformQueryAsync($"https://servicetreepublic.westus.kusto.windows.net", "Shared", kustoQuery);
        var kustoResult = new KustoQueryResult(reader, kustoQuery);
        if (!string.IsNullOrWhiteSpace(kustoResult.Result) && kustoResult.Result != "ZERO_ROWS_RETURNED")
        {
            var kustoResultDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(kustoResult.Result);
            if (kustoResultDictionary != null && kustoResultDictionary.Count > 0)
            {
                var subscriptionIdFromKusto = kustoResultDictionary["SubscriptionId"];
                if (subscriptionIdFromKusto == subscriptionId)
                {
                    return true; // Subscription is internal
                }
            }
        }
        return false;
    }

    public async Task<string> ExecuteGenevaAction(string incidentId, string actionName, Dictionary<string, string> inputParameters)
    {

        var logMessage = $"[execute_geneva_action] Invoked with actionName {actionName} and parameters: {JsonConvert.SerializeObject(inputParameters)}";
        _logger.LogInternalInformation(logMessage);
        await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            ThreadId!.Value,
            string.Empty,
            new ChatMessage(ChatRole.Assistant, $@"Invoking geneva action **{actionName}** with parameters:
    {string.Join(Environment.NewLine, inputParameters.Select(kvp => $"    {kvp.Key}: {kvp.Value}"))}")
        );

        var genevaAction = (await GetGenevaActions()).Where(x => x.ActionName.ToLower() == actionName.ToLower()).FirstOrDefault();
        if (genevaAction == null)
        {
            return $"No Geneva Action found for actionName: {actionName}";
        }
        var paramsNotFound = genevaAction.WorkflowInputParameters.Where(x => !inputParameters.ContainsKey(x)).Any();
        if (paramsNotFound)
        {
            return $"Missing input parameters for actionName: {actionName}. Required parameters are: {string.Join(", ", genevaAction.WorkflowInputParameters)}";
        }

        if (_icmWorkflowReadOnly && genevaAction.IsWriteAction)
        {
            return "Success. ICM Workflow Client is in ReadOnly mode.";
        }

        var incident = await _icmAPIClient.GetIncidentAsync(incidentId);
        if (incident == null)
        {
            return $"Incident with ID {incidentId} not found.";
        }

        if (_oneBranchApprovalService.IsEnabled && genevaAction.IsApprovalNeeded)
        {
            logMessage = $"[execute_geneva_action][{DateTime.UtcNow}] Geneva action requires approval. Check for existing approval.";
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                ThreadId!.Value,
                string.Empty,
                new ChatMessage(ChatRole.Assistant, "This geneva action requires approval"));
            _logger.LogInternalInformation(logMessage);
            var approvalRequestDetails = GetApprovalRequestDetails(ThreadId!.Value, actionName, inputParameters);

            if (approvalRequestDetails == null)
            {
                try
                {
                    // Create approval request with detailed information about the action
                    logMessage = $"[execute_geneva_action][{DateTime.UtcNow}] Geneva action requires approval. Creating approval document.";
                    _logger.LogInternalInformation(logMessage);
                    var approvalRequest = new OneBranchApprovalRequest
                    {
                        CorrelationId = Guid.NewGuid().ToString(),
                        Title = $"Geneva Action Approval: {actionName}",
                        RequestDescription = $"Request to execute Geneva Action '{actionName}' with parameters: {JsonConvert.SerializeObject(inputParameters)}",
                        Submitter = "SRE Agent",
                        ServiceTreeGuid = genevaAction.ServiceTreeId?.ToString() ?? "00000000-0000-0000-0000-000000000000",
                        ReleaseApproversAllowed = new List<string> { "AME\\AZURE-ALL-PSV" } // FTE AME account, see https://dev.azure.com/mseng/AzureDevOps/_wiki/wikis/AzureDevOps.wiki/1113/TSG-Azure-Network-Troubleshooting?anchor=security-groups-that-you-need-to-join
                    };

                    await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                        ThreadId!.Value,
                        string.Empty,
                        new ChatMessage(ChatRole.Assistant, "Sending request to Approval Service API to create approval document."));
                    // Create the approval document
                    var approvalResponse = await _oneBranchApprovalService.CreateApprovalDocumentAsync(approvalRequest, actionName, inputParameters);

                    // Add to our static cache
                    AddApprovalRequest(ThreadId!.Value, actionName, inputParameters, approvalResponse.ApprovalDocumentId, new OnebranchApprovalRequestDetails
                    {
                        ActionName = actionName,
                        InputParameters = new Dictionary<string, string>(inputParameters, StringComparer.OrdinalIgnoreCase),
                        ApprovalStatus = OnebranchApprovalStatus.NotStarted,
                        CreatedAt = DateTime.UtcNow,
                        ApprovalRequestUri = new Uri(approvalResponse.ApprovalDocumentUri)
                    });

                    logMessage = $"[execute_geneva_action][{DateTime.UtcNow}] Approval document created, please approve {approvalResponse.ApprovalDocumentUri} to continue.";
                    _logger.LogInternalInformation(logMessage);
                    await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                        ThreadId!.Value,
                        string.Empty,
                        new ChatMessage(ChatRole.Assistant, $@"Approval document created, please approve. (**Requires SAW**)
                    Approval Request URI: {approvalResponse.ApprovalDocumentUri}"));

                    await _icmAPIClient.PostDiscussionEntryAsync(incidentId, @$"
In order to potentially resolve the incident, the following Geneva Action '{actionName}' requires approval. Please review and approve the action:<br><br>
    <b>Approval Document ID:</b> {approvalResponse.ApprovalDocumentId}<br>
    <b>Approval Request Link (Requires SAW):</b> <a href=""{approvalResponse.ApprovalDocumentUri}"" target=""_blank"">Click here to approve</a><br>
    <b>Approval Request Description:</b> {approvalRequest.RequestDescription}<br>
");
                    var approvalStatusWaitingMessage = await GetApprovalStatus(approvalResponse.ApprovalDocumentId);
                    await _icmAPIClient.PostDiscussionEntryAsync(incidentId, approvalStatusWaitingMessage);

                    if (!approvalStatusWaitingMessage.Contains("approved by"))
                    {
                        return approvalStatusWaitingMessage;
                    }
                }
                catch (Exception ex)
                {
                    var errorMessage = $"[execute_geneva_action][{DateTime.UtcNow}] Error in approval workflow: {ex.Message}";
                    _logger.LogInternalWarning(errorMessage);
                    return errorMessage;
                }
            }
            else if (approvalRequestDetails.ApprovalStatus == OnebranchApprovalStatus.Approved)
            {
                logMessage = $"[execute_geneva_action][{DateTime.UtcNow}] Approval already exists for actionName: {actionName}. Proceeding with execution.";
                _logger.LogInternalInformation(logMessage);
                await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                    ThreadId!.Value,
                    string.Empty,
                    new ChatMessage(ChatRole.Assistant, $"Approval already exists for actionName: {actionName}. Proceeding with execution."));
            }
            else
            {
                logMessage = $"[execute_geneva_action][{DateTime.UtcNow}] Approval request is still pending for actionName: {actionName}. Waiting for approval.";
                _logger.LogInternalInformation(logMessage);
                return logMessage;
            }
        }


        var subscriptionId = inputParameters.ContainsKey("subscriptionId") ? inputParameters["subscriptionId"] : (inputParameters.ContainsKey("subscription") ? inputParameters["subscription"] : null);
        //if (!string.IsNullOrWhiteSpace(subscriptionId))
        //{
        //    if (!genevaAction.IsAllowedOnExternalSubs && !(await IsSubscriptionInternal(subscriptionId)))
        //    {
        //        logMessage = $"[is_subscription_internal] The subscription {subscriptionId} is external. This action is not allowed.";
        //        _logger.LogInternalWarning(logMessage);
        //        return logMessage;
        //    }
        //}

        _logger.LogInternalInformation("[GenevaActionsPlugin] Proceeding with executing Geneva Action");
        var response = await ExecuteGenevaActionWorkflow(genevaAction, inputParameters);
        await _icmAPIClient.PostDiscussionEntryAsync(incidentId, response);
        await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            ThreadId!.Value,
            string.Empty,
            new ChatMessage(ChatRole.Assistant, $"Geneva Actions response: {response}"));
        return response;
    }

    // Approval requests management methods
    private static string GenerateCacheKey(Guid threadId, string actionName, Dictionary<string, string> inputParameters)
    {
        // Create a deterministic key based on threadId, actionName, and sorted parameters
        var sortedParams = inputParameters.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}={kvp.Value}");
        var paramString = string.Join("|", sortedParams);
        return $"approval_{threadId}_{actionName}_{paramString}";
    }

    private static string GenerateCacheKeyByDocumentId(string documentId)
    {
        return $"approval_doc_{documentId}";
    }

    public static bool AddApprovalRequest(Guid threadId, string actionName, Dictionary<string, string> inputParameters, string documentId, OnebranchApprovalRequestDetails details)
    {
        var cacheKey = GenerateCacheKey(threadId, actionName, inputParameters);
        var documentCacheKey = GenerateCacheKeyByDocumentId(documentId);

        // Add with default expiration (no automatic expiration for pending requests)
        var policy = new CacheItemPolicy();

        try
        {
            _approvalRequestsCache.Add(cacheKey, details, policy);
            _approvalRequestsCache.Add(documentCacheKey, details, policy);
            return true;
        }
        catch
        {
            return false; // Item already exists
        }
    }

    public static bool RemoveApprovalRequest(Guid threadId, string actionName, Dictionary<string, string> inputParameters, string documentId)
    {
        var cacheKey = GenerateCacheKey(threadId, actionName, inputParameters);
        var documentCacheKey = GenerateCacheKeyByDocumentId(documentId);

        var removed1 = _approvalRequestsCache.Remove(cacheKey) != null;
        var removed2 = _approvalRequestsCache.Remove(documentCacheKey) != null;

        return removed1 || removed2;
    }

    public static OnebranchApprovalRequestDetails? GetApprovalRequestDetails(string documentId)
    {
        var documentCacheKey = GenerateCacheKeyByDocumentId(documentId);
        return _approvalRequestsCache.Get(documentCacheKey) as OnebranchApprovalRequestDetails;
    }

    public static OnebranchApprovalRequestDetails? GetApprovalRequestDetails(Guid threadId, string actionName, Dictionary<string, string> inputParameters)
    {
        var cacheKey = GenerateCacheKey(threadId, actionName, inputParameters);
        return _approvalRequestsCache.Get(cacheKey) as OnebranchApprovalRequestDetails;
    }

    public static void UpdateApprovalRequestStatus(Guid threadId, string actionName, Dictionary<string, string> inputParameters, string documentId, OnebranchApprovalRequestDetails details, GenevaActionsSettings settings)
    {
        var cacheKey = GenerateCacheKey(threadId, actionName, inputParameters);
        var documentCacheKey = GenerateCacheKeyByDocumentId(documentId);

        CacheItemPolicy policy;

        // If approved, set configurable expiration
        if (details.ApprovalStatus == OnebranchApprovalStatus.Approved)
        {
            policy = new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(settings.ApprovedRequestCacheExpirationHours)
            };
        }
        else
        {
            // For other statuses, use default policy (no automatic expiration)
            policy = new CacheItemPolicy();
        }

        _approvalRequestsCache.Set(cacheKey, details, policy);
        _approvalRequestsCache.Set(documentCacheKey, details, policy);
    }

    private class AgentFactoryConfigCosmos<T>
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string? Id { get; set; }
        public T? Content { get; set; }

        [JsonPropertyName("_ts")]
        [JsonProperty("_ts")]
        public int Timestamp { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public DateTimeOffset Datetime => DateTimeOffset.FromUnixTimeSeconds(Timestamp);
    }

}

public enum OnebranchApprovalStatus
{
    NotStarted,
    Approved,
    Cancelled,
    Denied
}

public class OnebranchApprovalRequestDetails
{
    public required string ActionName { get; set; }
    public required Dictionary<string, string> InputParameters { get; set; }
    public OnebranchApprovalStatus ApprovalStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public required Uri ApprovalRequestUri { get; set; }
}
