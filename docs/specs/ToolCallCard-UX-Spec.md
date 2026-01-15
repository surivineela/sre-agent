# Tool Call Card UX Specification

## Overview

This spec defines a unified `ToolCallCard` component for displaying tool execution results in the chat interface. The design follows VS Code Copilot's pattern: **collapsed by default, expandable on click**, with minimal visual footprint.

## Design Decisions (Gathered from User)

| Decision | Choice |
|----------|--------|
| Grep + ReadFile styling | Same unified ToolCallCard component |
| MCP tools | Keep separate McpToolExecutionMessage (unchanged) |
| RunInTerminal | Uses ToolCallCard (same as Grep/ReadFile, no approval flow) |
| Background commands | Show "Started in background" text, no spinner |
| Summary line format | Tool icon + action + key param (minimal) |
| Loading state | Summary line with spinner |
| Auto-collapse | Yes, previous tool calls auto-collapse when new ones appear |
| Terminal output | Plain monospace text (no ANSI parsing) |

---

## Architecture

### Component Hierarchy

```
AgentMessage.tsx
├── McpToolExecutionMessage.tsx  (unchanged - MCP tools)
├── ToolCallCard.tsx             (NEW - unified component)
│   ├── GrepToolContent.tsx      (content renderer for grep)
│   ├── ReadFileToolContent.tsx  (content renderer for file read)
│   └── TerminalToolContent.tsx  (content renderer for terminal)
└── ... (other existing message types)
```

### Data Flow

```
Backend Tool Execution
        ↓
OutboundCommunicationService (AppendAgentXxxMessage)
        ↓
SignalR Stream (StreamMessageType.ReadFile / Terminal)
        ↓
Frontend StreamingProvider
        ↓
createChatMessageFromStreamingMessage (parse JSON)
        ↓
ChatMessage object with toolCallResult property
        ↓
AgentMessage → ToolCallCard
        ↓
Specific content renderer (GrepToolContent, ReadFileToolContent, etc.)
```

---

## Data Structures

### Backend (C#)

#### New StreamMessageType values

```csharp
// Add to StreamMessageType.cs
public enum StreamMessageType
{
    // ... existing values ...

    /// <summary>
    /// File read results with file content preview
    /// </summary>
    ReadFile,

    /// <summary>
    /// Terminal command execution results
    /// </summary>
    Terminal,
}
```

#### ReadFileResult (new model)

```csharp
// Agent.Core/Models/Api/v1/ReadFileResult.cs
public class ReadFileResult
{
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int TotalLines { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsTruncated { get; set; }
    public string? Error { get; set; }
}
```

#### TerminalExecutionResult (new model)

```csharp
// Agent.Core/Models/Api/v1/TerminalExecutionResult.cs
public class TerminalExecutionResult
{
    public string Command { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public bool IsBackground { get; set; }
    public string? SessionId { get; set; }  // For background commands
    public int? ExitCode { get; set; }       // null for background
    public string? Output { get; set; }
    public string? Error { get; set; }
    public TerminalStatus Status { get; set; }
}

public enum TerminalStatus
{
    Running,
    Completed,
    Failed,
    Background  // Started in background, no result yet
}
```

### Frontend (TypeScript)

#### ReadFileResult type

```typescript
// Common/Contracts/DataPlane/ReadFileResult.ts
export interface ReadFileResult {
    filePath: string;
    startLine: number;
    endLine: number;
    totalLines: number;
    content: string;
    isTruncated: boolean;
    error?: string;
}
```

#### TerminalExecutionResult type

```typescript
// Common/Contracts/DataPlane/TerminalExecutionResult.ts
export type TerminalStatus = 'Running' | 'Completed' | 'Failed' | 'Background';

export interface TerminalExecutionResult {
    command: string;
    explanation?: string;
    isBackground: boolean;
    sessionId?: string;
    exitCode?: number;
    output?: string;
    error?: string;
    status: TerminalStatus;
}
```

#### Updated Message interface

```typescript
// Add to Message.ts
export type MessageType =
    | 'chart'
    // ... existing types ...
    | 'grepsearch'
    | 'readfile'      // NEW
    | 'terminal'      // NEW
    | 'mcptool'
    | null;

export interface Message {
    // ... existing fields ...
    grepSearchResult?: GrepSearchResult | null;
    readFileResult?: ReadFileResult | null;      // NEW
    terminalResult?: TerminalExecutionResult | null;  // NEW
    mcpToolExecution?: McpToolExecution | null;
}
```

---

## Component Specification

### ToolCallCard (unified component)

#### Props

```typescript
interface ToolCallCardProps {
    // Discriminated union for tool types
    toolType: 'grep' | 'readfile' | 'terminal';

    // Tool-specific data (only one will be set based on toolType)
    grepResult?: GrepSearchResult;
    readFileResult?: ReadFileResult;
    terminalResult?: TerminalExecutionResult;

    // Expansion state management (for auto-collapse)
    isExpanded?: boolean;
    onExpandChange?: (expanded: boolean) => void;

    // Loading state (while tool is executing)
    isLoading?: boolean;
}
```

#### Visual States

```
┌─────────────────────────────────────────────────────────────┐
│ COLLAPSED STATE (default)                                   │
│                                                             │
│  ▸ 🔍 Searched for "error" · 5 matches                     │
│  ▸ 📄 Read src/components/App.tsx · lines 1-50            │
│  ▸ ⚙️ Ran npm install · exit 0                            │
│  ↻ 📄 Reading src/utils.ts...                (loading)    │
│                                                             │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ EXPANDED STATE (on click)                                   │
│                                                             │
│  ▾ 🔍 Searched for "error" · 5 matches           [Copy]    │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ src/utils/logger.ts (2 matches)               [Copy]│   │
│  │  ▸ (collapsed file - click to expand)              │   │
│  │ src/components/Error.tsx (3 matches)          [Copy]│   │
│  │  ▸ (collapsed file - click to expand)              │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

#### Summary Line Format by Tool Type

| Tool | Icon | Summary Text |
|------|------|--------------|
| Grep | 🔍 | `Searched for "{query}" · {count} matches` |
| Grep (no results) | 🔍 | `Searched for "{query}" · no matches` |
| ReadFile | 📄 | `Read {filename} · lines {start}-{end}` |
| ReadFile (full) | 📄 | `Read {filename} · {totalLines} lines` |
| ReadFile (error) | 📄 | `Read {filename} · error` |
| Terminal | ⚙️ | `Ran {command (truncated to 40ch)} · exit {code}` |
| Terminal (bg) | ⚙️ | `Started {command (truncated)} in background` |
| Terminal (error) | ⚙️ | `Ran {command} · failed` |

---

## Styling Specification

### Colors & Tokens (Fluent UI)

```typescript
const useStyles = makeStyles({
    // Collapsed summary line
    summaryLine: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        padding: '4px 0',
        cursor: 'pointer',
        color: tokens.colorNeutralForeground3,
        fontSize: '13px',
        ':hover': {
            color: tokens.colorNeutralForeground2,
        },
    },

    // Icon styling
    toolIcon: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
        fontSize: '16px',
    },

    // Chevron for expand/collapse
    chevron: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
    },

    // Spinner for loading state
    spinner: {
        marginRight: '4px',
    },

    // Key param (filename, query, command)
    keyParam: {
        fontFamily: 'Consolas, Monaco, monospace',
        color: tokens.colorNeutralForeground2,
    },

    // Result count / status
    resultInfo: {
        color: tokens.colorNeutralForeground4,
    },

    // Expanded container
    expandedContainer: {
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground1,
        overflow: 'hidden',
        marginTop: '4px',
    },

    // Code content area
    codeContainer: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        lineHeight: '18px',
        backgroundColor: tokens.colorNeutralBackground2,
    },

    // Line numbers
    lineNumber: {
        minWidth: '48px',
        padding: '0 8px',
        textAlign: 'right',
        color: tokens.colorNeutralForeground4,
        userSelect: 'none',
        borderRight: `1px solid ${tokens.colorNeutralStroke3}`,
    },

    // Terminal output styling
    terminalOutput: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        backgroundColor: tokens.colorNeutralBackground2,
        padding: '12px',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        maxHeight: '300px',
        overflow: 'auto',
    },

    // Error styling
    errorText: {
        color: tokens.colorPaletteRedForeground1,
    },
});
```

---

## Content Renderers

### GrepToolContent

Reuses existing `GrepResultMessage` rendering logic:
- Collapsible file list
- Line numbers with match highlighting
- Context lines in gray, match lines in black
- Copy buttons at file and global level

### ReadFileToolContent

```typescript
interface ReadFileToolContentProps {
    result: ReadFileResult;
}

// Display:
// - File header: {filePath} (lines {start}-{end} of {total})
// - Numbered code lines
// - Truncation indicator if isTruncated
// - Error message if error
// - Copy button
```

Visual mockup:
```
┌────────────────────────────────────────────────────────┐
│ src/components/App.tsx (lines 1-50 of 234)      [Copy] │
├────────────────────────────────────────────────────────┤
│  1 │ import React from 'react';                        │
│  2 │ import { useState } from 'react';                 │
│  3 │                                                   │
│  4 │ export const App = () => {                        │
│  5 │   const [count, setCount] = useState(0);          │
│ ...│ ...                                               │
│ 50 │   return <div>{count}</div>;                      │
├────────────────────────────────────────────────────────┤
│ (Truncated. Request more lines with offset parameter.) │
└────────────────────────────────────────────────────────┘
```

### TerminalToolContent

```typescript
interface TerminalToolContentProps {
    result: TerminalExecutionResult;
}

// Display:
// - Command header with exit code badge
// - Output in monospace
// - Error in red if present
// - "Started in background" for background commands
// - Copy button
```

Visual mockups:

**Foreground command (completed):**
```
┌────────────────────────────────────────────────────────┐
│ npm install                              [Exit: 0] [Copy] │
├────────────────────────────────────────────────────────┤
│ added 150 packages in 2.5s                             │
│                                                        │
│ 5 packages are looking for funding                     │
│   run `npm fund` for details                           │
└────────────────────────────────────────────────────────┘
```

**Background command:**
```
┌────────────────────────────────────────────────────────┐
│ npm run dev                               [Background] │
├────────────────────────────────────────────────────────┤
│ Started in background (session: abc123)                │
│ Use terminal state to check output later.              │
└────────────────────────────────────────────────────────┘
```

**Failed command:**
```
┌────────────────────────────────────────────────────────┐
│ npm test                            [Exit: 1] [Copy]   │
├────────────────────────────────────────────────────────┤
│ FAIL src/App.test.tsx                                  │
│   ✕ renders learn react link (5ms)                     │
├────────────────────────────────────────────────────────┤
│ Error: Test failed                                     │
└────────────────────────────────────────────────────────┘
```

---

## Backend Implementation Changes

### 1. Update VsCodeToolsPluginDefinition.cs

Add streaming calls for ReadFile and RunInTerminal:

```csharp
[AgentTool(ToolMode.Auto)]
public async Task<string> ReadFile(string filePath, int startLine = 1, int endLine = int.MaxValue)
{
    var result = await _plugin.ReadFileAsync(filePath, startLine, endLine);

    // Stream to frontend for rich rendering
    var threadId = Core.ToolStatic.AsyncLocalThreadId.Value;
    if (threadId != Guid.Empty)
    {
        var readFileResult = ParseReadFileResult(result, filePath, startLine, endLine);
        await _communicationService.AppendAgentReadFileMessage(threadId, readFileResult);
    }

    return result;
}

[AgentTool(ToolMode.Auto)]
public async Task<string> RunInTerminal(string command, string explanation, bool isBackground = false)
{
    var result = await _plugin.RunInTerminalAsync(command, explanation, isBackground);

    // Stream to frontend for rich rendering
    var threadId = Core.ToolStatic.AsyncLocalThreadId.Value;
    if (threadId != Guid.Empty)
    {
        var terminalResult = ParseTerminalResult(result, command, explanation, isBackground);
        await _communicationService.AppendAgentTerminalMessage(threadId, terminalResult);
    }

    return result;
}
```

### 2. Update OutboundCommunicationService.cs

Add new streaming methods:

```csharp
public async Task<Guid> AppendAgentReadFileMessage(Guid threadId, ReadFileResult readFileResult, Guid messageId = default)
{
    // Similar pattern to AppendAgentGrepSearchMessage
    var jsonString = JsonSerializer.Serialize(readFileResult, _serializerOptions);
    await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.ReadFile, messageId);

    var messageText = $"Read file: {readFileResult.FilePath} (lines {readFileResult.StartLine}-{readFileResult.EndLine})";
    return await _sinkService.SinkAgentReadFileMessageAsync(threadId, messageText, messageId, readFileResult);
}

public async Task<Guid> AppendAgentTerminalMessage(Guid threadId, TerminalExecutionResult terminalResult, Guid messageId = default)
{
    var jsonString = JsonSerializer.Serialize(terminalResult, _serializerOptions);
    await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.Terminal, messageId);

    var statusText = terminalResult.IsBackground ? "background" : $"exit {terminalResult.ExitCode}";
    var messageText = $"Terminal: {terminalResult.Command} ({statusText})";
    return await _sinkService.SinkAgentTerminalMessageAsync(threadId, messageText, messageId, terminalResult);
}
```

### 3. Update Message record

Add new properties to the Message record:

```csharp
public record Message(
    // ... existing properties ...
    GrepSearchResult? GrepSearchResult = null,
    ReadFileResult? ReadFileResult = null,        // NEW
    TerminalExecutionResult? TerminalResult = null, // NEW
    // ... rest of properties ...
);
```

---

## Frontend Implementation Changes

### 1. Update Utility.tsx

Add parsing for new message types:

```typescript
// In createChatMessageFromStreamingMessage()
case 'readfile':
    readFileResult = getSpecialMessageContentFromStreamingMessage<ReadFileResult>(streamingMessage);
    break;
case 'terminal':
    terminalResult = getSpecialMessageContentFromStreamingMessage<TerminalExecutionResult>(streamingMessage);
    break;
```

### 2. Update AgentMessage.tsx

Add routing for new message types:

```typescript
// In AgentMessage component
} : message.grepSearchResult ? (
    <ToolCallCard toolType="grep" grepResult={message.grepSearchResult} />
) : message.readFileResult ? (
    <ToolCallCard toolType="readfile" readFileResult={message.readFileResult} />
) : message.terminalResult ? (
    <ToolCallCard toolType="terminal" terminalResult={message.terminalResult} />
) : message.mcpToolExecution ? (
    <McpToolExecutionMessage execution={message.mcpToolExecution} />
) : ...
```

### 3. Create ToolCallCard.tsx

New unified component that delegates to content renderers:

```typescript
const ToolCallCard = ({ toolType, grepResult, readFileResult, terminalResult, isLoading }: ToolCallCardProps) => {
    const [isExpanded, setIsExpanded] = useState(false);

    const getSummaryContent = () => {
        switch (toolType) {
            case 'grep':
                return {
                    icon: <Search16Regular />,
                    text: `Searched for "${grepResult!.query}"`,
                    detail: grepResult!.totalMatches > 0
                        ? `${grepResult!.totalMatches} matches`
                        : 'no matches',
                };
            case 'readfile':
                return {
                    icon: <Document16Regular />,
                    text: `Read ${getFileName(readFileResult!.filePath)}`,
                    detail: `lines ${readFileResult!.startLine}-${readFileResult!.endLine}`,
                };
            case 'terminal':
                return {
                    icon: <Terminal16Regular />,
                    text: `Ran ${truncate(terminalResult!.command, 40)}`,
                    detail: terminalResult!.isBackground
                        ? 'background'
                        : `exit ${terminalResult!.exitCode}`,
                };
        }
    };

    const renderContent = () => {
        switch (toolType) {
            case 'grep':
                return <GrepToolContent result={grepResult!} />;
            case 'readfile':
                return <ReadFileToolContent result={readFileResult!} />;
            case 'terminal':
                return <TerminalToolContent result={terminalResult!} />;
        }
    };

    return (
        <div className={classes.root}>
            <SummaryLine
                {...getSummaryContent()}
                isExpanded={isExpanded}
                isLoading={isLoading}
                onClick={() => setIsExpanded(!isExpanded)}
            />
            {isExpanded && (
                <div className={classes.expandedContainer}>
                    {renderContent()}
                </div>
            )}
        </div>
    );
};
```

---

## Auto-Collapse Behavior

To implement auto-collapse when new tool calls appear, modify `useChatBox.ts`:

```typescript
// Track which tool cards are expanded
const [expandedToolIds, setExpandedToolIds] = useState<Set<string>>(new Set());

// When new tool message arrives, collapse previous ones
useEffect(() => {
    if (newToolMessage) {
        setExpandedToolIds(new Set([newToolMessage.id]));
    }
}, [newToolMessage]);

// Pass expansion state to ToolCallCard
<ToolCallCard
    isExpanded={expandedToolIds.has(message.id)}
    onExpandChange={(expanded) => {
        if (expanded) {
            setExpandedToolIds(new Set([message.id]));
        } else {
            setExpandedToolIds(prev => {
                const next = new Set(prev);
                next.delete(message.id);
                return next;
            });
        }
    }}
/>
```

---

## Refactoring: Extract GrepToolContent from GrepResultMessage

The existing `GrepResultMessage` component contains both the summary line and the expanded content. Refactor to extract:

1. **GrepToolContent.tsx** - Just the expanded content (file list, code preview)
2. **ToolCallCard.tsx** - Generic wrapper with summary line logic

This allows `ToolCallCard` to reuse the same summary line pattern while delegating content rendering to specialized components.

---

## File Structure

```
src/Space/Components/
├── ToolCallCard/
│   ├── ToolCallCard.tsx           # Main unified component
│   ├── SummaryLine.tsx            # Reusable summary line
│   ├── GrepToolContent.tsx        # Grep-specific content
│   ├── ReadFileToolContent.tsx    # ReadFile-specific content
│   ├── TerminalToolContent.tsx    # Terminal-specific content
│   └── types.ts                   # Shared types
├── McpToolExecutionMessage.tsx    # Unchanged
├── GrepResultMessage.tsx          # Deprecated, migrate to ToolCallCard
└── ...
```

---

## Migration Plan

### Phase 1: Backend (No UI changes)
1. Add `ReadFileResult` and `TerminalExecutionResult` models
2. Add `StreamMessageType.ReadFile` and `StreamMessageType.Terminal`
3. Update `OutboundCommunicationService` with new streaming methods
4. Update tool definitions to call streaming methods
5. Update `Message` record with new properties

### Phase 2: Frontend Data Layer
1. Add TypeScript types for `ReadFileResult` and `TerminalExecutionResult`
2. Update `MessageType` union
3. Update `Message` interface
4. Update `createChatMessageFromStreamingMessage` to parse new types

### Phase 3: UI Components
1. Create `ToolCallCard` component structure
2. Extract `GrepToolContent` from `GrepResultMessage`
3. Create `ReadFileToolContent`
4. Create `TerminalToolContent`
5. Update `AgentMessage` routing

### Phase 4: Polish
1. Implement auto-collapse behavior
2. Add loading states
3. Migrate old `GrepResultMessage` usage to `ToolCallCard`
4. Remove deprecated code

---

## References

- [VS Code Copilot Chat Tools](https://code.visualstudio.com/docs/copilot/chat/chat-tools) - Tool calls collapsed by default
- [Claude Code CLI](https://github.com/anthropics/claude-code) - Spinner animations, context visualization
- Existing `GrepResultMessage.tsx` - Visual pattern to follow
- Existing `McpToolExecutionMessage.tsx` - Card-based pattern (kept separate)
