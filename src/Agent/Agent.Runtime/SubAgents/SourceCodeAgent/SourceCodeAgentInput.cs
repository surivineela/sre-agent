using Agent.Core.Models;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.SourceCodeAgent;

public sealed record SourceCodeAgentInput(
    [Description("This object contains a list of container apps that need GitHub urls from the user in order to add source control nodes to the graph")]
    SourceCodeInput Input,
    [Description("The list of tools that the agent can use to perform its tasks")]
    IReadOnlyList<string> ToolSignatures,
    string ThreadId);
