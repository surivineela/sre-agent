// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework.Hooks;

/// <summary>
/// Interface for executing hooks. Different implementations handle different hook types.
/// </summary>
public interface IHookExecutor
{
    /// <summary>
    /// Gets the hook type this executor handles.
    /// </summary>
    HookType SupportedType { get; }

    /// <summary>
    /// Executes a hook and returns the result.
    /// </summary>
    /// <param name="hook">The hook definition to execute.</param>
    /// <param name="context">Context information for the hook.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hook execution result.</returns>
    Task<HookResult> ExecuteAsync(HookDefinition hook, HookContext context, CancellationToken cancellationToken = default);
}
