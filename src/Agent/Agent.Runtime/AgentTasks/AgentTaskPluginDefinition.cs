// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Plugins;

namespace Agent.Runtime.AgentTasks;

[AgentToolPlugin]
public class AgentTaskPluginDefinition(
    AgentTaskService agentTaskService,
    IAgentTasksRepository agentTasksRepository
) : ContextToolTarget<AgentContext>
{
    [Description("List all active agent tasks for the current thread.")]
    [AgentTool(ToolMode.Auto)]
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
    [AgentTool(ToolMode.Auto)]
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
