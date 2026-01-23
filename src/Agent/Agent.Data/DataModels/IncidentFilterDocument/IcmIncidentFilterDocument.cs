// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

public record IcmIncidentFilterDocument : IcmIncidentFilterDocumentPayload, IIncidentFilterDocument
{
    public IcmIncidentFilterDocument(
    ) : base()
    { }
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public bool IsDeleted { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string DocumentType { get; } = IncidentFilterDocumentUtilities.GetDocumentTypeName(IncidentManagementType.Icm);
    public string PartitionKey => DocumentType;
}

public record IcmIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    public IcmIncidentFilterDocumentPayload() : base()
    { }
    public string MonitorId { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// List of trigger events that this filter responds to.
    /// Defaults to [IncidentCreatedOrTransferred] for backward compatibility.
    /// ICM provider only.
    /// </summary>
    public List<IcmIncidentTriggerEvent> Triggers { get; set; } = new() { IcmIncidentTriggerEvent.IncidentCreatedOrTransferred };

    /// <summary>
    /// List of handling agent IDs for multi-agent parallel processing (Phase 2).
    /// Each agent processes incidents on its own thread.
    /// Takes precedence over base HandlingAgent if populated.
    /// </summary>
    public List<string> HandlingAgents { get; set; } = new();

    /// <summary>
    /// Checks if a specific trigger event is enabled for this filter.
    /// Falls back to IncidentCreatedOrTransferred if Triggers is empty/null.
    /// </summary>
    public bool IsTriggerEnabled(IcmIncidentTriggerEvent triggerEvent)
    {
        if (Triggers == null || Triggers.Count == 0)
        {
            // Default behavior for backward compatibility
            return triggerEvent == IcmIncidentTriggerEvent.IncidentCreatedOrTransferred;
        }
        return Triggers.Contains(triggerEvent);
    }

    /// <summary>
    /// Gets the effective list of handling agents (backward compatible).
    /// Priority: HandlingAgents (if any) → HandlingAgent (from base) → empty list.
    /// </summary>
    public List<string> GetEffectiveHandlingAgents()
    {
        // Phase 2: Prefer HandlingAgents list if populated
        if (HandlingAgents?.Count > 0)
            return HandlingAgents;

        // Backward compatibility: Fall back to single HandlingAgent from base class
        if (!string.IsNullOrEmpty(HandlingAgent))
            return new List<string> { HandlingAgent };

        return new List<string>();
    }
}
