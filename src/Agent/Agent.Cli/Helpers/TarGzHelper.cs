// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Formats.Tar;
using System.IO.Compression;

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for creating and extracting tar.gz archives.
/// </summary>
public static class TarGzHelper
{
    /// <summary>
    /// Creates a tar.gz archive from a source directory.
    /// </summary>
    /// <param name="sourceDir">The source directory to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tar.gz archive as a byte array.</returns>
    public static async Task<byte[]> CreateTarGzAsync(string sourceDir, CancellationToken ct = default)
    {
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
        await using (var tarWriter = new TarWriter(gzipStream, leaveOpen: true))
        {
            foreach (var filePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(sourceDir, filePath).Replace('\\', '/');
                await tarWriter.WriteEntryAsync(filePath, relativePath, ct);
            }
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Creates a tar.gz archive from selected subdirectories of a source directory.
    /// </summary>
    /// <param name="sourceDir">The source directory containing subdirectories to archive.</param>
    /// <param name="includeFolders">List of folder names to include (relative to sourceDir).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tar.gz archive as a byte array.</returns>
    public static async Task<byte[]> CreateTarGzFromFoldersAsync(
        string sourceDir,
        IEnumerable<string> includeFolders,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
        await using (var tarWriter = new TarWriter(gzipStream, leaveOpen: true))
        {
            foreach (var folder in includeFolders)
            {
                var folderPath = Path.Combine(sourceDir, folder);
                if (!Directory.Exists(folderPath))
                {
                    continue;
                }

                foreach (var filePath in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(sourceDir, filePath).Replace('\\', '/');
                    await tarWriter.WriteEntryAsync(filePath, relativePath, ct);
                }
            }
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Extracts a tar.gz archive to a destination directory.
    /// </summary>
    /// <param name="tarGzStream">The tar.gz stream to extract.</param>
    /// <param name="destDir">The destination directory.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task ExtractTarGzAsync(Stream tarGzStream, string destDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destDir);

        using var gzipStream = new GZipStream(tarGzStream, CompressionMode.Decompress);
        await TarFile.ExtractToDirectoryAsync(gzipStream, destDir, overwriteFiles: true, ct);
    }

    /// <summary>
    /// Extracts a tar.gz archive from a byte array to a destination directory.
    /// </summary>
    /// <param name="tarGzData">The tar.gz data as a byte array.</param>
    /// <param name="destDir">The destination directory.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task ExtractTarGzAsync(byte[] tarGzData, string destDir, CancellationToken ct = default)
    {
        using var memoryStream = new MemoryStream(tarGzData);
        await ExtractTarGzAsync(memoryStream, destDir, ct);
    }

    /// <summary>
    /// Lists all files in a tar.gz archive.
    /// </summary>
    /// <param name="tarGzStream">The tar.gz stream to list.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of file paths in the archive.</returns>
    public static async Task<List<string>> ListTarGzContentsAsync(Stream tarGzStream, CancellationToken ct = default)
    {
        var files = new List<string>();

        using var gzipStream = new GZipStream(tarGzStream, CompressionMode.Decompress);
        await using var tarReader = new TarReader(gzipStream);

        while (await tarReader.GetNextEntryAsync(cancellationToken: ct) is { } entry)
        {
            if (entry.EntryType == TarEntryType.RegularFile)
            {
                files.Add(entry.Name);
            }
        }

        return files;
    }
}
