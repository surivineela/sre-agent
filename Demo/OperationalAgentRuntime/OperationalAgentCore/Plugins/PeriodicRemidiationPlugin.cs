using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OperationalAgentRuntime.Cli.DemoExec.Tasks;
using System.ComponentModel;

public class PeriodicRemediationPlugin
{
    private readonly ITaskClient _taskClient;
    private readonly ILogger<PeriodicRemediationPlugin> _logger;

    public PeriodicRemediationPlugin(ITaskClient taskClient, ILogger<PeriodicRemediationPlugin> logger)
    {
        _taskClient = taskClient;
        _logger = logger;
    }

    [KernelFunction("schedule_periodic_remediation")]
    [Description("Schedules a periodic remediation task with specified CRON expression")]
    public async Task<string> ScheduleRemediation(
        [Description("Resource ID of the target app service")]
        string resourceId,
        [Description("CRON expression for scheduling (e.g. '*/30 * * * *' for every 30 mins), do validate the cron expression is correct")]
        string cronExpression,
        [Description("Description of the remediation action")]
        string actionDescription)
    {
        Console.WriteLine($"[schedule_periodic_remediation] Invoked with resourceId: {resourceId}, cron: {cronExpression}");

        var remediationTask = new RemediationTask
        {
            ResourceId = resourceId,
            CronExpression = cronExpression,
            Description = actionDescription,
            Status = TaskStatus.Created
        };

        await _taskClient.ScheduleRemediationAsync(remediationTask);

        return $"Scheduled periodic remediation for {resourceId}\n" +
               $"Schedule: {cronExpression}\n" +
               $"Action: {actionDescription}";
    }
}
