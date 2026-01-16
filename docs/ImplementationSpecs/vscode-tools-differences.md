# VS Code Agent Tools - Implementation Differences & Enhancements

This document tracks differences between the VS Code (TypeScript/Node.js) implementations and the SRE Agent Runtime (C#) implementations, along with potential future enhancements.

## fetch_webpage Tool

### Implementation Comparison

| Aspect | VS Code (TypeScript) | SRE Agent (C#) | Notes |
|--------|---------------------|----------------|-------|
| **Web content extraction** | Electron BrowserWindow with Accessibility tree/DOM extraction | HtmlAgilityPack with HTTP fetch | Electron enables JS rendering; HtmlAgilityPack is server-side appropriate |
| **URI scheme support** | `http://`, `https://`, `file://`, `mcp-resource://`, custom schemes | `http://`, `https://` only | File scheme support could be added |
| **Image handling** | Detects PNG, JPEG, GIF, WebP, BMP; returns as data parts | Not supported | Could add image download capability |
| **Binary detection** | Uses `detectEncodingFromBuffer()` | Not implemented | Could add binary detection |
| **Invalid URL message** | `'Invalid URL'` | `'Error fetching page: {message}'` | Minor difference |
| **Empty URLs message** | `'No valid URLs provided.'` | `'Tool call failed: No URLs provided'` | Follows our error convention |
| **Output format** | Structured `{ kind: 'text', title, value }` objects | Plain text `Content from {url}:\n{content}` | Simplified for text-based output |
| **Redirect handling** | Detects redirects, suggests retry | Not handled explicitly | HttpClient follows redirects automatically |
| **Query parameter** | Used for semantic search via `UrlChunkEmbeddingsIndex` | Not used | Could add content filtering |
| **Content truncation** | Via prompt-tsx priority system | 10KB limit | Both approaches valid |

### VS Code Source Files

- **Wrapper tool**: `src/extension/tools/vscode-node/fetchWebPageTool.tsx`
- **Internal tool**: `src/vs/workbench/contrib/chat/electron-browser/builtInTools/fetchPageTool.ts`
- **Web loader**: `src/vs/platform/webContentExtractor/electron-main/webPageLoader.ts`

### Content Extraction Logic

**VS Code approach:**
1. Load page in hidden Electron BrowserWindow
2. Wait for network idle (debounced)
3. Extract via Accessibility tree first
4. Fall back to main DOM element extraction if content < 100 chars
5. Timeouts: 30s overall, 5s post-load, 0.5s idle debounce

**SRE Agent approach:**
1. HTTP GET request via HttpClient
2. Parse HTML with HtmlAgilityPack
3. Remove script, style, nav, header, footer, aside elements
4. Find main content: `<main>` → `<article>` → `#content` → `.content` → `<body>`
5. Extract inner text, normalize whitespace, truncate at 10KB

### Potential Enhancements

1. **File URI Support**
   - Add `file://` scheme handling for local file access within sandbox
   - Validate paths are within sandbox root

2. **Query-based Content Filtering**
   - Use the `query` parameter to filter/rank content sections
   - Could use simple keyword matching or integrate with existing semantic search

3. **Better Error Messages**
   - Distinguish between network errors, parsing errors, and invalid URLs
   - Match VS Code's structured error format

4. **Redirect Detection**
   - Detect when HttpClient followed redirects
   - Report final URL in output

5. **Image Support**
   - Detect image URLs by extension/content-type
   - Return base64-encoded data or save to temp file

6. **Content Type Detection**
   - Check `Content-Type` header before parsing as HTML
   - Handle JSON, XML, plain text appropriately

7. **Timeout Configuration**
   - Add configurable timeout (currently uses HttpClient defaults)
   - Match VS Code's 30-second timeout

---

## manage_todo_list Tool

### Implementation Status: ✅ Matches VS Code

The implementation closely follows the VS Code source at:
`src/vs/workbench/contrib/chat/common/tools/builtinTools/manageTodoListTool.ts`

**Matched behaviors:**
- Read operation returns `"No todo list found."` for empty list
- Markdown format: `[x]` completed, `[-]` in-progress, `[ ]` not-started
- Description indented with `  - `
- Warnings for small list (<3), large list (>10), many changes (>3)
- Index-by-index change calculation algorithm
- `todoList` required for write operation

---

## Terminal Tools (run_in_terminal, get_terminal_output)

### Implementation Comparison

| Aspect | VS Code | SRE Agent | Notes |
|--------|---------|-----------|-------|
| **Shell** | PowerShell (Windows), bash (Linux/Mac) | bash only (Linux target) | Matches spec |
| **Session persistence** | VS Code terminal API | TerminalSessionManager with Process | Custom implementation |
| **Background processes** | Native terminal background | 2-second wait, then return ID | Matches spec |
| **Idle timeout** | None (terminal persists) | 5-minute idle cleanup | Added for resource management |
| **Output buffering** | VS Code terminal buffer | StringBuilder with 60KB limit | Matches spec limit |

### Potential Enhancements

1. **PowerShell Support for Windows**
   - Currently throws `NotImplementedException` on Windows
   - Could add PowerShell process management

2. **Working Directory Tracking**
   - Track `cd` commands to update working directory
   - Return in session info

3. **Environment Variable Capture**
   - Capture environment changes between commands
   - Persist across sessions

---

## File Operation Tools

### Implementation Status: ✅ Complete

Tools implemented: `read_file`, `create_file`, `create_directory`, `list_dir`, `replace_string_in_file`, `multi_replace_string_in_file`, `file_search`, `grep_search`

**Key behaviors matched:**
- Sandbox validation for all paths
- Line number 1-indexed
- 2000-line read limit
- Glob pattern matching via `Microsoft.Extensions.FileSystemGlobbing`
- Regex support in grep_search
- Ordinal string comparison

---

## Version History

| Date | Changes |
|------|---------|
| 2026-01-02 | Initial implementation and comparison |
