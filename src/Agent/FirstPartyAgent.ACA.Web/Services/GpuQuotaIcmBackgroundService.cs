// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Models;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.ACA.Web.Services;

public class GpuQuotaIcmBackgroundService : BackgroundService
{
    private readonly ILogger<GpuQuotaIcmBackgroundService> _logger;
    private readonly ITaskStorageService _taskStorageService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(1);

    public GpuQuotaIcmBackgroundService(ILogger<GpuQuotaIcmBackgroundService> logger, ITaskStorageService taskStorageService, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _taskStorageService = taskStorageService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    private async Task ProcessTaskAsync(QuotaIncidentState task, IIcmPlugin icmPlugin, IQuotaAgentService quotaAgentService, IContainerAppsPlugin containerAppsPlugin, ITaskStorageService taskStorageService)
    {
        if (!task.LastUpdateTimestamp.HasValue)
        {
            _logger.LogWarning($"[gpu_quota_icm_background_service] LastUpdateTimestamp is null for incident {task.Incident.IncidentId}");
            return;
        }
        var newDiscussions = await icmPlugin.GetDiscussionEntries(task.Incident.IncidentId, new DateTimeOffset(task.LastUpdateTimestamp!.Value));
        if (newDiscussions is null)
        {
            _logger.LogInformation($"[gpu_quota_icm_background_service] No discussions found for incident {task.Incident.IncidentId}");
            return;
        }

        if (newDiscussions.Count != 0)
        {
            _logger.LogInformation($"[gpu_quota_icm_background_service] LastestDiscussionTimestampUtc is {task.LastUpdateTimestamp!.Value}, {newDiscussions.Count} discussions are new for incident {task.Incident.IncidentId}");

            var conversationEntries = new List<ConversationEntry>();

            foreach (var discussion in newDiscussions)
            {
                var changedBy = discussion.ChangedBy;
                var text = discussion.Text;
                var cause = discussion.Cause;

                if (text.Equals("Acknowledging incident", StringComparison.OrdinalIgnoreCase))
                {
                    var message = $"[From ICM discussion] Incident {task.Incident.IncidentId} is acknowledged by {changedBy}.";
                    await containerAppsPlugin.ReplyTeamsDiscussionAsync(task.Incident.IncidentId, task.ConversationContext.TeamsMessageId, message);
                    _logger.LogInformation($"[gpu_quota_icm_background_service] Incident {task.Incident.IncidentId} is acknowledged by {changedBy}. Teams message synced.");
                }
                else if (cause.Equals("mitigated", StringComparison.OrdinalIgnoreCase))
                {
                    var message = $"[From ICM discussion] Incident {task.Incident.IncidentId} is mitigated by {changedBy}.";
                    await containerAppsPlugin.ReplyTeamsDiscussionAsync(task.Incident.IncidentId, task.ConversationContext.TeamsMessageId, message);
                    _logger.LogInformation($"[gpu_quota_icm_background_service] Incident {task.Incident.IncidentId} is mitigated by {changedBy}. Teams message synced.");
                }
                else if (cause.Equals("resolved", StringComparison.OrdinalIgnoreCase))
                {
                    await _taskStorageService.RemoveTaskAsync(task.Incident.IncidentId);
                    var message = $"[From ICM discussion] Incident {task.Incident.IncidentId} is resolved by {changedBy}. The agent will now stop proactive tracking on this case.";
                    await containerAppsPlugin.ReplyTeamsDiscussionAsync(task.Incident.IncidentId, task.ConversationContext.TeamsMessageId, message);
                    _logger.LogInformation($"[gpu_quota_icm_background_service] Incident {task.Incident.IncidentId} is resolved, stop tracking by agent.");
                    return;
                }
                else {
                    _logger.LogInformation($"[gpu_quota_icm_background_service] Incident {task.Incident.IncidentId} discussion updated by {changedBy}: {text}");
                    conversationEntries.Add(new ConversationEntry(changedBy, ConversationSource.Icm, text));
                }
            }

            if (conversationEntries.Count > 0)
            {
                _logger.LogInformation($"[gpu_quota_icm_background_service] processing new discussions for incident {task.Incident.IncidentId}");
                await quotaAgentService.Process(task, conversationEntries);
            }
            else
            {
                // no need to process the quota request when the icm is manually acknowledged or mitigated. but we still need to update the last update timestamp. 
                _logger.LogInformation($"[gpu_quota_icm_background_service] No new discussions need to be processed by the agent, update LastUpdateTimestamp for incident {task.Incident.IncidentId}");
                task.LastUpdateTimestamp = DateTime.UtcNow;
                await _taskStorageService.UpdateTaskAsync(task);
            }

            _logger.LogInformation($"[gpu_quota_icm_background_service] finished processing incident {task.Incident.IncidentId} by agent");
            return;
        }
        else
        {
            _logger.LogInformation($"[gpu_quota_icm_background_service] No new discussions found for incident {task.Incident.IncidentId}");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var icmPlugin = scope.ServiceProvider.GetRequiredService<IIcmPlugin>();
        var quotaAgentService = scope.ServiceProvider.GetRequiredService<IQuotaAgentService>();
        var taskStorageService = scope.ServiceProvider.GetRequiredService<ITaskStorageService>();
        var containerAppsPlugin = scope.ServiceProvider.GetRequiredService<IContainerAppsPlugin>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tasks = await _taskStorageService.GetAllTasksAsync();
                foreach (var (incidentId, task) in tasks)
                {
                    _logger.LogInformation($"[gpu_quota_icm_background_service] Processing task: {incidentId}");

                    // Process the task
                    await ProcessTaskAsync(task, icmPlugin, quotaAgentService, containerAppsPlugin, taskStorageService);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing gpu quota tasks.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}