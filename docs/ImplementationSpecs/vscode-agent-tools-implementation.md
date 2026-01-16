# VS Code Agent Tools Implementation Plan

## Overview

This document outlines the implementation plan for cloning GitHub Copilot's agent mode functionality into SreAgentRuntime. The goal is to provide VS Code-like coding agent capabilities, including file operations, search, terminal execution, and task management.

**Implementation Philosophy:** This is a **direct port** of the Node.js/TypeScript implementations in CopilotChat. Match the original behavior exactly wherever source code is available. Only deviate when the implementation would exceed ~50 lines of code for a single feature, in which case simplify.

**Reference Files:**
- Source prompt/tools capture: `CopilotChat/cloning/panel_editAgent_729c9838.copilotmd`
- Source implementations: `CopilotChat/src/extension/tools/node/`
- VS Code core tools: `microsoft/vscode` repo (`src/vs/workbench/contrib/chat/common/tools/`)
- Target experiment file: `Agent.Runtime/Experiments/VsCodeExperiment.yaml`

---

## Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Security Model** | Single-tenant, no sandboxing initially | Production will redirect commands to separate sandbox |
| **String Comparison** | Ordinal (no Unicode normalization) | Simplicity; ASCII-focused workloads |
| **Output Format** | Match Node exactly | Clone fidelity |
| **Error Handling** | Transparent exceptions as strings | "Tool call failed: reason" format |
| **Platform Target** | Cross-platform, Linux primary | Windows can throw `NotImplementedException` for complex items |
| **Shell** | `bash` on Linux (not configurable) | Simplicity |
| **Environment** | Inherit parent process environment | Standard behavior |

### Sandbox Roots

All file and terminal operations are restricted to these paths:

| Platform | Sandbox Root |
|----------|--------------|
| Linux | `/home/grayfrost/terminalRoot` |
| Windows | `C:\Users\visagarwal\OneDrive - Microsoft\Desktop\SreAgent\TerminalRoot` |

---

## Part 1: Tools Inventory

Based on the captured Copilot agent request, here are the **32 tools** organized by category:

### Category A: Core File/Directory Operations (8 tools)

| Tool Name | Description | Complexity |
|-----------|-------------|------------|
| `create_directory` | Create directory structure recursively (mkdir -p) | Low |
| `create_file` | Create new file with content, auto-creates directories | Low |
| `read_file` | Read file contents with line range (1-indexed) | Medium |
| `list_dir` | List directory contents (files and folders) | Low |
| `replace_string_in_file` | Find and replace exact string in file with context | Medium |
| `multi_replace_string_in_file` | Batch replacement operations across files | Medium |
| `file_search` | Search for files by glob pattern | Medium |
| `grep_search` | Text/regex search in files with optional filters | Medium |

### Category B: Code Intelligence (5 tools)

| Tool Name | Description | Status |
|-----------|-------------|--------|
| `semantic_search` | Natural language search for code/documentation | **DEFER** - implement later phase |
| `list_code_usages` | Find references, definitions, implementations of symbols | **SKIP** - requires LSP |
| `get_vscode_api` | VS Code API documentation | **SKIP** - not relevant |
| `get_errors` | Get compile/lint errors for files | **SKIP** - requires VS Code |
| `get_search_view_results` | Get results from search view | **SKIP** - VS Code UI specific |

### Category C: Git/SCM Operations (1 tool)

| Tool Name | Description | Status |
|-----------|-------------|--------|
| `get_changed_files` | Get git diffs (staged, unstaged, merge-conflicts) | **SKIP** - use terminal git commands instead |

### Category D: Terminal Operations (4 tools)

| Tool Name | Description | Complexity |
|-----------|-------------|------------|
| `run_in_terminal` | Execute shell commands with session persistence | High |
| `get_terminal_output` | Get output from background terminal command | Medium |
| `terminal_selection` | Get current terminal selection | Low |
| `terminal_last_command` | Get last command run in terminal | Low |

### Category E: Web/External Data (2 tools)

| Tool Name | Description | Status |
|-----------|-------------|--------|
| `fetch_webpage` | Fetch and extract content from web pages | **IMPLEMENT** - clone Node impl |
| `github_repo` | Search GitHub repository for code snippets | **SKIP** - requires GitHub API + embeddings |

### Category F: Task Management (1 tool)

| Tool Name | Description | Complexity |
|-----------|-------------|------------|
| `manage_todo_list` | Track tasks with status (not-started, in-progress, completed) | Low |

### Category G: MCP External Tools (11 tools)

These are provided via MCP server configuration, not native implementation:

| Tool Name | MCP Server |
|-----------|------------|
| `mcp_deepwiki_ask_question` | DeepWiki |
| `mcp_deepwiki_read_wiki_contents` | DeepWiki |
| `mcp_deepwiki_read_wiki_structure` | DeepWiki |
| `mcp_microsoft_doc_microsoft_docs_fetch` | Microsoft Docs |
| `mcp_microsoft_doc_microsoft_docs_search` | Microsoft Docs |
| `mcp_playwright-mc_browser_click` | Playwright |
| `mcp_playwright-mc_browser_close` | Playwright |
| `mcp_playwright-mc_browser_evaluate` | Playwright |
| `mcp_playwright-mc_browser_navigate` | Playwright |
| `mcp_playwright-mc_browser_snapshot` | Playwright |
| `mcp_playwright-mc_browser_wait_for` | Playwright |

### Category H: VS Code Specific (Skip)

| Tool Name | Notes |
|-----------|-------|
| `vscode_searchExtensions_internal` | VS Code marketplace - not applicable |

---

## Part 2: Implementation Approach

### Guiding Principle: Clone the Node Implementation

For every tool where source code is available in `CopilotChat/src/extension/tools/node/`, the C# implementation must:

1. **Match input schemas exactly** as captured in `panel_editAgent_729c9838.copilotmd`
2. **Match output format exactly** - same strings, same structure
3. **Match behavioral edge cases** - error messages, truncation, normalization
4. **Use ordinal string comparison** - no culture-aware operations

### Tools to Skip

| Tool | Reason |
|------|--------|
| `list_code_usages` | Requires VS Code Language Server Protocol |
| `get_vscode_api` | Not relevant for SRE context |
| `get_errors` | Requires VS Code language services |
| `get_search_view_results` | VS Code UI specific |
| `vscode_searchExtensions_internal` | VS Code marketplace |
| `get_changed_files` | Use `run_in_terminal` with `git` commands instead |
| `github_repo` | Requires GitHub API + embedding infrastructure |
| `semantic_search` | Deferred to later phase (requires embeddings) |

### Tools to Implement

| Tool | Source Reference |
|------|------------------|
| `read_file` | `node/readFileTool.tsx` |
| `create_file` | `node/createFileTool.tsx` |
| `create_directory` | `node/createDirectoryTool.tsx` |
| `list_dir` | `node/listDirTool.tsx` |
| `replace_string_in_file` | `node/replaceStringTool.tsx` + `abstractReplaceStringTool.tsx` |
| `multi_replace_string_in_file` | `node/multiReplaceStringTool.tsx` |
| `file_search` | `node/findFilesTool.tsx` |
| `grep_search` | `node/findTextInFilesTool.tsx` |
| `run_in_terminal` | Custom (VS Code core tool) |
| `get_terminal_output` | Custom (VS Code core tool) |
| `terminal_last_command` | Custom (VS Code core tool) |
| `terminal_selection` | Custom (VS Code core tool) |
| `manage_todo_list` | `microsoft/vscode` repo: `manageTodoListTool.ts` |
| `fetch_webpage` | `vscode-node/fetchWebPageTool.tsx` |

---

## Part 3: Terminal Operations (Custom Implementation)

Terminal tools are "core" VS Code tools not implemented in the CopilotChat extension. We implement our own version using real shell processes.

### Terminal Session Manager

### Architecture

Spawn real `bash` processes and communicate via stdin/stdout pipes.

```csharp
public class TerminalSession
{
    public string Id { get; init; }                    // Simple format: "term-1", "term-2"
    public string? Purpose { get; set; }               // LLM-provided description
    public string Shell { get; init; } = "bash";       // Always bash on Linux
    public string WorkingDirectory { get; set; }       // Tracked via shell state
    public string LastCommand { get; set; }
    public StringBuilder OutputBuffer { get; } = new();
    public bool IsBackground { get; set; }
    public Process Process { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastActivityAt { get; set; }
}
```

### Session Lifecycle

| Aspect | Behavior |
|--------|----------|
| **Session ID Format** | Simple incrementing: `term-1`, `term-2`, etc. |
| **Idle Timeout** | 5 minutes of inactivity → session terminated |
| **Multiple Sessions** | Supported from the start |
| **Throwaway Commands** | Support non-persisted sessions (no ID tracking) |
| **Environment** | Inherit parent process environment variables |
| **Initial Working Directory** | Agent root (configurable) - default is executing DLL location |

### `run_in_terminal` Behavior

| Mode | Behavior |
|------|----------|
| `isBackground=false` | Wait for command completion, return full output |
| `isBackground=true` | Wait exactly **2 seconds** (or until process exits early), return terminal ID + initial output |

**Output Format (matching VS Code):**
```
Command is running in terminal with ID=term-1
The command became idle with output:
[truncated output - first N chars + last N chars]
```

### Output Truncation

| Limit | Behavior |
|-------|----------|
| **60KB total** | Truncate output, keeping first + last portions |
| **Stderr** | Interleaved with stdout (not separated) |

### `get_terminal_output`

Returns buffered output since session start (non-destructive read). Format:
```
Output of terminal term-1:
[command prompt and output]
```

### `terminal_last_command`

Returns the last command executed in the active terminal session.

### `terminal_selection`

Returns empty string (no selection concept without VS Code UI).

---

## Part 4: File Operations (Clone Node Implementations)

All file operations use direct .NET APIs. Match Node behavior exactly.

### Path Validation

All paths must be within the sandbox root. Use same checks as Node implementation.

```csharp
private bool IsPathWithinSandbox(string path)
{
    var sandboxRoot = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? @"C:\Users\visagarwal\OneDrive - Microsoft\Desktop\SreAgent\TerminalRoot"
        : "/home/grayfrost/terminalRoot";
    
    var fullPath = Path.GetFullPath(path);
    return fullPath.StartsWith(sandboxRoot, StringComparison.Ordinal);
}
```

### `read_file`

**Schema (V1 - matches copilotmd):**
```json
{
  "required": ["filePath", "startLine", "endLine"],
  "properties": {
    "filePath": { "type": "string" },
    "startLine": { "type": "number", "description": "1-based" },
    "endLine": { "type": "number", "description": "1-based, inclusive" }
  }
}
```

**Behavior (from `readFileTool.tsx`):**
- Lines are 1-indexed
- If `startLine > endLine`, swap them (forgiving)
- Clamp to file bounds
- **MAX_LINES_PER_READ = 2000** - truncate and indicate more available
- Return error if offset > file line count

**Output format:**
```
File: path/to/file.ts (lines 1-100 of 500)
[content...]
```

If truncated:
```
(Output truncated. Read more lines with offset parameter.)
```

### `create_file`

**Behavior (from `createFileTool.tsx`):**
- Throw if file already exists: `"File already exists. You must use an edit tool to modify it."`
- Auto-create parent directories
- Strip leading filepath comments (e.g., `// src/foo.ts`) using `removeLeadingFilepathComment()`

**Output:** `"File created at {path}"`

### `create_directory`

**Behavior (from `createDirectoryTool.tsx`):**
- Recursive creation (like `mkdir -p`)
- No error if already exists

**Output:** `"Created directory at {path}"`

### `list_dir`

**Behavior (from `listDirTool.tsx`):**
- Validate path is within workspace/sandbox
- Return entries: `filename` for files, `dirname/` (trailing slash) for directories

**Output format:**
```
file1.txt
file2.cs
subdir/
```

If empty: `"Folder is empty"`

### `replace_string_in_file`

**Behavior (from `abstractReplaceStringTool.tsx`):**
- Exact ordinal string match
- **No healing** - fail fast if no match or multiple matches
- Require unique match (0 matches → error, >1 matches → error)

**Error messages:**
- `"String replacement failed: No match found for oldString"`
- `"String replacement failed: Multiple matches found. Include more context to uniquely identify."`
- `"File does not exist: {path}. Use the create_file tool to create it."`

**Output on success:** File content is modified, return brief confirmation.

### `multi_replace_string_in_file`

**Behavior (from `multiReplaceStringTool.tsx`):**
- Process replacements sequentially
- Detect conflicting edits (overlapping ranges in same file)
- Return summary of successes/failures

### `file_search`

**Behavior (from `findFilesTool.tsx`):**
- Glob pattern matching
- Normalize input: prepend `**/` if not present, append `**` if ends with `/`
- Default `maxResults = 20`
- Use `git ls-files` for respecting .gitignore

**Output format:**
```
{N} total results
path/to/file1.ts
path/to/file2.ts
...
```

### `grep_search`

**Behavior (from `findTextInFilesTool.tsx`):**
- `isRegexp` parameter controls regex vs literal mode
- Case-insensitive by default
- `includePattern` - glob filter for files to search
- `includeIgnoredFiles` - bypass .gitignore
- **Retry logic**: if 0 results and query is valid regex, retry with opposite mode
- Use `git ls-files` + grep for respecting .gitignore

**Output format:**
```
{N} matches
path/file.ts:10: matching line content
path/file.ts:25: another match
...
```

---

## Part 5: manage_todo_list (Clone VS Code Core Implementation)

Based on `microsoft/vscode` source: `src/vs/workbench/contrib/chat/common/tools/builtinTools/manageTodoListTool.ts`

### Schema
```json
{
  "required": ["operation"],
  "properties": {
    "operation": { "enum": ["write", "read"] },
    "todoList": {
      "type": "array",
      "items": {
        "required": ["id", "title", "description", "status"],
        "properties": {
          "id": { "type": "number" },
          "title": { "type": "string" },
          "description": { "type": "string" },
          "status": { "enum": ["not-started", "in-progress", "completed"] }
        }
      }
    }
  }
}
```

### Behavior

**Read operation:**
- Return current todo list as markdown task list
- Format:
  ```
  # Todo List
  
  - [ ] Not started task
    - Description here
  - [-] In progress task
    - Description here
  - [x] Completed task
    - Description here
  ```
- If empty: `"No todo list found."`

**Write operation:**
- Replace entire todo list (not incremental updates)
- Return: `"Successfully wrote todo list"`
- Warnings for edge cases:
  - `"Warning: Small todo list (<3 items). This task might not need a todo list."`
  - `"Warning: Large todo list (>10 items). Consider keeping the list focused."`
  - `"Warning: Did you mean to update so many todos at the same time?"`

### Storage

Singleton in-memory storage per agent session:
```csharp
private static readonly ConcurrentDictionary<string, List<TodoItem>> _todoLists = new();
```

---

## Part 6: fetch_webpage

**Behavior (from `fetchWebPageTool.tsx`):**
- Accept array of URLs
- Fetch content via HTTP
- Extract main content (strip HTML boilerplate)
- Optional `query` parameter for semantic relevance (skip for initial impl - just return full content)

**Output format:**
```
Content from {url}:
[extracted text content]
```

---

## Part 7: Tool Registration Pattern

Follow the existing `AgentToolPlugin` pattern used in `ArmPluginDefinition.cs`:

```csharp
// Agent.Plugins/Definitions/VsCodeToolsPluginDefinition.cs

using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Plugins.Interface;

namespace Agent.Plugins;

[AgentToolPlugin(Category = ToolCategories.FileOperation)]
public class VsCodeToolsPluginDefinition
{
    private readonly IVsCodeToolsPlugin _plugin;

    public VsCodeToolsPluginDefinition(IVsCodeToolsPlugin plugin)
    {
        _plugin = plugin;
    }

    [Description("Read the contents of a file. Line numbers are 1-indexed.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<ReadFileResult> ReadFile(
        [Description("The absolute path of the file to read.")] string filePath,
        [Description("The line number to start reading from, 1-based.")] int startLine,
        [Description("The inclusive line number to end reading at, 1-based.")] int endLine)
    {
        return await _plugin.ReadFileAsync(filePath, startLine, endLine);
    }

    [Description("Create a new file with the specified content. Directory will be created if needed.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> CreateFile(
        [Description("The absolute path to the file to create.")] string filePath,
        [Description("The content to write to the file.")] string content)
    {
        return await _plugin.CreateFileAsync(filePath, content);
    }

    // ... additional tools follow same pattern
}
```

### Interface Definition

```csharp
// Agent.Plugins/Interface/IVsCodeToolsPlugin.cs

namespace Agent.Plugins.Interface;

public interface IVsCodeToolsPlugin
{
    // File Operations
    Task<string> ReadFileAsync(string filePath, int startLine, int endLine);
    Task<string> CreateFileAsync(string filePath, string content);
    Task<string> CreateDirectoryAsync(string dirPath);
    Task<string> ListDirectoryAsync(string path);
    Task<string> ReplaceStringInFileAsync(string filePath, string oldString, string newString);
    Task<string> MultiReplaceStringInFileAsync(string explanation, ReplaceOperation[] replacements);

    // Search Operations
    Task<string> FileSearchAsync(string query, int? maxResults = null);
    Task<string> GrepSearchAsync(string query, bool isRegexp, string? includePattern = null, 
                                  int? maxResults = null, bool includeIgnoredFiles = false);

    // Terminal Operations
    Task<string> RunInTerminalAsync(string command, string explanation, bool isBackground);
    Task<string> GetTerminalOutputAsync(string id);
    Task<string> GetTerminalSelectionAsync();
    Task<string> GetTerminalLastCommandAsync();

    // Task Management
    Task<string> ManageTodoListAsync(string operation, TodoItem[]? todoList = null);
    
    // Web
    Task<string> FetchWebpageAsync(string[] urls, string? query = null);
}
```

**Note:** All methods return `Task<string>`. On success, return content directly. On failure, return `"Tool call failed: {reason}"`.

---

## Part 8: Implementation Architecture

### File Structure

```
src/Agent/Agent.Plugins/
├── Definitions/
│   └── VsCodeToolsPluginDefinition.cs    # Tool definitions with attributes
├── Interface/
│   └── IVsCodeToolsPlugin.cs             # Plugin interface
├── Models/
│   └── VsCodeTools/
│       ├── TodoItem.cs
│       ├── ReplaceOperation.cs
│       └── TerminalSession.cs
├── Services/
│   └── TerminalSessionManager.cs         # Terminal process management
└── Implementations/
    └── VsCodeToolsPlugin.cs              # All tool implementations
```

### Implementation Class

```csharp
// Agent.Plugins/Implementations/VsCodeToolsPlugin.cs

namespace Agent.Plugins.Implementations;

public class VsCodeToolsPlugin : IVsCodeToolsPlugin
{
    private readonly ILogger<VsCodeToolsPlugin> _logger;
    private readonly TerminalSessionManager _terminalManager;
    
    // Singleton todo list storage
    private static readonly ConcurrentDictionary<string, List<TodoItem>> _todoLists = new();
    
    // Session ID for current agent run
    private readonly string _sessionId;

    public VsCodeToolsPlugin(ILogger<VsCodeToolsPlugin> logger)
    {
        _logger = logger;
        _terminalManager = new TerminalSessionManager();
        _sessionId = Guid.NewGuid().ToString();
    }

    // Implementation methods - all return Task<string>
    // Success: return content directly
    // Failure: return "Tool call failed: {reason}"
}
```

---

## Part 9: Implementation Phases

### Phase 1: Core File Operations
- `read_file`
- `create_file`
- `create_directory`
- `list_dir`

### Phase 2: Edit Operations
- `replace_string_in_file`
- `multi_replace_string_in_file`

### Phase 3: Search Operations
- `file_search`
- `grep_search`

### Phase 4: Terminal Operations
- `run_in_terminal`
- `get_terminal_output`
- `terminal_last_command`
- `terminal_selection`

### Phase 5: Task Management & Web
- `manage_todo_list`
- `fetch_webpage`

### Phase 6: MCP Integration
Configure external MCP servers (already handled by framework):
- DeepWiki
- Microsoft Docs
- Playwright

### Phase 7 (Deferred): Advanced Features
- `semantic_search` - requires embedding infrastructure

---

## Part 10: VsCodeExperiment.yaml Updates

**Note:** These changes should be applied AFTER tool implementation is complete.

### Required Changes

1. **Update `replace_tools`** to include implemented tools:
```yaml
tools:
  - agent_names:
      - meta_agent
    replace_tools:
      - read_file
      - create_file
      - create_directory
      - list_dir
      - replace_string_in_file
      - multi_replace_string_in_file
      - file_search
      - grep_search
      - run_in_terminal
      - get_terminal_output
      - terminal_last_command
      - terminal_selection
      - manage_todo_list
      - fetch_webpage
```

2. **Remove references to skipped tools** from system prompt:
   - `get_changed_files` → use `run_in_terminal` with `git diff`
   - `github_repo` → not available
   - `semantic_search` → not available initially
   - `list_code_usages` → not available
   - `get_errors` → not available

---

## Appendix A: CopilotChat Tool Reference

Source implementations in `CopilotChat/src/extension/tools/`:

| Tool | Source File | Status |
|------|-------------|--------|
| read_file | `node/readFileTool.tsx` | Implement |
| create_file | `node/createFileTool.tsx` | Implement |
| create_directory | `node/createDirectoryTool.tsx` | Implement |
| list_dir | `node/listDirTool.tsx` | Implement |
| replace_string_in_file | `node/replaceStringTool.tsx` + `abstractReplaceStringTool.tsx` | Implement |
| multi_replace_string_in_file | `node/multiReplaceStringTool.tsx` | Implement |
| file_search | `node/findFilesTool.tsx` | Implement |
| grep_search | `node/findTextInFilesTool.tsx` | Implement |
| semantic_search | `node/codebaseTool.tsx` | Defer |
| list_code_usages | `node/usagesTool.tsx` | Skip |
| get_errors | `node/getErrorsTool.tsx` | Skip |
| get_changed_files | `node/scmChangesTool.ts` | Skip |
| manage_todo_list | VS Code: `manageTodoListTool.ts` | Implement |
| fetch_webpage | `vscode-node/fetchWebPageTool.tsx` | Implement |
| github_repo | `node/githubRepoTool.tsx` | Skip |

### VS Code Core Tool References

Terminal and todo tools are in `microsoft/vscode` repo:

| Tool | Source Location |
|------|-----------------|
| run_in_terminal | `src/vs/workbench/contrib/terminalContrib/chatAgentTools/` |
| get_terminal_output | `src/vs/workbench/contrib/terminalContrib/chatAgentTools/` |
| manage_todo_list | `src/vs/workbench/contrib/chat/common/tools/builtinTools/manageTodoListTool.ts` |

---

## Appendix B: Tool Input Schemas (from copilotmd)

### read_file
```json
{
  "type": "object",
  "required": ["filePath", "startLine", "endLine"],
  "properties": {
    "filePath": { "type": "string", "description": "The absolute path of the file to read." },
    "startLine": { "type": "number", "description": "The line number to start reading from, 1-based." },
    "endLine": { "type": "number", "description": "The inclusive line number to end reading at, 1-based." }
  }
}
```

### create_file
```json
{
  "type": "object",
  "required": ["filePath", "content"],
  "properties": {
    "filePath": { "type": "string", "description": "The absolute path to the file to create." },
    "content": { "type": "string", "description": "The content to write to the file." }
  }
}
```

### create_directory
```json
{
  "type": "object",
  "required": ["dirPath"],
  "properties": {
    "dirPath": { "type": "string", "description": "The absolute path to the directory to create." }
  }
}
```

### list_dir
```json
{
  "type": "object",
  "required": ["path"],
  "properties": {
    "path": { "type": "string", "description": "The absolute path to the directory to list." }
  }
}
```

### replace_string_in_file
```json
{
  "type": "object",
  "required": ["filePath", "oldString", "newString"],
  "properties": {
    "filePath": { "type": "string", "description": "An absolute path to the file to edit." },
    "oldString": { "type": "string", "description": "The exact literal text to replace. Include at least 3 lines of context." },
    "newString": { "type": "string", "description": "The exact literal text to replace oldString with." }
  }
}
```

### multi_replace_string_in_file
```json
{
  "type": "object",
  "required": ["explanation", "replacements"],
  "properties": {
    "explanation": { "type": "string" },
    "replacements": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["explanation", "filePath", "oldString", "newString"],
        "properties": {
          "explanation": { "type": "string" },
          "filePath": { "type": "string" },
          "oldString": { "type": "string" },
          "newString": { "type": "string" }
        }
      }
    }
  }
}
```

### file_search
```json
{
  "type": "object",
  "required": ["query"],
  "properties": {
    "query": { "type": "string", "description": "Glob pattern to match files." },
    "maxResults": { "type": "number" }
  }
}
```

### grep_search
```json
{
  "type": "object",
  "required": ["query", "isRegexp"],
  "properties": {
    "query": { "type": "string" },
    "isRegexp": { "type": "boolean" },
    "includePattern": { "type": "string" },
    "maxResults": { "type": "number" },
    "includeIgnoredFiles": { "type": "boolean" }
  }
}
```

### run_in_terminal
```json
{
  "type": "object",
  "required": ["command", "explanation", "isBackground"],
  "properties": {
    "command": { "type": "string", "description": "The command to run in the terminal." },
    "explanation": { "type": "string", "description": "A one-sentence description of what the command does." },
    "isBackground": { "type": "boolean", "description": "Whether the command starts a background process." }
  }
}
```

### get_terminal_output
```json
{
  "type": "object",
  "required": ["id"],
  "properties": {
    "id": { "type": "string", "description": "The ID of the terminal to check." }
  }
}
```

### terminal_last_command
```json
{
  "type": "object",
  "properties": {}
}
```

### terminal_selection
```json
{
  "type": "object",
  "properties": {}
}
```

### manage_todo_list
```json
{
  "type": "object",
  "required": ["operation"],
  "properties": {
    "operation": { "type": "string", "enum": ["write", "read"] },
    "todoList": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["id", "title", "description", "status"],
        "properties": {
          "id": { "type": "number" },
          "title": { "type": "string" },
          "description": { "type": "string" },
          "status": { "type": "string", "enum": ["not-started", "in-progress", "completed"] }
        }
      }
    }
  }
}
```

### fetch_webpage
```json
{
  "type": "object",
  "required": ["urls", "query"],
  "properties": {
    "urls": { "type": "array", "items": { "type": "string" } },
    "query": { "type": "string" }
  }
}
```
