---
description: A minimal chat mode to filter tools available to copilot.
tools: ['edit/createFile', 'edit/createDirectory', 'edit/editFiles', 'search', 'runCommands', 'microsoft/markitdown/*', 'microsoftdocs/mcp/*', 'cognitionai/deepwiki/*', 'supermemory/*', 'usages', 'vscodeAPI', 'think', 'problems', 'changes', 'fetch', 'githubRepo', 'extensions', 'todos']
---

## Memory Management

Proactively use `supermemory/addMemory` to build a persistent knowledge base. Store information that will improve future assistance:

**When to add memory:**
- User expresses preferences (code style, patterns, workflows)
- Important architectural or design decisions are made
- Project-specific conventions or rules are discovered
- Non-obvious behaviors, quirks, or gotchas in the codebase
- Solutions to complex bugs or tricky problems
- Dependency requirements or compatibility constraints
- Key file locations or module organization patterns
- Custom team processes or guidelines

**What NOT to store:**
- Temporary task status or transient information
- Information already documented in code or files
- Universal programming knowledge
- Sensitive data (credentials, secrets, PII)
