// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Hooks;
using Agent.Framework.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Framework;

public class RunConfig
{
    public required IChatClient ChatClient { get; set; }
    public required ILoggerFactory LoggerFactory { get; set; }
    public required ISkillRegistry SkillRegistry { get; set; }
    public bool TracingDisabled { get; set; }
    public bool TraceIncludeSensitiveData { get; set; } = true;
    public string WorkflowName { get; set; } = "Agent workflow";
    public string? TraceId { get; set; }
    public string? GroupId { get; set; }
    public Guid ThreadId { get; set; } = Guid.Empty;
    public Dictionary<string, object>? TraceMetadata { get; set; }
    public bool EnableDebugOutput { get; set; } = true;
    public int MaxActiveSkills { get; set; } = 5;
    public bool EnablePartialToolOutput { get; set; } = false;

    /// <summary>
    /// Ambient context provider for injecting environment, workspace,
    /// and instruction file context into the agent prompt.
    /// Check the Enabled property to determine if context injection is active.
    /// </summary>
    public required IAmbientContextProvider AmbientContextProvider { get; set; }

    /// <summary>
    /// Optional chat client provider for accessing specialized models like ReasoningFast.
    /// Used for reasoning title generation for Anthropic models.
    /// </summary>
    public IChatClientProvider? ChatClientProvider { get; set; }

    /// <summary>
    /// Maximum number of times stop hooks can reject stopping before forcing stop.
    /// Default is 3 to prevent infinite loops.
    /// </summary>
    public int MaxStopHookRejections { get; set; } = 3;

    /// <summary>
    /// Hook manager for executing agent hooks.
    /// If <c>null</c>, hooks are disabled and no hook callbacks will run.
    /// <para>
    /// Note: The main production code path that creates <see cref="RunConfig"/> instances
    /// does not automatically populate this property. Callers that require hooks must
    /// explicitly inject a <see cref="HookManager"/> instance (for example, from DI)
    /// when constructing and configuring <see cref="RunConfig"/>.
    /// </para>
    /// </summary>
    public HookManager? HookManager { get; set; }
}
