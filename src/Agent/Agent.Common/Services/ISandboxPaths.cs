// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Common.Services;

/// <summary>
/// Contains sandbox directory paths.
/// </summary>
/// <param name="SandboxRoot">Root directory for sandbox operations.</param>
/// <param name="CodeRefsPath">Path to code repositories directory.</param>
/// <param name="TmpPath">Path to temporary files directory.</param>
public record SandboxPaths(string SandboxRoot, string CodeRefsPath, string TmpPath);

/// <summary>
/// Interface for accessing sandbox directory paths.
/// Provides abstraction for both local and remote (per-thread) sandbox paths.
/// </summary>
public interface ISandboxPaths
{
    /// <summary>
    /// Gets the sandbox paths for the current context.
    /// For local mode, returns shared paths.
    /// For ADC mode, returns per-thread paths (lazily initialized via remote call).
    /// </summary>
    Task<SandboxPaths> GetSandboxPathsAsync();
}
