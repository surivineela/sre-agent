using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.AppReliabilityAgent;
using Agent.Runtime.SubAgents.CPUAnalysisAgent;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Runtime.MetaAgent;

public class CPUAnalysisPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly CPUAnalysisAgentFactory _cpuAnalysisAgentFactory;
    private readonly ArmHelper _armHelper;

    public ThreadContext? Context { get; set; }

    public CPUAnalysisPlugin(
        DurableTaskClient durableTaskClient,
        CPUAnalysisAgentFactory cpuAnalysisAgentFactory,
        ArmHelper armHelper)
    {
        _durableTaskClient = durableTaskClient;
        _cpuAnalysisAgentFactory = cpuAnalysisAgentFactory;
        _armHelper = armHelper;
    }

    [KernelFunction("list_cpu_analysis_practice_workflow")]
    [Description("List the information of started cpu analysis workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<CPUAnalysisInput>>> ListCPUAnalysisWorkflows()
    {
        var list = new List<WorkflowMetadata<CPUAnalysisInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _cpuAnalysisAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<CPUAnalysisInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [KernelFunction("start_cpu_analysis_workflow")]
    [Description("Start the workflow to resolve multiple apps with high CPU.")]
    public async Task<string> StartCPUAnalysisAgent(
        [Description("The list of apps to be modified")] CPUAnalysisInput input,
        ThreadContext context)
    {
        var instanceId = await _cpuAnalysisAgentFactory.StartOrchestration(input, context);
        return $"A workflow has been started to adopt best reliability practice, the workflow instance id is: {instanceId}";
    }

    [KernelFunction("scale_up_app_service_plan_by_sku")]
    [Description("Scale up the app service plan by sku")]
    public async Task<string> ScaleUpAppServicePlanBySku(
    [Description("resourceId of the app")] string resourceId)
    {
        var currentSku = await _armHelper.GetCurrentSkuAsync(resourceId);
        var nextSku = ArmHelper.GetNextSku(currentSku);
        var success = await _armHelper.ScaleUpAppServicePlanByNameAsync(resourceId, nextSku);
        if (success)
        {
            return $"The app service plan for {resourceId} has been scaled up to {nextSku.Name}";
        }
        return $"There was an issue scaling up your app service plan";
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
       
       var response =  await _armHelper.CreateAutoScaleSetting(
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


        if(String.IsNullOrEmpty(response))
        {
            return "There was an issue creating the auto-scaling configuration.";
        }

        return "Auto-scaling configuration has been successfully applied. ";
    }

    // -trigger deployment swap
    // -collects deployment activities through custom activity logs
}

