using Agent.Core.Models;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;
using Agent.Runtime.MetaAgent;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Agent.Core.Helpers;
using OperationalAgentCore;

namespace Agent.Runtime.SubAgents.AppCodeAnalysisAgent;


// [Export]
public sealed class AppCodeAnalysisAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private IAppInsightsPlugin _appInsightsPlugin;
    private ArmHelper _armHelper;

    public const string OrchestrationInstanceIdPrefix = nameof(AppCodeAnalysisAgent);

    public AppCodeAnalysisAgentFactory(
        IAppInsightsPlugin appInsightsPlugin,
        IApprovalPlugin approvalPlugin,
        IMetricsPlugin metricsPlugin,
        IGithubIssuePlugin githubPlugin,
        DurableTaskClient durableTaskClient,
        ArmHelper armHelper)
    {
        //change tool signatures 
        var toolSignatures = new List<string>();

        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        var githubPluginDefinition = new GitHubIssuePluginDefinition(githubPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => githubPluginDefinition.CreateGithubIssue));

        toolSignatures.Add(ToolsRepository.GetSignature(() => GetCallStackForApp));
        toolSignatures.Add(ToolsRepository.GetSignature(() => PerformDeploymentSwapForApp));
        toolSignatures.Add(ToolsRepository.GetSignature(() => GetDeploymentActivity));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _appInsightsPlugin = appInsightsPlugin;
        _armHelper = armHelper;
    }


    public async Task<string> StartOrchestration(
        AppCodeAnalysisInput input,
        ThreadContext context)
    {
        return await _durableTaskClient.ScheduleNewAppCodeAnalysisAgentInstanceAsync(
            new AppCodeAnalysisAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                Context: context),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public AppCodeAnalysisInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<AppCodeAnalysisAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }

    [KernelFunction("get_app_stack_trace")]
    [Description("This function attempts to retrieve the stack traces for a user's particular app")]
    public async Task<string> GetCallStackForApp(
    [Description("resourceId of the app")] string resourceId, DateTime? startTime, DateTime? endTime)
    {
        if (startTime == null)
        {
            startTime = DateTime.UtcNow.AddHours(-3);
        }
        if (endTime == null)
        {
            endTime = DateTime.UtcNow;
        }

        string stackTraceQuery = $@"exceptions
        | where timestamp >= datetime({startTime.ToString()}) and timestamp <= datetime({endTime.ToString()})
        | where operation_Name contains ""{resourceId}""
        | project details[0].message
        | take 10";
        var stackTrace = await _appInsightsPlugin.ExecuteAppInsightsQuery(stackTraceQuery);
        return stackTrace;
    }

    [KernelFunction("perform_deployment_swap_for_app")]
    [Description("Performs a Deployment Swap for the specified app")]
    public async Task<string> PerformDeploymentSwapForApp(
       [Description("resourceId for app")] string resourceId,
       [Description("preserve VNET setting for deployment swap")] bool preserveVNet,
        [Description("source deployment slot to swap from")] string sourceSlot = "production",
         [Description("target deployment slot to swap to")] string targetSlot = "staging")
    {
        var success = await _armHelper.SwapAppServiceSlotsAsync(resourceId, preserveVNet, sourceSlot, targetSlot);

        if (success)
        {
            return $"The deployment swap operation has successfully completed. Swap operations were performed from {sourceSlot} to {targetSlot}";
        }
        return "There was an issue performing the swap. The deployment swap operation(s) was unsuccessful.";
    }

    [KernelFunction("get_deployment_activity_for_app")]
    [Description("Gets Deployment Activities on the specified app")]
    public async Task<string> GetDeploymentActivity(
    [Description("resourceId for app")] string resourceId)
    {
        try
        {
            // Parse subscriptionId and resourceGroupName from resourceId  
            var segments = resourceId.Split('/');
            if (segments.Length < 5)
            {
                throw new ArgumentException("Invalid resource ID format.");
            }

            string subscriptionId = segments[2];
            string resourceGroupName = segments[4];

            // Call the method to get deployment activities  
            var (deployments, swaps) = await _armHelper.GetDeploymentActivity(subscriptionId, resourceGroupName, resourceId);

            // Initialize result string  
            string result = "Deployment Activities:\n";

            if (deployments != null)
            {
                foreach (var deployment in deployments)
                {
                    result += $"Deployment: {deployment.OperationName}, Success: {deployment.IsSuccessful}, Timestamp: {deployment.Timestamp}\n";
                }
            }
            else
            {
                result += "No deployment activities found.\n";
            }

            result += "\nSwap Activities:\n";

            if (swaps != null)
            {
                foreach (var swap in swaps)
                {
                    result += $"Swap: {swap.OperationName}, Success: {swap.IsSuccessful}, Timestamp: {swap.Timestamp}\n";
                }
            }
            else
            {
                result += "No swap activities found.\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            // Handle any exceptions and return an error message  
            return $"An error occurred: {ex.Message}";
        }
    }
}
