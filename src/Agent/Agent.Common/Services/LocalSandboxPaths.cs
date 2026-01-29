// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Runtime.InteropServices;

namespace Agent.Common.Services;

/// <summary>
/// Local implementation of ISandboxPaths.
/// Provides sandbox paths for local file system operations.
/// Paths are computed once at construction time (single shared sandbox).
/// </summary>
public class LocalSandboxPaths : ISandboxPaths
{
    private readonly SandboxPaths _paths;

    /// <summary>
    /// Creates a new LocalSandboxPaths instance with default paths based on OS.
    /// Windows: Desktop/SreAgent/TerminalRoot
    /// Linux/macOS: ~/sreagent/terminalRoot
    /// </summary>
    public LocalSandboxPaths()
    {
        var sandboxRoot = ComputeSandboxRoot();
        var codeRefsPath = Path.Combine(sandboxRoot, "codeRefs");
        var tmpPath = Path.Combine(sandboxRoot, "tmp");

        _paths = new SandboxPaths(sandboxRoot, codeRefsPath, tmpPath);

        EnsureDirectoriesExist();
    }

    public SandboxPaths SandboxPaths => _paths;

    /// <inheritdoc />
    public Task<SandboxPaths> GetSandboxPathsAsync() => Task.FromResult(_paths);

    private static string ComputeSandboxRoot()
    {
        string path;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SreAgent", "TerminalRoot");
        }
        else
        {
            // Linux/macOS
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "sreagent", "terminalRoot");
        }

        return path;
    }

    private void EnsureDirectoriesExist()
    {
        if (!Directory.Exists(_paths.SandboxRoot))
        {
            Directory.CreateDirectory(_paths.SandboxRoot);
        }

        if (!Directory.Exists(_paths.CodeRefsPath))
        {
            Directory.CreateDirectory(_paths.CodeRefsPath);
        }

        if (!Directory.Exists(_paths.TmpPath))
        {
            Directory.CreateDirectory(_paths.TmpPath);
        }
    }
}
