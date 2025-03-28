using Agent.Core.Models;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.CVEAgent;

public sealed record CVEAgentInput(
    [Description("This object contains a list of GitHub repos that need to be scanned for security threats")]
    CVEInput Input,
    [Description("The list of tools that the agent can use to perform its tasks")]
    IReadOnlyList<string> ToolSignatures,
    string ThreadId);
