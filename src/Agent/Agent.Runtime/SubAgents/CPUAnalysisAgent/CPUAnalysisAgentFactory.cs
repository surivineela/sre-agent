using Agent.Core.Models;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Agent.Plugins.Implementation;
using Agent.Runtime.MetaAgent;
using Agent.Core.Helpers;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.CPUAnalysisAgent;


// [Export]
public sealed class CPUAnalysisAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ArmHelper _armHelper;

    public const string OrchestrationInstanceIdPrefix = nameof(CPUAnalysisAgent);

    public CPUAnalysisAgentFactory(
        IMetricsPlugin metricsPlugin,
        IApprovalPlugin approvalPlugin,
        IGithubIssuePlugin githubPlugin,
        ToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        ArmHelper armHelper)
    {
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

        toolSignatures.Add(ToolsRepository.GetSignature(() => ScaleUpAppServicePlanBySku));
        toolSignatures.Add(ToolsRepository.GetSignature(() => CollectMemoryDumpForApp));
        toolSignatures.Add(ToolsRepository.GetSignature(() => AutoScaleApp));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _armHelper = armHelper;
    }

    public async Task<string> StartOrchestration(
        CPUAnalysisInput input,
        Guid threadId)
    {
        return await _durableTaskClient.ScheduleNewCPUAnalysisAgentInstanceAsync(
            new CPUAnalysisAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public CPUAnalysisInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<CPUAnalysisAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }

    [KernelFunction("scale_up_app_service_plan_by_sku")]
    [Description("Scale up the app service plan by sku")]
    public async Task<string> ScaleUpAppServicePlanBySku(
    [Description("resourceId of the app")] string resourceId)
    {
        var appServicePlanId = await _armHelper.GetAppServicePlanNameAsync(resourceId);
        var currentSku = await _armHelper.GetCurrentSkuAsync(appServicePlanId);
        var nextSku = ArmHelper.GetNextSku(currentSku);
        var success = await _armHelper.ScaleUpAppServicePlanByNameAsync(resourceId, nextSku);
        if (success)
        {
            return $"The app service plan for {resourceId} has been scaled up to {nextSku.Name}";
        }
        return $"There was an issue scaling up your app service plan";
    }

    [KernelFunction("collect_memory_dump_for_app")]
    [Description("Collect Memory Dump for App")]
    public async Task<string> CollectMemoryDumpForApp(
    [Description("resourceId of the app")] string resourceId)
    {
        // call arm helper
        var responseString = await _armHelper.TakeMemoryDumpAsync(resourceId);

        if (String.IsNullOrEmpty(responseString))
        {
            return $"There was an issue collecting the memory dump for {resourceId}";
        }
        return $"The memory dump for {resourceId} has been collected, the link is: {responseString}";
    }

    [KernelFunction("autoscale_app_service")]
    [Description("Create AutoScale Settings for App to Autoscale App")]
    public async Task<string> AutoScaleApp(
        [Description("resourceId of the app")] string subscriptionId,
        string resourceGroupName,
        string autoScaleSettingName,
        string location,
        string resourceId,
        int minCount,
        int maxCount,
        int targetCount,
        string profileName = "DefaultProfile",
        string metricName = "CpuPercentage",
        string operatorProperty = "GreaterThan",
        double threshold = 70.0,
        string timeAggregation = "Average",
        string statistic = "Average",
        string timeGrain = "PT1M",
        string timeWindow = "PT5M",
        string scaleDirection = "Increase",
        string scaleType = "ChangeCount",
        string scaleValue = "1",
        string cooldown = "PT5M")
    {

        var response = await _armHelper.CreateAutoScaleSetting(
             subscriptionId,
             resourceGroupName,
             autoScaleSettingName,
             location,
             resourceId,
             minCount,
             maxCount,
             targetCount, // Argument 8: Changed from string to int
             profileName,
             metricName,
             operatorProperty,
             threshold,
             timeAggregation,
             statistic,
             timeGrain,
             timeWindow,
             scaleDirection,
             scaleType,
             scaleValue,
             cooldown
         );


        if (String.IsNullOrEmpty(response))
        {
            return "There was an issue creating the auto-scaling configuration.";
        }

        return "Auto-scaling configuration has been successfully applied. ";
    }
}
