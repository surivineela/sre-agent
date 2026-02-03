# SRECTL Workspace Commands

Manage workspace memory files for SRE Agent. These commands allow you to upload, download, and delete memory files that persist agent knowledge across sessions.

## Commands Overview

```bash
srectl workspace repo-instructions <upload|download|delete> [options]
srectl workspace session-insights <upload|download|delete> [options]
srectl workspace synthesized-knowledge <upload|download|delete> [options]
```

## Repo Instructions

Repository-specific instruction files stored in `.github/` folders. These contain custom instructions, prompts, or configuration for specific repositories.

```bash
# Upload repo instructions
srectl workspace repo-instructions upload --repo <repo-name> [--path <folder>]

# Download repo instructions
srectl workspace repo-instructions download --repo <repo-name> [--path <folder>]

# Delete repo instructions
srectl workspace repo-instructions delete --repo <repo-name>
```

| Option | Description |
|--------|-------------|
| `--repo` | Repository name (required) |
| `--path` | Local folder path. If provided, uses path directly. If omitted, uses default memory path with subfolders. |

**Examples:**
```bash
# Upload from default location
srectl workspace repo-instructions upload --repo my-service

# Upload from specific folder
srectl workspace repo-instructions upload --repo my-service --path ./custom-instructions

# Download to specific folder
srectl workspace repo-instructions download --repo my-service --path ./backup
```

## Session Insights

Session-specific insights and learnings captured during agent interactions. Organized by thread ID.

```bash
# Upload session insights
srectl workspace session-insights upload [--thread-id <guid>] [--path <folder>]

# Download session insights
srectl workspace session-insights download [--thread-id <guid>] [--path <folder>]

# Delete session insights
srectl workspace session-insights delete [--thread-id <guid>]
```

| Option | Description |
|--------|-------------|
| `--thread-id` | Thread ID (optional). If omitted, operates on all session insights. |
| `--path` | Local folder path. If provided, uses path directly. If omitted, uses default memory path with subfolders. |

**Examples:**
```bash
# Upload all session insights
srectl workspace session-insights upload

# Upload for specific thread
srectl workspace session-insights upload --thread-id 12345678-1234-1234-1234-123456789abc

# Download all session insights
srectl workspace session-insights download --path ./session-backup

# Delete specific thread's insights
srectl workspace session-insights delete --thread-id 12345678-1234-1234-1234-123456789abc
```

## Synthesized Knowledge

General knowledge synthesized by the agent from various sources. Not tied to specific repositories or sessions.

```bash
# Upload synthesized knowledge
srectl workspace synthesized-knowledge upload [--path <folder>]

# Download synthesized knowledge
srectl workspace synthesized-knowledge download [--path <folder>]

# Delete synthesized knowledge
srectl workspace synthesized-knowledge delete
```

| Option | Description |
|--------|-------------|
| `--path` | Local folder path. If provided, uses path directly. If omitted, uses default memory path with subfolders. |

**Examples:**
```bash
# Upload from default location
srectl workspace synthesized-knowledge upload

# Download to specific folder
srectl workspace synthesized-knowledge download --path ./knowledge-backup

# Delete all synthesized knowledge
srectl workspace synthesized-knowledge delete
```

## Storage Architecture

Files are stored in two locations:

1. **Local Storage**: Agent's local file system (`{MemoriesPath}/`)
2. **Blob Storage**: Azure Blob Storage for persistence across agent restarts

### Download Behavior

| Type | Local Missing Behavior |
|------|----------------------|
| Repo Instructions | Downloads from blob → saves locally → returns |
| Session Insights | Downloads from blob → returns directly (not saved locally) |
| Synthesized Knowledge | Downloads from blob → saves locally → returns |

### File Format

All transfers use **tar.gz** archives. The archive preserves the folder structure:

```
repo-instructions-{repo}.tar.gz
├── .github/
│   └── {repo}/
│       ├── instructions.md
│       └── config.yaml

session-insights.tar.gz
├── sessionInsights/
│   └── {thread-id}/
│       └── insights.json

synthesized-knowledge.tar.gz
├── synthesizedKnowledge/
│   └── knowledge.md
```

## Common Options

| Option | Description |
|--------|-------------|
| `--debug` | Enable debug logging to see HTTP requests/responses |
| `--help` | Show help for command |

## See Also

- [SRECTL Reference](srectl-reference.md) - Complete SRECTL command reference
- [Extending SRE Agent](extending-sre-agent-quickstart.md) - Quickstart guide for extending SRE Agent
