// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Helpers;

/// <summary>
/// Provides methods to validate file paths against path traversal and symlink attacks.
/// </summary>
public static class PathSecurityHelper
{
    /// <summary>
    /// Validates that the resolved file path is within the trusted directory,
    /// checking each path segment for symlinks that could escape the trusted directory.
    /// </summary>
    /// <param name="trustedDirectory">The trusted base directory (assumed safe).</param>
    /// <param name="untrustedFileName">The untrusted filename/relative path from user input.</param>
    /// <returns>The validated full path if safe.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if path traversal or symlink escape is detected.</exception>
    public static string GetSafeFilePath(string trustedDirectory, string untrustedFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(untrustedFileName);

        // Normalize the trusted directory
        string fullBasePath = Path.GetFullPath(trustedDirectory);
        if (!fullBasePath.EndsWith(Path.DirectorySeparatorChar))
        {
            fullBasePath += Path.DirectorySeparatorChar;
        }

        // Combine and normalize the full target path
        string combinedPath = Path.Combine(fullBasePath, untrustedFileName);
        string fullTargetPath = Path.GetFullPath(combinedPath);

        // Check for path traversal: target must be within the base directory
        if (!fullTargetPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Access denied. Path traversal detected for: {untrustedFileName}");
        }

        // Walk from trusted directory to target, checking each segment for symlinks
        string relativePath = fullTargetPath.Substring(fullBasePath.Length);
        string[] segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        string currentPath = fullBasePath.TrimEnd(Path.DirectorySeparatorChar);

        foreach (string segment in segments)
        {
            currentPath = Path.Combine(currentPath, segment);

            if (Directory.Exists(currentPath))
            {
                var dirInfo = new DirectoryInfo(currentPath);
                var resolvedTarget = dirInfo.ResolveLinkTarget(returnFinalTarget: true);
                if (resolvedTarget != null)
                {
                    string resolvedPath = Path.GetFullPath(resolvedTarget.FullName);
                    if (!resolvedPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new UnauthorizedAccessException($"Access denied. Symlink escapes allowed directory.");
                    }
                }
            }
            else if (File.Exists(currentPath))
            {
                var fileInfo = new FileInfo(currentPath);
                var resolvedTarget = fileInfo.ResolveLinkTarget(returnFinalTarget: true);
                if (resolvedTarget != null)
                {
                    string resolvedPath = Path.GetFullPath(resolvedTarget.FullName);
                    if (!resolvedPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new UnauthorizedAccessException($"Access denied. Symlink escapes allowed directory.");
                    }
                }
            }
        }

        return fullTargetPath;
    }

    /// <summary>
    /// Tries to validate that the resolved file path is within the trusted directory,
    /// checking each path segment for symlinks that could escape the trusted directory.
    /// </summary>
    /// <param name="trustedDirectory">The trusted base directory (assumed safe).</param>
    /// <param name="untrustedFileName">The untrusted filename/relative path from user input.</param>
    /// <param name="safePath">The validated full path if safe; null otherwise.</param>
    /// <returns>True if the path is safe; false otherwise.</returns>
    public static bool TryGetSafeFilePath(string trustedDirectory, string untrustedFileName, out string? safePath)
    {
        safePath = null;

        if (string.IsNullOrWhiteSpace(trustedDirectory) || string.IsNullOrWhiteSpace(untrustedFileName))
        {
            return false;
        }

        try
        {
            safePath = GetSafeFilePath(trustedDirectory, untrustedFileName);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
