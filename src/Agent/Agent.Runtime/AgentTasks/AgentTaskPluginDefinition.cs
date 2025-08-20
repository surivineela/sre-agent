// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Agent.Runtime.AgentTasks;

[AgentToolPlugin]
public class AgentTaskPluginDefinition(
    AgentTaskService agentTaskService,
    IAgentTasksRepository agentTasksRepository,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment
) : ContextToolTarget<AgentContext>
{
    [Description("List all active agent tasks for the current thread.")]
    public async Task<List<AgentTaskShort>> ListAllActiveTasks()
    {
        var threadId = Context?.ThreadId ?? throw new InvalidOperationException("ThreadId is not set");
        var tasks = await agentTasksRepository.GetAgentTasksAsync(threadId);
        return tasks.Where(task => task.Status == AgentTaskStatus.InProgress)
                    .Select(task => task.ToShortForm())
                    .ToList();
    }

    [Description(
        """
        Start an incident investigation task. This will create a new task and start the investigation process.
        The task will run in the background and you can check the status of the task by calling the ListAllActiveTasks tool.
        """)]
    public async Task<bool> StartIncidentInvestigationTask(
        [Description("A brief but descriptive title for the investigation task, should be a single sentence and effectively describe the incident.")]
        string title,
        [Description(
            """
            A detailed description of the incident, should provide as much detail as possible about the incident, including the symptoms, impact, and any other relevant information.
            If you are mentioning an azure resource, include the full Azure Resource ID.
            """)]
        string incidentDescription)
    {
        // Check if agent tasks are enabled
        var agentTasksEnabled = configuration.GetValue<bool>("AppSettings:Core:AgentTasksEnabled", false);
        var agentName = AgentNameHelper.GetAgentName(hostEnvironment.IsProduction());

        bool agentNameException = !string.IsNullOrEmpty(agentName) && agentName.ToLowerInvariant().EndsWith("-tasks");

        if (!agentNameException && !agentTasksEnabled)
        {
            return false;
        }

        var threadId = Context?.ThreadId ?? throw new InvalidOperationException("ThreadId is not set");

        var properties = new IncidentInvestigationTaskProperties
        {
            InitialInvestigation = new InitialInvestigationProperties()
            {
                Status = InitialInvestigationStatus.InProgress,
                Summary = string.Empty,
                GatheringContext = new GatheringContextProperties()
                {
                    Status = InitialInvestigationStatus.NotStarted,
                }
            },
            FormingHypothesis = new FormingHypothesisProperties()
            {
                Status = FormingHypothesisStatus.NotStarted
            },
            Conclusion = new ConclusionProperties()
            {
                Summary = string.Empty,
                Title = string.Empty,
            }
        };
        var task = new AgentTask
        {
            Id = Guid.NewGuid(),
            Title = title,
            Type = AgentTaskType.IncidentInvestigation,
            Status = AgentTaskStatus.InProgress,
            ThreadId = threadId,
            Properties = properties,
            InputData = new IncidentInvestigationTaskInputData
            {
                IncidentDescription = incidentDescription
            }
        };

        await agentTaskService.StartAgentTaskAsync(task);
        return true;
    }
}
