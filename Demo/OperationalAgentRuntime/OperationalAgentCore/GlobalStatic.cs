using Microsoft.SemanticKernel;
using System.Collections.Concurrent;

namespace OperationalAgentCore;

// TODO: figure out how to DI these into DiagnosePlugin
public static class GlobalStatic
{
    public static TeamsConnector TeamsConnector;

    public static ConcurrentDictionary<ApprovalDescriptor, ApprovalStatus> ApprovalStatus { get; } = new();
}
