using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

public record PagerDutyIncidentFilterAIRootCauseDocument : IncidentFilterAIRootCausePayload, IIncidentFilterAIRootCauseDocument
{
    public PagerDutyIncidentFilterAIRootCauseDocument() : base() { }

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
    public string DocumentType { get; } = IncidentFilterAIRootCauseUtilities.GetDocumentType(IncidentManagementType.PagerDuty);
    public string PartitionKey => DocumentType;
}
