// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Models;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent storage
/// </summary>
public record ExtendedAgentDocument(
    string Id,
    string Name,
    string Instructions,
    string? HandoffDescription,
    List<string> Handoffs,
    List<string> Tools,
    List<string> Connectors,
    bool AllowParallelToolCalls,
    List<AgentsAsTools> AgentsAsTools,
    int MaxReflectionCount,
    string CriticPromptPath,
    bool CriticOnHandOff,
    string CustomReflectionNote,
    List<string> CommonPrompts,
    float? Temperature,
    string? OutputType,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    string OperationId
) : ICosmosDocument
{
    public string DocumentType => "ExtendedAgent";
    public string PartitionKey => Name; // Use agent name as partition key for easy querying
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;
}
