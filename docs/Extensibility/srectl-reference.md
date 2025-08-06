# SRECTL Command Reference

A comprehensive reference guide for all SRECTL commands, parameters, and usage patterns.

## Table of Contents
1. [Configuration Commands](#configuration-commands)
2. [Agent Commands](#agent-commands)
3. [Tool Commands](#tool-commands)
4. [Thread Management Commands](#thread-management-commands)
5. [List Commands](#list-commands)
6. [Utility Commands](#utility-commands)

---

## Configuration Commands

### `srectl init`

Initialize SRECTL configuration and create project structure.

**Syntax:**
```bash
srectl init --resource-url <ResourceURL>
```

**Parameters:**
- `--resource-url` (required): URL of the SRE Agent data plane API
  - **Local development:** `https://localhost:7023`
  - **Remote server:** `https://your-endpoint.azuresre.ai`

**What it does:**
- Creates `.sreagent-config.json` with server configuration
- Creates `agents/`, `tools/`, `connectors/` directories
- Adds example YAML files
- Tests server connection
- Determines authentication requirements

**Examples:**
```bash
srectl init --resource-url https://localhost:7023
srectl init --resource-url https://example.azuresre.ai
```

---

## Agent Commands

### `srectl agent create`

Create a new agent definition with comprehensive configuration options.

**Syntax:**
```bash
srectl agent create --name <AgentName> [options]
```

**Required Parameters:**
- `--name`: Unique agent name (no whitespace)

**Optional Parameters:**
- `--instructions`: System prompt/instructions for the agent
- `--tools`: Space-separated list of tools the agent can use
- `--handoff-description`: Description for handoff to this agent
- `--handoffs`: Space-separated list of agents this agent can handoff to
- `--allow-parallel-tool-calls`: Enable parallel tool execution
- `--max-reflection-count`: Maximum reflection iterations (default: 0)
- `--critic-prompt-path`: Path to critic prompt file
- `--critic-on-handoff`: Enable critic evaluation on handoff
- `--custom-reflection-note`: Custom reflection note text
- `--common-prompts`: Space-separated list of common prompts to include
- `--temperature`: Model temperature (0.0-2.0, default varies by model)
- `--output-type`: Expected output format for the agent
- `--smart`: Use AI to generate instructions and recommend tools

**Examples:**
```bash
# Minimal agent
srectl agent create --name simple_agent

# Fully configured agent
srectl agent create --name incident_agent \
  --instructions "Handle PagerDuty incidents with proper escalation" \
  --tools ResolvePagerDutyIncident AcknowledgePagerDutyIncident \
  --handoff-description "Use for PagerDuty incident management" \
  --handoffs meta_agent \
  --allow-parallel-tool-calls \
  --max-reflection-count 2 \
  --temperature 0.7

# AI-generated agent
srectl agent create --name DatabaseIssueAgent --smart \
  --instructions "Focus on PostgreSQL performance optimization"
```

### `srectl agent validate`

Validate agent YAML files for correctness and compliance.

**Syntax:**
```bash
srectl agent validate --file <path> | --all
```

**Parameters:**
- `--file`: Path to specific agent YAML file to validate
- `--all`: Validate all agent files in the `agents/` directory

**Validation checks:**
- Name: Non-empty, no whitespace
- Instructions: 50-5000 characters
- Tools: At least one tool, no empty names
- Handoffs: No empty/whitespace names
- Temperature: 0.0-2.0 range
- Max reflection count: Non-negative values
- Handoff description: Under 500 characters
- Common prompts: No empty names

**Examples:**
```bash
srectl agent validate --file agents/my_agent/my_agent.yaml
srectl agent validate --all
```

### `srectl agent apply`

Apply an agent configuration to the remote server.

**Syntax:**
```bash
srectl agent apply --name <AgentName>
```

**Parameters:**
- `--name` (required): Name of the agent to apply

**Prerequisites:**
- SRECTL must be initialized (`srectl init`)
- Agent YAML file must exist in `agents/` directory
- For remote servers: Azure CLI authentication (`az login`)

**Examples:**
```bash
srectl agent apply --name incident_agent
srectl agent apply --name example_agent
```

---

## Tool Commands

### `srectl tool create`

Create a new tool definition.

**Syntax:**
```bash
srectl tool create --name <ToolName> --type <ToolType> [--extra key value ...]
```

**Required Parameters:**
- `--name`: Unique tool name
- `--type`: Tool type/category

**Optional Parameters:**
- `--extra`: Additional key-value pairs for tool customization

**Special Features:**
- **KustoTool auto-generation**: Automatically creates comprehensive template with:
  - Default connector reference
  - Template KQL query with examples
  - Sample parameters with proper structure
  - Metadata with owner, version, tags

**Examples:**
```bash
# Simple tool
srectl tool create --name MyHttpTool --type HttpConnector

# KustoTool with auto-generation
srectl tool create --name GetServiceLogs --type KustoTool

# Tool with custom properties
srectl tool create --name QueryHealthPings --type KustoQuery \
  --extra version 1.0 description "Fetch worker health pings"
```

### `srectl tool validate`

Validate tool definitions.

**Syntax:**
```bash
srectl tool validate --name <ToolName> | --all
```

**Parameters:**
- `--name`: Specific tool name to validate
- `--all`: Validate all tools in the `tools/` directory

**Examples:**
```bash
srectl tool validate --name GetServiceLogs
srectl tool validate --all
```

### `srectl tool apply`

Apply a tool configuration to the remote server.

**Syntax:**
```bash
srectl tool apply --name <ToolName>
```

**Parameters:**
- `--name` (required): Name of the tool to apply

**Prerequisites:**
- SRECTL initialized
- Tool YAML file exists in `tools/` directory
- Authentication for remote servers

**Examples:**
```bash
srectl tool apply --name GetServiceLogs
```

### `srectl tool show-types`

Display available tool types that can be used when creating tools.

**Syntax:**
```bash
srectl tool show-types [--verbose] [--type <ToolTypeName>]
```

**Parameters:**
- `--verbose`: Show detailed information including assembly and namespace
- `--type`: Show detailed information for a specific tool type

**Examples:**
```bash
srectl tool show-types
srectl tool show-types --verbose
srectl tool show-types --type KustoTool
```

### `srectl tool show-connectors`

Display available connector types that can be referenced in tools.

**Syntax:**
```bash
srectl tool show-connectors [--verbose]
```

**Parameters:**
- `--verbose`: Show detailed information including assembly and namespace

**Examples:**
```bash
srectl tool show-connectors
srectl tool show-connectors --verbose
```

---

## Thread Management Commands

### `srectl thread new`

Create a new conversation thread and send an initial message.

**Syntax:**
```bash
srectl thread new --message "<your-question>"
```

**Parameters:**
- `--message` (required): Initial message to send to the agent

**What it does:**
- Creates new conversation thread on remote server
- Sends initial message to agent
- Displays agent response in real-time
- Stores thread ID for future reference

**Examples:**
```bash
srectl thread new --message "Help me troubleshoot a Redis container issue"
srectl thread new --message "What's the current system health status?"
```

### `srectl thread continue`

Continue an existing conversation thread with a follow-up message.

**Syntax:**
```bash
srectl thread continue --message "<follow-up-message>" [--thread-id <thread-id>]
```

**Parameters:**
- `--message` (required): Follow-up message to send
- `--thread-id` (optional): Specific thread ID to continue (defaults to most recent)

**Examples:**
```bash
srectl thread continue --message "The issue is still persisting"
srectl thread continue --thread-id thread_abc123 --message "Can you check again?"
```

### `srectl thread list`

Display all conversation threads.

**Syntax:**
```bash
srectl thread list
```

**What it displays:**
- Thread IDs and titles
- Creation dates and last activity timestamps
- Summary of each conversation
- Total thread count

**Example:**
```bash
srectl thread list
```

### `srectl thread delete`

Remove a specific conversation thread.

**Syntax:**
```bash
srectl thread delete --thread-id <thread-id>
```

**Parameters:**
- `--thread-id` (required): ID of the thread to delete

**Examples:**
```bash
srectl thread delete --thread-id thread_abc123
```

---

## List Commands

### `srectl list agents`

Retrieve and display all agents available on the remote server.

**Syntax:**
```bash
srectl list agents
```

**What it displays:**
- Agent names and descriptions
- Creation timestamps
- Associated tools and handoffs
- Total agent count

**Authentication:**
- Local servers: No authentication required
- Remote servers: Requires Azure CLI authentication

**Example:**
```bash
srectl list agents
```

### `srectl list tools`

Retrieve and display all tools available on the remote server.

**Syntax:**
```bash
srectl list tools
```

**What it displays:**
- Tool names, categories, and descriptions
- Plugin information
- Parameters and configuration
- Total tool count

**Example:**
```bash
srectl list tools
```

### `srectl list extended-tools`

Retrieve and display all extended tools that have been added to the server through the apply command. These are custom tools configured via YAML files.

**Syntax:**
```bash
srectl list extended-tools
```

**What it displays:**
- Extended tool names, types, and descriptions  
- Creation and update timestamps
- Parameters and configuration
- Connector information
- Total extended tool count with pagination

**Example:**
```bash
srectl list extended-tools
```

---

## Utility Commands

### `srectl apply-yaml`

Apply any YAML file directly to the remote server.

**Syntax:**
```bash
srectl apply-yaml --file <path-to-yaml-file>
```

**Parameters:**
- `--file` (required): Path to YAML file to apply

**Use cases:**
- Advanced configuration scenarios
- Bulk configuration updates
- Custom YAML structures

**Examples:**
```bash
srectl apply-yaml --file custom-config.yaml
```

---

## Common Parameter Patterns

### File Paths
- Use relative paths from current directory
- Agent files: `agents/<AgentName>/<AgentName>.yaml`
- Tool files: `tools/<ToolName>/<ToolName>.yaml`

### Naming Conventions
- **Agent names**: No whitespace, use underscore or camelCase
- **Tool names**: No whitespace, descriptive names
- **YAML properties**: Generated in snake_case format

### List Parameters
Multiple values can be specified as:
- Space-separated: `--tools Tool1 Tool2 Tool3`
- Comma-separated: `--tools Tool1,Tool2,Tool3`

### Boolean Flags
Boolean parameters are flags that don't require values:
- `--allow-parallel-tool-calls` (sets to true)
- `--critic-on-handoff` (sets to true)
- `--smart` (enables AI generation)

### Numeric Parameters
- `--temperature`: Float between 0.0 and 2.0
- `--max-reflection-count`: Non-negative integer

---

## Exit Codes

SRECTL returns appropriate exit codes for automation:
- **0**: Success
- **1**: General error (validation failure, file not found, etc.)
- **2**: Authentication error
- **3**: Network/connection error

---

## Configuration File

SRECTL stores configuration in `.sreagent-config.json`:
```json
{
  "resourceUrl": "https://your-endpoint.azuresre.ai",
  "authRequired": true
}
```

---

## Global Options

All commands support:
- `--help`: Display command-specific help
- `--version`: Display SRECTL version information

**Examples:**
```bash
srectl --help
srectl agent create --help
srectl --version
```
