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

    private async Task ProcessTaskAsync(QuotaIncidentState task, IIcmPlugin icmPlugin, IQuotaAgentService quotaAgentService)
    {
        //
        var newDiscussions = await icmPlugin.GetDiscussionEntries(task.Incident.IncidentId, new DateTimeOffset(task.LastUpdateTimestamp!.Value));
        if (newDiscussions is null)
        {
            _logger.LogInformation($"[gpu_quota_icm_background_service] No discussions found for incident {task.Incident.IncidentId}");
            return;
        }

        if (newDiscussions.Count != 0)
        {
            _logger.LogInformation($"[gpu_quota_icm_background_service] LastestDiscussionTimestampUtc is null all discussions are new for incident {task.Incident.IncidentId}");

            await quotaAgentService.Process(task, newDiscussions.Select(d => new ConversationEntry(d.ChangedBy, ConversationSource.Icm, d.Text)).ToList());
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tasks = await _taskStorageService.GetAllTasksAsync();
                foreach (var (incidentId, task) in tasks)
                {
                    _logger.LogInformation($"[gpu_quota_icm_background_service] Processing task: {incidentId}");

                    // Process the task
                    await ProcessTaskAsync(task, icmPlugin, quotaAgentService);
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