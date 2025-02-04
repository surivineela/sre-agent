using Agents.Core.Helpers;
using Agents.Core.Models;
using System.Collections.Concurrent;

namespace Agents.Core;

// TODO: figure out how to DI these into DiagnosePlugin
public static class GlobalStatic
{
    public static TeamsConnector TeamsConnector;

    public static ConcurrentDictionary<ApprovalDescriptor, ApprovalStatus> ApprovalStatus { get; } = new();
}
