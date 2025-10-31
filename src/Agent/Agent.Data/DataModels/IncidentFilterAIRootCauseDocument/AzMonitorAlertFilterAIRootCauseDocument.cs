using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

public record AzMonitorAlertFilterAIRootCauseDocument : IncidentFilterAIRootCausePayload, IIncidentFilterAIRootCauseDocument
{
    public AzMonitorAlertFilterAIRootCauseDocument() : base() { }

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
    public string DocumentType { get; } = IncidentFilterAIRootCauseUtilities.GetDocumentType(IncidentManagementType.AzMonitor);
    public string PartitionKey => DocumentType;
}
