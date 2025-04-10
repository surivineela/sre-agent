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
        ToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        ArmHelper armHelper)
    {
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        toolSignatures.Add(toolsRepository.GetSignature(() => ScaleUpAppServicePlanBySku)); 
        toolSignatures.Add(toolsRepository.GetSignature(() => CollectMemoryDumpForApp));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _armHelper = armHelper;
    }

    public async Task<string> StartOrchestration(
        CPUAnalysisInput input,
        ThreadContext context)
    {
        return await _durableTaskClient.ScheduleNewCPUAnalysisAgentInstanceAsync(
            new CPUAnalysisAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                Context: context),
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

        // give the path to the user 
    }
}
