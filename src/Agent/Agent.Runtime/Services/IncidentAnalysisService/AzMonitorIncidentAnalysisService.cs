// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Data.DataModels.IncidentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class AzMonitorIncidentAnalysisService : IncidentAnalysisServiceBase<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload, AlertItem>
{
    public AzMonitorIncidentAnalysisService(
        IChatClient client,
        IIncidentManagementService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload> incidentManagementService,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        CoreSettings coreSettings,
        ArmHelper armHelper,
        ILogger<IIncidentAnalysisService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload, AlertItem>> logger)
        : base(client, incidentManagementService, repository, inboundCommunicationService, coreSettings, armHelper, logger)
    {
    }

    public override async Task<AzMonitorAlertDocument> AnalyzeIncident(AzMonitorAlertDocument incidentDocument, AlertItem incident)
    {
        // For now, return the incident document as-is
        // This can be enhanced with AI analysis capabilities later
        await Task.CompletedTask;
        return incidentDocument;
    }

    protected override bool IsMitigatedByAgent(AzMonitorAlertDocument azMonitorIncident)
    {
        string status;

        status = azMonitorIncident.Status.ToLower();
        var isMitigatedByAgent = status == "resolved" || status == "closed";
        return isMitigatedByAgent;
    }

    protected override DateTime? IncidentMitigatedAt(AzMonitorAlertDocument azMonitorIncident)
    {
        DateTime? mitigatedAt = null;
        // AzMonitor incidents don't have a specific "resolved at" field like PagerDuty
        // We'll use the UpdatedAt if the incident is resolved
        if (azMonitorIncident.Status.Equals("resolved", StringComparison.CurrentCultureIgnoreCase) || azMonitorIncident.Status.Equals("closed", StringComparison.CurrentCultureIgnoreCase))
        {
            mitigatedAt = azMonitorIncident.UpdatedAt;
        }
        return mitigatedAt;
    }
}
