using Agent.Core.Models;

namespace Agent.Runtime.SubAgents.ManagedIdentityMigration;

public sealed record ManagedIdentityMigrationAgentInput(
    ManagedIdentityMigrationInput Input,
    IReadOnlyList<string> ToolSignatures);