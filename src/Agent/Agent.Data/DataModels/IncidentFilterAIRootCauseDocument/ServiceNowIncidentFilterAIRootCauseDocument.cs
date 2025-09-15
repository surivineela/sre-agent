using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

public record ServiceNowIncidentFilterAIRootCauseDocument : IncidentFilterAIRootCausePayload, IIncidentFilterAIRootCauseDocument
{
    public ServiceNowIncidentFilterAIRootCauseDocument() : base() { }

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
    public string DocumentType { get; } = IncidentFilterAIRootCauseUtilities.GetDocumentType(IncidentManagementType.ServiceNow);
    public string PartitionKey => DocumentType;
}
