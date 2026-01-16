// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Provides ambient context strings to inject into agent prompts.
/// Replicates Copilot Chat's context injection pattern.
/// </summary>
public interface IAmbientContextProvider
{
    /// <summary>
    /// Gets whether ambient context injection is enabled.
    /// When false, no context will be injected into prompts.
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// Gets instruction context for the FIRST SYSTEM MESSAGE (after main system prompt).
    /// Includes: copilot instruction files as attachments + instruction file list.
    /// This is static for the entire conversation.
    /// </summary>
    Task<string> GetInstructionsContextAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets environment context for the FIRST USER MESSAGE.
    /// Includes: OS info, workspace folder structure.
    /// This is static for the entire conversation.
    /// </summary>
    Task<string> GetEnvironmentContextAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets dynamic context for the 2ND LAST USER MESSAGE (before user query).
    /// Includes: git repos, current date, terminal states, tool reminders, plan reminders.
    /// This is dynamic and updated with each request.
    /// The plugin already has access to tools, todo state, and terminal state internally.
    /// </summary>
    Task<string> GetPreUserQueryContextAsync(CancellationToken ct = default);
}
