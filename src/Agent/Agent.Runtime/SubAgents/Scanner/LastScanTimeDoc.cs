// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;

namespace Agent.Runtime.SubAgents.Scanner;

public class LastScanTimeDoc : ICosmosDocument
{
    public static string GetLastScanTimeKey(IncidentManagementType? type)
    {
        return $"LastScanTime{type?.ToString()}";
    }

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public string Id { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string PartitionKey { get; set; } = string.Empty;

    public DateTime LastScanTime { get; set; }
}
