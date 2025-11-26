// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScorecardHealthState
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}
