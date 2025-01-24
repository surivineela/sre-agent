using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OperationalAgentRuntime.Cli.DemoExec;
using OperationalAgentRuntime.Cli.DemoExec.Helpers;
using OperationalAgentRuntime.Cli.DemoExec.Models;
using OperationalAgentRuntime.Cli.DemoExec.Tasks;

public class RemediationWorker : BackgroundService
{
    private readonly ITaskClient _taskClient;
    private readonly ILogger<RemediationWorker> _logger;
    private readonly Kernel _kernel;
    private static readonly HttpClient _httpClient = new HttpClient();

    // Check for progress updates every 10 minutes
    private readonly TimeSpan _progressNotificationInterval = TimeSpan.FromMinutes(10);

    public RemediationWorker(
        ITaskClient taskClient,
        ILogger<RemediationWorker> logger,
        Kernel kernel)
    {
        _taskClient = taskClient;
        _logger = logger;
        _kernel = kernel;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tasks = await _taskClient.GetPendingRemediationsAsync();
                foreach (var task in tasks)
                {
                    Console.WriteLine($"[remediation_worker] Pending Remediation detected: {task.Id}");

                    if (task.LastExecuted == null)
                    {
                        // Optionally notify that the remediation is fully completed
                        await TeamsNotificationHelper.SendTeamsNotificationAsync(
                            _httpClient,
                            $"**I have just started the remediation for app '{task.ResourceId}'!"
                        );
                    }

                    // evauates using the cron expression
                    if (ShouldExecute(task.CronExpression, task.LastExecuted))
                    {
                        bool isHealthy = await ExecuteRemediation(task);

                        if (isHealthy)
                        {
                            // The app is healthy now
                            Console.WriteLine($"[remediation_worker] App is healthy. Deleting task {task.Id}.");

                            // Delete the task from the store
                            await _taskClient.DeleteRemediationAsync(task.Id);
                        }
                        else
                        {
                            // Not healthy yet, so update the task with latest status
                            Console.WriteLine($"[remediation_worker] App is NOT healthy. Updating task {task.Id}.");
                            task.LastExecuted = DateTime.UtcNow;
                            task.Description = $"Last monitoring indicates the app is still not healthy at {DateTime.UtcNow}.";
                            await _taskClient.UpdateRemediationAsync(task);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[remediation_worker] Cron not satisfied for task {task.Id}");
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in remediation worker");
            }
        }
    }

    private bool ShouldExecute(string cronExpression, DateTime? lastExecuted)
    {
        var expression = NCrontab.CrontabSchedule.Parse(cronExpression);
        var nextOccurrence = expression.GetNextOccurrence(lastExecuted ?? DateTime.MinValue);
        return nextOccurrence <= DateTime.UtcNow;
    }

    /// <summary>
    /// Executes the remediation for a task and returns true if the AI signals the app is healthy (<end>), false otherwise.
    /// Additionally, we send periodic "progress" updates to Teams that include the AI's reasoning.
    /// </summary>
    private async Task<bool> ExecuteRemediation(RemediationTask task)
    {
        // Track action
        TrackedActionHelper.TrackAction(
            "RemediationWorker",
            task.ResourceId,
            ActionType.Remediation,
            task.Description,
            new Dictionary<string, string> { ["CronExpression"] = task.CronExpression }
        );

        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(RemediationAgent.SystemMessage);

        // If this is the first time, or if enough time has passed, notify the user
        if (task.LastProgressNotification == null)
        {
            // First time: let them know we're starting remediation
            await TeamsNotificationHelper.SendTeamsNotificationAsync(
                _httpClient,
                $"Starting remediation for **Task {task.Id}**. Time: {DateTime.UtcNow}"
            );

            // Update LastProgressNotification
            task.LastProgressNotification = DateTime.UtcNow;
            await _taskClient.UpdateRemediationAsync(task);
        }

        while (true)
        {
            // Check if it's time for a periodic progress notification
            if (task.LastProgressNotification == null
                || (DateTime.UtcNow - task.LastProgressNotification) >= _progressNotificationInterval)
            {
                await TeamsNotificationHelper.SendTeamsNotificationAsync(
                    _httpClient,
                    $"**Task {task.Id}** is still in progress. " +
                    $"We are continuing to remediate. Last attempt at {DateTime.UtcNow}."
                );

                task.LastProgressNotification = DateTime.UtcNow;
                await _taskClient.UpdateRemediationAsync(task);
            }

            // Get the next response from the AI
            var result = await chatCompletionService.GetChatMessageContentAsync(
                history,
                executionSettings: new()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                },
                kernel: _kernel);

            Console.WriteLine("Assistant > " + result);

            // Check if the AI message indicates the app is healthy
            if (result.Content?.Contains("<end>") == true)
            {
                // Provide a more verbose reason in the Teams message
                await TeamsNotificationHelper.SendTeamsNotificationAsync(
                    _httpClient,
                    $"**Good news! I think the app is now healthy. I'll end the remediation, let me know if you want me to run it again." +
                    $"**Reason**: {result.Content}"
                );
                Console.WriteLine("Assistant > The app is healthy now. Ending remediation.");
                return true;
            }

            // Check for signals that the conversation ended but not necessarily healthy
            if (result.Metadata?.TryGetValue("tool_calls", out var toolCalls) == true &&
                toolCalls.ToString().Contains("end_conversation"))
            {
                // If we want to keep looping or stop here is up to your logic.
                // We'll continue for now in case the next iteration clarifies more info.
                Console.WriteLine("Assistant > The AI signaled it will end the conversation. Will continue for next cycle.");
            }

            if (task.LastProgressNotification == null
               || (DateTime.UtcNow - task.LastProgressNotification) >= _progressNotificationInterval)
            {
                task.LastProgressNotification = DateTime.UtcNow;
                // If we got here, the AI’s latest response indicates “still not healthy”
                // We might send a more verbose message about why it thinks it’s not healthy:
                await TeamsNotificationHelper.SendTeamsNotificationAsync(
                 _httpClient,
                  $"**I have been running a remediation for app '{task.ResourceId}'**: My analysis indicates the app is still not healthy. " +
                  $"**Reason**: {result.Content}"
                );
            }

            // Update the chat history with the new AI message
            history.AddMessage(result.Role, result.Content ?? string.Empty);

            return false;
        }
    }
}
