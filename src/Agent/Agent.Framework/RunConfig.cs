// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Framework;

public class RunConfig
{
    public required IChatClient ChatClient { get; set; }
    public required ILoggerFactory LoggerFactory { get; set; }
    public bool TracingDisabled { get; set; }
    public bool TraceIncludeSensitiveData { get; set; } = true;
    public string WorkflowName { get; set; } = "Agent workflow";
    public string? TraceId { get; set; }
    public string? GroupId { get; set; }
    public Guid ThreadId { get; set; } = Guid.Empty;
    public Dictionary<string, object>? TraceMetadata { get; set; }
    public bool EnableDebugOutput { get; set; }
}
