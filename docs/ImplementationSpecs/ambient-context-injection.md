# Ambient Context Injection Implementation

This document describes how to replicate Copilot Chat's context injection pattern in SRE Agent Runtime's `Runner.cs` to provide the agent with environment, workspace, terminal, and instruction file context.

## Design Overview

- **Single provider interface**: `IAmbientContextProvider` with three methods for different injection points
- **VsCodeToolsPlugin implements it**: All formatting logic lives in the plugin
- **SandboxRoot as workspace**: Directory tree shows SandboxRoot structure
- **Instruction files**: Copilot instruction files from `codeRefs/` folder (`copilot-instructions.md`, `*.instructions.md`, `AGENTS.md`)
- **Three injection points**:
  1. First System Message: Instructions (as attachments)
  2. First User Message: Environment + Workspace
  3. 2nd Last User Message: Git repos + Date + Terminals + Reminders
- **User query**: Wrapped in `<user_query>` tags

## How Copilot Chat Does It

### Directory Tree Algorithm (from `visualFileTree.ts`)

The algorithm is **breadth-first expansion with character limit**:

```typescript
// Core algorithm:
1. Start with root directory contents
2. Sort: files first (alphabetically), then directories (alphabetically)
3. Convert to "parts" - files become text, directories become expandable nodes
4. Loop until no more expansion possible:
   - For each directory part, fetch children
   - Convert children to parts (respecting remaining character budget)
   - If new parts added, continue loop
5. Join all parts with newlines

// Sorting logic:
rootNodes.sort((a, b) => {
    if (a[1] === b[1]) {
        return a[0].localeCompare(b[0]);  // Same type: alphabetical
    }
    return a[1] === FileType.Directory ? 1 : -1;  // Files before directories
});

// Truncation logic:
- When item doesn't fit, try adding "..." placeholder
- May need to remove previous items to make space for "..."
- Each level uses tab indentation: '\t'.repeat(level)
- Directories have trailing slash: name + '/'
```

**Key characteristics:**
- **Breadth-first**: All items at level N shown before expanding to level N+1
- **Character-limited**: Stops when `maxLength` (default 2000) exceeded
- **Graceful truncation**: Adds `...` when cutting off, may backtrack to fit
- **Consistent ordering**: Files alphabetically, then directories alphabetically
- **Ignores**: Dot files (optional), .gitignore patterns, copilot-ignored files
- **Excludes non-text files**: Images, video, audio, binary, compiled, fonts, 3D formats, documents, archives

### Excluded File Extensions (from `workspaceFileIndex.ts`)

VS Code's `shouldAlwaysIgnoreFile` excludes these categories:

| Category | Extensions |
|----------|------------|
| **Images** | jpg, jpeg, png, gif, bmp, tiff, ico, webp, svg, heic, raw formats |
| **Video** | mp4, mkv, webm, mov, avi, wmv, flv |
| **Audio** | mp3, wav, m4a, flac, ogg, wma, aac |
| **Compressed** | 7z, bz2, gz, tar, zip, rar, iso |
| **Fonts** | woff, woff2, otf, ttf, eot |
| **3D formats** | obj, fbx, stl, blend, glb, gltf |
| **Documents** | pdf, doc, docx, xls, xlsx, ppt, pptx, psd |
| **Binary/compiled** | exe, dll, so, wasm, jar, class, pyc, pdb |
| **Build artifacts** | log, cache, lock, coverage, map, tsbuildinfo |

**Excluded folders:** node_modules, venv, .git, dist, out, .yarn, .npm

**Excluded files:** .ds_store, thumbs.db, package-lock.json, yarn.lock

### Example Output

```
TerminalRoot/
    README.md
    package.json
    src/
        index.ts
        utils.ts
        components/
            ...
    tests/
        test1.spec.ts
        ...
    docs/
        ...
```

---

## Implementation Plan for SRE Agent

### Phase 1: Create Provider Interface with Three Methods

**Location:** `Agent.Framework/IAmbientContextProvider.cs`

Three injection points matching the final message structure:

```csharp
/// <summary>
/// Provides ambient context strings to inject into agent prompts.
/// </summary>
public interface IAmbientContextProvider
{
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
```

### Final Message Structure

```
[0] SystemMessage: "You are an expert AI programming assistant..." (main agent prompt)

[1] SystemMessage: Instructions Context                    ← GetInstructionsContextAsync()
        <attachment filePath="codeRefs/.github/copilot-instructions.md">
            ... file content ...
        </attachment>
        <instructions>
            Here is a list of instruction files...
            <instruction>
                <file>path/to/file.instructions.md</file>
                <applyTo>**/*.ts</applyTo>
            </instruction>
        </instructions>

[2] UserMessage: Environment Context                       ← GetEnvironmentContextAsync()
        <environment_info>OS</environment_info>
        <workspace_info>Folder structure</workspace_info>

[3] ... Rest of conversation history (previous turns) ...

[N-1] UserMessage: Pre-Query Context                       ← GetPreUserQueryContextAsync()
        <attachments>Git repos in codeRefs/</attachments>
        <context>Current date + Terminal states</context>
        <reminder_tool>Tool usage reminders</reminder_tool>
        <reminder_todo>Plan reminder</reminder_todo>

[N] UserMessage: User Query                                ← Wrapped by Runner
        <user_query>The actual user request</user_query>
```

### Phase 2: Implement in VsCodeToolsPlugin

**Location:** `Agent.Plugins/Implementation/VsCodeToolsPlugin.cs`

Add `IAmbientContextProvider` to VsCodeToolsPlugin:

```csharp
public class VsCodeToolsPlugin : IVsCodeToolsPlugin, IAmbientContextProvider, IDisposable
{
    /// <summary>
    /// Instructions context for FIRST SYSTEM MESSAGE.
    /// - copilot-instructions.md and AGENTS.md: Full content as attachments (with 5K truncation)
    /// - *.instructions.md: Metadata only (file, applyTo, description)
    /// </summary>
    public async Task<string> GetInstructionsContextAsync(CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var result = await LoadInstructionFilesAsync(ct);

        if (result.FullContentFiles.Count == 0 && result.MetadataOnlyFiles.Count == 0)
            return string.Empty;

        const int MaxCharsPerFile = 5000;
        const int MaxTotalChars = 25000;
        int totalCharsUsed = 0;

        // 1. Add full content of main instruction files as attachments
        //    (copilot-instructions.md, AGENTS.md)
        foreach (var file in result.FullContentFiles)
        {
            if (totalCharsUsed >= MaxTotalChars)
            {
                // Budget exceeded - just list remaining files
                sb.AppendLine($"<!-- Additional file: {file.Path} - use ReadFile tool to acquire -->");
                continue;
            }

            var truncatedContent = file.Content;
            var isTruncated = false;

            if (file.Content.Length > MaxCharsPerFile)
            {
                truncatedContent = file.Content[..MaxCharsPerFile];
                truncatedContent += "\n\n[Content truncated. Use ReadFile tool to see full content.]";
                isTruncated = true;
            }

            if (totalCharsUsed + truncatedContent.Length > MaxTotalChars)
            {
                // Would exceed total budget
                truncatedContent = file.Content[..(MaxTotalChars - totalCharsUsed)];
                truncatedContent += "\n\n[Content truncated. Use ReadFile tool to see full content.]";
                isTruncated = true;
            }

            if (isTruncated)
                sb.AppendLine($"<attachment filePath=\"{file.Path}\" isTruncated=\"true\">");
            else
                sb.AppendLine($"<attachment filePath=\"{file.Path}\">");

            sb.AppendLine(truncatedContent);
            sb.AppendLine("</attachment>");

            totalCharsUsed += truncatedContent.Length;
        }

        // 2. Add instruction file list with metadata only (for *.instructions.md files)
        if (result.MetadataOnlyFiles.Count > 0)
        {
            sb.AppendLine("<instructions>");
            sb.AppendLine("Here is a list of instruction files that contain rules for modifying or creating new code.");
            sb.AppendLine("These files are important for ensuring that the code is modified or created correctly.");
            sb.AppendLine("Please make sure to follow the rules specified in these files when working with the codebase.");
            sb.AppendLine("If the file is not already available as attachment, use the 'ReadFile' tool to acquire it.");
            sb.AppendLine("Make sure to acquire the instructions before making any changes to the code.");

            foreach (var file in result.MetadataOnlyFiles)
            {
                sb.AppendLine("<instruction>");
                sb.AppendLine($"<file>{file.Path}</file>");
                if (!string.IsNullOrEmpty(file.ApplyTo))
                    sb.AppendLine($"<applyTo>{file.ApplyTo}</applyTo>");
                if (!string.IsNullOrEmpty(file.Description))
                    sb.AppendLine($"<description>{file.Description}</description>");
                sb.AppendLine("</instruction>");
            }

            sb.AppendLine("</instructions>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Environment context for FIRST USER MESSAGE.
    /// Contains OS and workspace folder structure.
    /// </summary>
    public async Task<string> GetEnvironmentContextAsync(CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        // 1. Environment info
        sb.AppendLine("<environment_info>");
        sb.AppendLine($"The user's current OS is: {GetOperatingSystem()}");
        sb.AppendLine("</environment_info>");

        // 2. Workspace info (SandboxRoot structure)
        sb.AppendLine("<workspace_info>");
        sb.AppendLine("I am working in a workspace with the following folders:");
        sb.AppendLine($"- {SandboxRoot}");
        sb.AppendLine();
        sb.AppendLine("I am working in a workspace that has the following structure:");
        sb.AppendLine("```");
        sb.AppendLine(await BuildDirectoryTreeAsync(SandboxRoot, maxLength: 2000, ct));
        sb.AppendLine("```");
        sb.AppendLine("This is the state of the context at this point. The view may be truncated.");
        sb.AppendLine("</workspace_info>");

        return sb.ToString();
    }

    /// <summary>
    /// Pre-query context for 2ND LAST USER MESSAGE.
    /// Contains git repos, date, terminals, and reminders.
    /// The plugin has internal access to tools, todo state, and terminal state.
    /// </summary>
    public async Task<string> GetPreUserQueryContextAsync(CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        // 1. Git repository attachments
        var gitRepos = await GetGitRepositoriesAsync(ct);
        if (gitRepos.Count > 0)
        {
            sb.AppendLine("<attachments>");
            foreach (var repo in gitRepos)
            {
                sb.AppendLine($"<attachment id=\"{repo.Owner}/{repo.Name}\">");
                sb.AppendLine("Information about one of the current repositories. You can use this information when you need to calculate diffs or compare changes with the default branch:");
                sb.AppendLine($"Repository name: {repo.Name}");
                sb.AppendLine($"Owner: {repo.Owner}");
                sb.AppendLine($"Current branch: {repo.CurrentBranch}");
                sb.AppendLine($"Default branch: {repo.DefaultBranch}");
                sb.AppendLine("</attachment>");
            }
            sb.AppendLine("</attachments>");
        }

        // 2. Context: date + terminal (plugin has access to _terminalManager)
        sb.AppendLine("<context>");
        sb.AppendLine($"The current date is {DateTime.Now:MMMM d, yyyy}.");

        var terminalState = _terminalManager.GetTerminalStateForContext();
        if (!string.IsNullOrEmpty(terminalState))
        {
            sb.AppendLine();
            sb.AppendLine(terminalState);
        }
        sb.AppendLine("</context>");

        // 3. Tool usage reminders (always included)
        sb.AppendLine("<reminder_tool>");
        sb.AppendLine("When using the ReplaceStringInFile tool, include 3-5 lines of unchanged code before and after the string you want to replace, to make it unambiguous which part of the file should be edited.");
        sb.AppendLine("For maximum efficiency, whenever you plan to perform multiple independent edit operations, invoke them simultaneously using MultiReplaceStringInFile tool rather than sequentially.");
        sb.AppendLine("Do NOT create a new markdown file to document each change or summarize your work unless specifically requested by the user.");
        sb.AppendLine("</reminder_tool>");

        // 4. Plan reminder (plugin has direct access to _todoLists)
        var planReminder = BuildPlanReminder();
        if (!string.IsNullOrEmpty(planReminder))
        {
            sb.AppendLine("<reminder_todo>");
            sb.AppendLine(planReminder);
            sb.AppendLine("</reminder_todo>");
        }
        }

        return sb.ToString();
    }

    private static string GetOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS";
        return "Linux";
    }
}
```

### Phase 3: Directory Tree Builder (Exact VS Code Algorithm)

**Location:** `Agent.Plugins/Implementation/VsCodeToolsPlugin.cs` (private method)

```csharp
/// <summary>
/// Builds a directory tree string using VS Code's exact algorithm:
/// - Breadth-first expansion
/// - Files sorted alphabetically before directories
/// - Character-limited with "..." truncation
/// </summary>
private async Task<string> BuildDirectoryTreeAsync(string rootPath, int maxLength, CancellationToken ct)
{
    // Part types: text (final) or dir (expandable)
    var parts = new List<(string value, string? dirPath, int level)>();

    // Initial population from root
    var rootItems = await GetSortedDirectoryContentsAsync(rootPath, ct);
    var initialParts = ToParts(rootItems, rootPath, level: 0, maxLength);
    parts.AddRange(initialParts);
    int remainingSpace = maxLength - PartsLength(parts);

    // Breadth-first expansion loop
    while (true)
    {
        bool didExpand = false;
        var newParts = new List<(string value, string? dirPath, int level)>();

        foreach (var part in parts)
        {
            if (part.dirPath == null)
            {
                // Text part - keep as-is
                newParts.Add(part);
            }
            else
            {
                // Directory part - convert to text and expand children
                newParts.Add((part.value, null, part.level)); // Convert to text

                var children = await GetSortedDirectoryContentsAsync(part.dirPath, ct);
                if (ct.IsCancellationRequested) return string.Empty;

                var subParts = ToParts(children, part.dirPath, part.level + 1, remainingSpace - 1);
                if (subParts.Count > 0)
                {
                    didExpand = true;
                    remainingSpace -= PartsLength(subParts) + 1; // +1 for newline
                    newParts.AddRange(subParts);
                }
            }
        }

        parts = newParts;
        if (!didExpand) break;
    }

    return string.Join("\n", parts.Select(p => p.value));
}

private async Task<List<(string name, bool isDirectory)>> GetSortedDirectoryContentsAsync(
    string path, CancellationToken ct)
{
    var entries = new List<(string name, bool isDirectory)>();

    try
    {
        foreach (var file in Directory.GetFiles(path))
        {
            var name = Path.GetFileName(file);
            if (!name.StartsWith(".") && !ShouldExcludeFile(name))
                entries.Add((name, false));
        }
        foreach (var dir in Directory.GetDirectories(path))
        {
            var name = Path.GetFileName(dir);
            if (!name.StartsWith(".") && !ShouldExcludeFolder(name))
                entries.Add((name, true));
        }
    }
    catch { return entries; }

    // Sort: files first (alphabetically), then directories (alphabetically)
    entries.Sort((a, b) =>
    {
        if (a.isDirectory == b.isDirectory)
            return string.Compare(a.name, b.name, StringComparison.Ordinal);
        return a.isDirectory ? 1 : -1; // Files before directories
    });

    return entries;
}

// Excluded extensions (matching VS Code's workspaceFileIndex.ts)
private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
{
    // Images
    "jpg", "jpeg", "jpe", "png", "gif", "bmp", "tif", "tiff", "tga", "ico", "icns", "xpm",
    "webp", "svg", "eps", "heif", "heic", "raw", "arw", "cr2", "cr3", "nef", "dng",
    // Video
    "mp4", "m4v", "mkv", "webm", "mov", "avi", "wmv", "flv",
    // Audio
    "mp3", "wav", "m4a", "flac", "ogg", "wma", "weba", "aac", "pcm",
    // Compressed
    "7z", "bz2", "gz", "tgz", "rar", "tar", "xz", "zip", "vsix", "iso", "img", "pkg",
    // Fonts
    "woff", "woff2", "otf", "ttf", "eot",
    // 3D formats
    "obj", "fbx", "stl", "3ds", "dae", "blend", "ply", "glb", "gltf", "max", "c4d",
    // Documents
    "pdf", "ai", "ps", "indd", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "ods", "odp", "rtf", "psd", "pbix",
    // Binary/compiled
    "exe", "dll", "dylib", "so", "a", "o", "lib", "out", "elf", "wasm", "pdb", "idb", "sym",
    "nupkg", "winmd", "pyc", "pkl", "pickle", "pyd", "rlib", "rmeta", "dill",
    "jar", "class", "ear", "war", "apk", "dex", "phar",
    // Build artifacts
    "temp", "tmp", "db", "sqlite", "parquet", "bin", "dat", "data", "hex", "cache", "sum", "hash",
    "coverage", "testlog", "log", "trace", "tlog", "snap", "msi", "deb",
    "vsidx", "suo", "xcuserstate", "download", "map", "tsbuildinfo", "jsbundle",
    "lock", "git", "pack"
};

private static readonly HashSet<string> ExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
{
    "node_modules", "venv", "out", "dist", ".git", ".yarn", ".npm", ".venv", ".vscode-test"
};

private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
{
    ".ds_store", "thumbs.db", "package-lock.json", "yarn.lock", ".cache"
};

private static bool ShouldExcludeFile(string fileName)
{
    if (ExcludedFiles.Contains(fileName))
        return true;

    var ext = Path.GetExtension(fileName).TrimStart('.');
    return ExcludedExtensions.Contains(ext);
}

private static bool ShouldExcludeFolder(string folderName)
{
    return ExcludedFolders.Contains(folderName);
}

private List<(string value, string? dirPath, int level)> ToParts(
    List<(string name, bool isDirectory)> items,
    string parentPath,
    int level,
    int maxLength)
{
    var indent = new string('\t', level);
    var parts = new List<(string value, string? dirPath, int level)>();
    int remainingSpace = maxLength;

    for (int i = 0; i < items.Count; i++)
    {
        var (name, isDir) = items[i];
        var str = indent + name + (isDir ? "/" : "");

        if (str.Length > remainingSpace)
        {
            // Try adding "..." placeholder
            var placeholder = indent + "...";

            // Remove previous items until there's space for placeholder
            while (placeholder.Length > remainingSpace && parts.Count > 0)
            {
                remainingSpace += parts[^1].value.Length + 1;
                parts.RemoveAt(parts.Count - 1);
            }

            if (placeholder.Length <= remainingSpace)
                parts.Add((placeholder, null, level));

            break;
        }

        if (isDir)
            parts.Add((str, Path.Combine(parentPath, name), level));
        else
            parts.Add((str, null, level));

        remainingSpace -= str.Length;
        if (i != items.Count - 1)
            remainingSpace -= 1; // Account for newline
    }

    return parts;
}

private static int PartsLength(List<(string value, string? dirPath, int level)> parts)
{
    int len = parts.Sum(p => p.value.Length);
    return len + Math.Max(0, parts.Count - 1); // Add newlines between parts
}
```

### Phase 4: Instruction File Loading

**Location:** `Agent.Plugins/Implementation/VsCodeToolsPlugin.cs` (private method)

```csharp
/// <summary>
/// Result of loading instruction files - contains both full-content files
/// (copilot-instructions.md, AGENTS.md) and metadata-only files (*.instructions.md).
/// </summary>
public record InstructionFilesResult(
    IReadOnlyList<InstructionFileContent> FullContentFiles,
    IReadOnlyList<InstructionFileMetadata> MetadataOnlyFiles);

public record InstructionFileContent(string Path, string Content);

public record InstructionFileMetadata(string Path, string? Description, string? ApplyTo);

/// <summary>
/// Loads copilot instruction files from codeRefs/ folder.
/// Separates:
/// - Full content files: copilot-instructions.md, AGENTS.md (content loaded)
/// - Metadata-only files: *.instructions.md (only file path + frontmatter parsed)
/// </summary>
private async Task<InstructionFilesResult> LoadInstructionFilesAsync(CancellationToken ct)
{
    var fullContentFiles = new List<InstructionFileContent>();
    var metadataOnlyFiles = new List<InstructionFileMetadata>();
    var codeRefsPath = Path.Combine(SandboxRoot, "codeRefs");

    if (!Directory.Exists(codeRefsPath))
        return new InstructionFilesResult(fullContentFiles, metadataOnlyFiles);

    try
    {
        // Pattern 1: copilot-instructions.md (full content)
        foreach (var file in Directory.GetFiles(codeRefsPath, "copilot-instructions.md", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(SandboxRoot, file);
            var content = await File.ReadAllTextAsync(file, ct);
            fullContentFiles.Add(new InstructionFileContent(relativePath, content));
        }

        // Pattern 2: AGENTS.md (full content)
        foreach (var file in Directory.GetFiles(codeRefsPath, "AGENTS.md", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(SandboxRoot, file);
            var content = await File.ReadAllTextAsync(file, ct);
            fullContentFiles.Add(new InstructionFileContent(relativePath, content));
        }

        // Pattern 3: *.instructions.md (metadata only, NOT full content)
        foreach (var file in Directory.GetFiles(codeRefsPath, "*.instructions.md", SearchOption.AllDirectories))
        {
            // Skip if it's copilot-instructions.md (already handled)
            if (Path.GetFileName(file).Equals("copilot-instructions.md", StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = Path.GetRelativePath(SandboxRoot, file);
            var (description, applyTo) = await ParseInstructionMetadataAsync(file, ct);
            metadataOnlyFiles.Add(new InstructionFileMetadata(relativePath, description, applyTo));
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to load instruction files from {Path}", codeRefsPath);
    }

    return new InstructionFilesResult(fullContentFiles, metadataOnlyFiles);
}

/// <summary>
/// Parses instruction file frontmatter for description and applyTo fields.
/// Reads just the first ~50 lines to find YAML frontmatter.
/// </summary>
private async Task<(string? description, string? applyTo)> ParseInstructionMetadataAsync(
    string filePath, CancellationToken ct)
{
    try
    {
        // Read first 50 lines to find frontmatter
        var lines = new List<string>();
        using var reader = new StreamReader(filePath);
        for (int i = 0; i < 50 && !reader.EndOfStream; i++)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line != null) lines.Add(line);
        }

        // Check for YAML frontmatter (starts and ends with ---)
        if (lines.Count < 2 || lines[0] != "---")
            return (null, null);

        var endIndex = lines.FindIndex(1, l => l == "---");
        if (endIndex < 0)
            return (null, null);

        string? description = null;
        string? applyTo = null;

        for (int i = 1; i < endIndex; i++)
        {
            var line = lines[i];
            if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                description = line["description:".Length..].Trim().Trim('"', '\'');
            else if (line.StartsWith("applyTo:", StringComparison.OrdinalIgnoreCase))
                applyTo = line["applyTo:".Length..].Trim().Trim('"', '\'');
        }

        return (description, applyTo);
    }
    catch
    {
        return (null, null);
    }
}
```

**File type handling:**
| Pattern | Handling |
|---------|----------|
| `copilot-instructions.md` | Full content as `<attachment>` with 5K truncation |
| `AGENTS.md` | Full content as `<attachment>` with 5K truncation |
| `*.instructions.md` | Metadata only: `<instruction>` with `<file>`, `<applyTo>`, `<description>` |

**Truncation budget:**
- 5,000 characters per file maximum
- 25,000 characters total maximum
- Add `isTruncated="true"` attribute when truncated
- Add truncation message: `[Content truncated. Use ReadFile tool to see full content.]`
- Files exceeding total budget are listed as comments only

### Phase 4.5: Terminal State for Context

**Location:** `Agent.Plugins/Services/TerminalSessionManager.cs` (new method)

```csharp
/// <summary>
/// Gets terminal state formatted for context injection.
/// Shows just the last foreground command with its exit code.
/// </summary>
public string GetTerminalStateForContext()
{
    var session = GetCurrentSession();
    if (session == null)
        return string.Empty;

    // Only include if there was a last command
    if (string.IsNullOrEmpty(session.LastCommand))
        return string.Empty;

    var sb = new StringBuilder();
    sb.AppendLine($"Last command: {session.LastCommand}");
    if (session.LastExitCode.HasValue)
        sb.AppendLine($"Exit code: {session.LastExitCode.Value}");

    return sb.ToString().TrimEnd();
}
```

**Terminal context shows:**
- Just the last foreground command with its exit code
- Uses existing `TerminalSession.LastCommand` and `TerminalSession.LastExitCode` properties
- No Cwd tracking needed (use `VsCodeToolsPlugin.SandboxRoot` instead)

### Phase 4.6: Git Repository Information

**Location:** `Agent.Plugins/Implementation/VsCodeToolsPlugin.cs` (private method)

```csharp
public record GitRepositoryInfo(string Name, string Owner, string CurrentBranch, string DefaultBranch);

/// <summary>
/// Discovers git repositories in the codeRefs/ folder and extracts branch info.
/// Parses .git folder directly (no git CLI dependency).
/// </summary>
private async Task<List<GitRepositoryInfo>> GetGitRepositoriesAsync(CancellationToken ct)
{
    var result = new List<GitRepositoryInfo>();
    var codeRefsPath = Path.Combine(SandboxRoot, "codeRefs");

    if (!Directory.Exists(codeRefsPath))
        return result;

    try
    {
        // Find all .git directories (indicating repo roots)
        var gitDirs = Directory.GetDirectories(codeRefsPath, ".git", SearchOption.AllDirectories);

        foreach (var gitDir in gitDirs)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var repoRoot = Path.GetDirectoryName(gitDir)!;
                var repoName = Path.GetFileName(repoRoot);

                // Get current branch by parsing .git/HEAD
                var currentBranch = await ParseCurrentBranchAsync(gitDir, ct);

                // Get default branch and owner from .git/config
                var (owner, defaultBranch) = await ParseGitConfigAsync(gitDir, ct);

                result.Add(new GitRepositoryInfo(repoName, owner, currentBranch, defaultBranch));
            }
            catch
            {
                // Skip corrupted/unreadable .git folders
                continue;
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to discover git repositories in {Path}", codeRefsPath);
    }

    return result;
}

/// <summary>
/// Parses current branch from .git/HEAD file.
/// Format: "ref: refs/heads/branch-name" or a commit hash (detached HEAD).
/// </summary>
private async Task<string> ParseCurrentBranchAsync(string gitDir, CancellationToken ct)
{
    var headPath = Path.Combine(gitDir, "HEAD");
    if (!File.Exists(headPath))
        return "unknown";

    try
    {
        var content = (await File.ReadAllTextAsync(headPath, ct)).Trim();

        // Check if it's a ref (normal branch)
        if (content.StartsWith("ref: refs/heads/"))
            return content["ref: refs/heads/".Length..];

        // Detached HEAD (commit hash) - return abbreviated hash
        if (content.Length >= 7)
            return content[..7];

        return "unknown";
    }
    catch
    {
        return "unknown";
    }
}

/// <summary>
/// Parses .git/config to extract remote origin URL and default branch.
/// </summary>
private async Task<(string owner, string defaultBranch)> ParseGitConfigAsync(
    string gitDir, CancellationToken ct)
{
    var configPath = Path.Combine(gitDir, "config");
    var owner = "unknown";
    var defaultBranch = "main"; // Default assumption

    if (!File.Exists(configPath))
        return (owner, defaultBranch);

    try
    {
        var lines = await File.ReadAllLinesAsync(configPath, ct);
        var inRemoteOrigin = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed == "[remote \"origin\"]")
            {
                inRemoteOrigin = true;
                continue;
            }

            if (trimmed.StartsWith("[") && inRemoteOrigin)
            {
                inRemoteOrigin = false;
                continue;
            }

            if (inRemoteOrigin && trimmed.StartsWith("url = "))
            {
                var url = trimmed["url = ".Length..];
                owner = ExtractOwnerFromUrl(url);
            }
        }

        // Try to read default branch from refs/remotes/origin/HEAD
        var originHeadPath = Path.Combine(gitDir, "refs", "remotes", "origin", "HEAD");
        if (File.Exists(originHeadPath))
        {
            var content = (await File.ReadAllTextAsync(originHeadPath, ct)).Trim();
            if (content.StartsWith("ref: refs/remotes/origin/"))
                defaultBranch = content["ref: refs/remotes/origin/".Length..];
        }
        else
        {
            // Fallback: check if main or master refs exist
            var refsPath = Path.Combine(gitDir, "refs", "remotes", "origin");
            if (Directory.Exists(refsPath))
            {
                if (File.Exists(Path.Combine(refsPath, "main")))
                    defaultBranch = "main";
                else if (File.Exists(Path.Combine(refsPath, "master")))
                    defaultBranch = "master";
            }
        }
    }
    catch
    {
        // Return defaults on any error
    }

    return (owner, defaultBranch);
}

/// <summary>
/// Extracts owner from git remote URL.
/// Supports: https://github.com/owner/repo.git, git@github.com:owner/repo.git
/// </summary>
private static string ExtractOwnerFromUrl(string url)
{
    try
    {
        // HTTPS format: https://github.com/owner/repo.git
        var httpsMatch = Regex.Match(url, @"github\.com/([^/]+)/");
        if (httpsMatch.Success)
            return httpsMatch.Groups[1].Value;

        // SSH format: git@github.com:owner/repo.git
        var sshMatch = Regex.Match(url, @"github\.com:([^/]+)/");
        if (sshMatch.Success)
            return sshMatch.Groups[1].Value;

        // Azure DevOps, GitLab, etc. - add patterns as needed
    }
    catch { }

    return "unknown";
}
```

**Git info extraction (no git CLI needed):**
- **Current branch**: Parse `.git/HEAD` file
- **Owner**: Parse `.git/config` for `[remote "origin"]` URL
- **Default branch**: Parse `.git/refs/remotes/origin/HEAD` or check for main/master refs
- **Corrupted .git folders**: Silently skipped

### Phase 5: Modify Runner.cs

**Location:** `Agent.Framework/Runner.cs`

#### 5.1 Add IAmbientContextProvider to RunConfig

Runner remains a static class. Add the provider to `RunConfig`:

```csharp
public class RunConfig
{
    // ... existing properties ...

    /// <summary>
    /// Optional ambient context provider for injecting environment, workspace,
    /// and instruction file context into the agent prompt.
    /// </summary>
    public IAmbientContextProvider? AmbientContextProvider { get; init; }
}
```

**Note:** The modelInput is rebuilt fresh each turn from the conversation history. Context injection happens during message building, so there's no need to track "first turn" - the injected messages are always reconstructed.

#### 5.2 Add Three Context Injection Points in RunSingleStepAsync

```csharp
// In RunSingleStepAsync, after building modelInput:

// 1. Insert INSTRUCTIONS context as first system message (after main system prompt)
await AddInstructionsContextAsync(modelInput, runConfig.AmbientContextProvider);

// 2. Insert ENVIRONMENT context as first user message
await AddEnvironmentContextAsync(modelInput, runConfig.AmbientContextProvider);

// 3. Before the final user message, insert PRE-QUERY context
await AddPreUserQueryContextAsync(modelInput, runConfig.AmbientContextProvider);

// 4. Wrap the user's query in <user_query> tags
WrapUserQueryMessage(modelInput);
```

#### 5.3 Implement AddInstructionsContextAsync

```csharp
private static async Task AddInstructionsContextAsync(
    List<ChatMessage> modelInput,
    IAmbientContextProvider? provider)
{
    if (provider == null)
        return;

    var instructionsContext = await provider.GetInstructionsContextAsync();

    if (!string.IsNullOrWhiteSpace(instructionsContext))
    {
        // Insert as SYSTEM message right after the main system prompt (index 1)
        modelInput.Insert(1, new ChatMessage(ChatRole.System, instructionsContext));
    }
}
```

#### 5.4 Implement AddEnvironmentContextAsync

```csharp
private static async Task AddEnvironmentContextAsync(
    List<ChatMessage> modelInput,
    IAmbientContextProvider? provider)
{
    if (provider == null)
        return;

    var envContext = await provider.GetEnvironmentContextAsync();

    if (!string.IsNullOrWhiteSpace(envContext))
    {
        // Find index after system messages (first non-system message position)
        int insertIndex = modelInput.FindIndex(m => m.Role != ChatRole.System);
        if (insertIndex < 0) insertIndex = modelInput.Count;

        modelInput.Insert(insertIndex, new ChatMessage(ChatRole.User, envContext));
    }
}
```

#### 5.5 Implement AddPreUserQueryContextAsync

```csharp
private static async Task AddPreUserQueryContextAsync(
    List<ChatMessage> modelInput,
    IAmbientContextProvider? provider)
{
    if (provider == null)
        return;

    var preQueryContext = await provider.GetPreUserQueryContextAsync();

    if (!string.IsNullOrWhiteSpace(preQueryContext))
    {
        // Insert as 2ND LAST message (before the user's query)
        int insertIndex = modelInput.Count; // Will become 2nd last after user query is added
        modelInput.Insert(insertIndex, new ChatMessage(ChatRole.User, preQueryContext));
    }
}
```

#### 5.6 Implement WrapUserQueryMessage

```csharp
private static void WrapUserQueryMessage(List<ChatMessage> modelInput)
{
    // Find the last user message (the actual user query)
    var lastUserIndex = -1;
    for (int i = modelInput.Count - 1; i >= 0; i--)
    {
        if (modelInput[i].Role == ChatRole.User)
        {
            lastUserIndex = i;
            break;
        }
    }

    if (lastUserIndex < 0) return;

    var originalContent = modelInput[lastUserIndex].Text;
    var wrappedContent = $"<user_query>\n{originalContent}\n</user_query>";
    modelInput[lastUserIndex] = new ChatMessage(ChatRole.User, wrappedContent);
}
```

#### 5.7 Reuse AddPlanReminderIfNeeded Logic

The plan reminder logic from the existing `AddPlanReminderIfNeeded` should be reused inside `GetPreUserQueryContextAsync` via the `BuildPlanReminder` method. The existing `AddPlanReminderIfNeeded` in Runner.cs should be made a no-op when the ambient context provider is present.

### Phase 6: Wire Up in DI

**Location:** `Agent.Plugins/Extensions/VsCodeToolsServiceCollectionExtensions.cs`

```csharp
public static IServiceCollection AddVsCodeTools(this IServiceCollection services)
{
    // ... existing registrations ...

    // VsCodeToolsPlugin now implements both interfaces
    services.AddSingleton<VsCodeToolsPlugin>();
    services.AddSingleton<IVsCodeToolsPlugin>(sp => sp.GetRequiredService<VsCodeToolsPlugin>());
    services.AddSingleton<IAmbientContextProvider>(sp => sp.GetRequiredService<VsCodeToolsPlugin>());

    return services;
}
```

### Phase 7: Wire Up in ReasoningLoop

**Location:** `Agent.Runtime/Reasoning/ReasoningLoop.cs`

Inject `IAmbientContextProvider` and pass it via RunConfig:

```csharp
public class ReasoningLoop
{
    private readonly IAmbientContextProvider? _ambientContextProvider;

    public ReasoningLoop(
        // ... existing parameters ...
        IAmbientContextProvider? ambientContextProvider = null)
    {
        _ambientContextProvider = ambientContextProvider;
    }

    public async Task RunAsync(...)
    {
        var runConfig = new RunConfig
        {
            // ... existing config ...
            AmbientContextProvider = _ambientContextProvider
        };

        await Runner.RunAsync(runConfig, ...);
    }
}
```

---

## Context Message Placement

### After Implementation
```
[0] SystemMessage: Main agent prompt ("You are an expert AI programming assistant...")

[1] SystemMessage: Instructions Context                    ← GetInstructionsContextAsync()
        <attachment>copilot-instructions.md content</attachment>
        <instructions>List of instruction files</instructions>

[2] UserMessage: Environment Context                       ← GetEnvironmentContextAsync()
        <environment_info>OS</environment_info>
        <workspace_info>Folder structure</workspace_info>

[3] ... Original conversation history (previous turns) ...

[N-1] UserMessage: Pre-Query Context                       ← GetPreUserQueryContextAsync()
        <attachments>Git repos</attachments>
        <context>Date + Terminals</context>
        <reminder_tool>Tool usage reminders</reminder_tool>
        <reminder_todo>Plan reminder</reminder_todo>

[N] UserMessage: User Query                                ← WrapUserQueryMessage()
        <user_query>The actual user request</user_query>
```

---

## File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `Agent.Framework/IAmbientContextProvider.cs` | **New** | Interface with 3 parameterless methods: `GetInstructionsContextAsync()`, `GetEnvironmentContextAsync()`, `GetPreUserQueryContextAsync()` |
| `Agent.Framework/RunConfig.cs` | **Modify** | Add `AmbientContextProvider` property |
| `Agent.Plugins/Implementation/VsCodeToolsPlugin.cs` | **Modify** | Implement `IAmbientContextProvider`, add tree builder, git discovery (parsing .git folder), add record types for instruction files |
| `Agent.Plugins/Interface/IVsCodeToolsPlugin.cs` | **No change** | Keep existing interface separate |
| `Agent.Plugins/Services/TerminalSessionManager.cs` | **Modify** | Add `GetTerminalStateForContext()` |
| `Agent.Framework/Runner.cs` | **Modify** | Add static injection methods that read from `RunConfig.AmbientContextProvider` |
| `Agent.Runtime/Reasoning/ReasoningLoop.cs` | **Modify** | Inject `IAmbientContextProvider` via DI and pass via RunConfig |
| `Agent.Plugins/Extensions/VsCodeToolsServiceCollectionExtensions.cs` | **Modify** | Register `IAmbientContextProvider` |

---

## Output Format Examples

### Instructions Context (GetInstructionsContextAsync) - First System Message
```xml
<attachment filePath="codeRefs/.github/copilot-instructions.md">
# Project Guidelines
- Use TypeScript for all new code
- Follow ESLint rules
</attachment>
<attachment filePath="codeRefs/AGENTS.md">
# Agent Instructions
When working on this project...
</attachment>
<instructions>
Here is a list of instruction files that contain rules for modifying or creating new code.
These files are important for ensuring that the code is modified or created correctly.
Please make sure to follow the rules specified in these files when working with the codebase.
If the file is not already available as attachment, use the 'ReadFile' tool to acquire it.
Make sure to acquire the instructions before making any changes to the code.
<instruction>
<file>codeRefs/.github/instructions/typescript.instructions.md</file>
<applyTo>**/*.ts</applyTo>
<description>TypeScript coding guidelines</description>
</instruction>
<instruction>
<file>codeRefs/.github/instructions/testing.instructions.md</file>
<applyTo>**/*.spec.ts</applyTo>
<description>Unit testing guidelines</description>
</instruction>
</instructions>
```

**Note:** Only `copilot-instructions.md` and `AGENTS.md` get full content as `<attachment>` elements. The `*.instructions.md` files are listed in `<instructions>` with metadata only (file path, applyTo, description).

### Environment Context (GetEnvironmentContextAsync) - First User Message
```xml
<environment_info>
The user's current OS is: Windows
</environment_info>
<workspace_info>
I am working in a workspace with the following folders:
- C:\Users\visagarwal\SandboxRoot

I am working in a workspace that has the following structure:
```
SandboxRoot/
    README.md
    package.json
    codeRefs/
        .github/
            copilot-instructions.md
        AGENTS.md
    src/
        index.ts
        utils.ts
        components/
            ...
    tests/
        ...
```
This is the state of the context at this point. The view may be truncated.
</workspace_info>
```

### Pre-Query Context (GetPreUserQueryContextAsync) - 2nd Last User Message
```xml
<attachments>
<attachment id="serverless-paas-balam/sreagent-runtime">
Information about one of the current repositories. You can use this information when you need to calculate diffs or compare changes with the default branch:
Repository name: sreagent-runtime
Owner: serverless-paas-balam
Current branch: vscode
Default branch: main
</attachment>
</attachments>
<context>
The current date is January 4, 2026.

Last command: npx tsc --noEmit
Exit code: 0
</context>
<reminder_tool>
When using the ReplaceStringInFile tool, include 3-5 lines of unchanged code before and after the string you want to replace, to make it unambiguous which part of the file should be edited.
For maximum efficiency, whenever you plan to perform multiple independent edit operations, invoke them simultaneously using MultiReplaceStringInFile tool rather than sequentially.
Do NOT create a new markdown file to document each change or summarize your work unless specifically requested by the user.
</reminder_tool>
<reminder_todo>
You have an active todo list. Remember to update it as you complete tasks.
Current status: 2/5 completed
</reminder_todo>
```

### User Query (WrapUserQueryMessage) - Last User Message
```xml
<user_query>
Update the impl plan to match this.
</user_query>
```

---

## Dependencies

- `TerminalSessionManager` (existing)
- `RuntimeInformation.IsOSPlatform` (for OS detection)
- Standard `System.IO` for directory operations and .git folder parsing
- `System.Text.RegularExpressions` for parsing git remote URLs

**No external dependencies required** - git info is extracted by parsing `.git` folder directly (no git CLI needed).
