// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// Extended Message Feedback model for Cosmos DB
public record ReasoningMessageDocument(
    string Id,
    string SubAgentThreadId,
    int Role,
    string? Text,
    FunctionInvocation? FunctionInvocation
) : ICosmosDocument
{
    public string DocumentType => "ReasoningMessage";
    public string PartitionKey => SubAgentThreadId; // Use SubAgentThread Id as partition key to keep reasoning messages with their subagent thread

    // Conversion to/from domain model
    public static ReasoningMessageDocument FromDomainModel(ReasoningMessage reasoningMessage) =>
        new ReasoningMessageDocument(
            Id: reasoningMessage.Id.ToString(),
            SubAgentThreadId: reasoningMessage.SubAgentThreadId.ToString(),
            Role: (int)reasoningMessage.Role,
            Text: reasoningMessage.Text,
            FunctionInvocation: reasoningMessage.FunctionInvocation
        );

    public ReasoningMessage ToDomainModel() =>
        new ReasoningMessage(
            Id: Guid.Parse(Id),
            SubAgentThreadId: Guid.Parse(SubAgentThreadId),
            Role: (ReasoningMessageRoleEnum)Role,
            Text: Text,
            FunctionInvocation: FunctionInvocation
        );
}

