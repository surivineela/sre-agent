using Agent.Core.Models;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.ManagedIdentityMigration;

public sealed record ManagedIdentityMigrationAgentInput(
    ManagedIdentityMigrationInput Input,
    IReadOnlyList<string> ToolSignatures,
    ThreadContext Context);
