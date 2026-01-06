// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Models;

/// <summary>
/// Represents metadata about the agent instance.
/// </summary>
public record AgentMetadata(string Name, string SubscriptionId, string ResourceGroup);
