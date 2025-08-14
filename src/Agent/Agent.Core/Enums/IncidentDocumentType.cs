namespace Agent.Core.Enums;

/// <summary>
/// Represents the different types of incident documents supported by the system.
/// </summary>
public enum IncidentDocumentType
{
    /// <summary>
    /// ServiceNow incident document
    /// </summary>
    ServiceNowIncident,
    
    /// <summary>
    /// PagerDuty incident document
    /// </summary>
    PagerDutyIncident,
    
    /// <summary>
    /// ICM (Incident and Change Management) incident document
    /// </summary>
    IcmIncident,
    
    /// <summary>
    /// Azure Monitor incident/alert document
    /// </summary>
    AzureMonitorIncident
}
