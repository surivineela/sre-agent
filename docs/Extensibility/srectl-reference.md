# SRECTL Command Reference

A comprehensive reference guide for all SRECTL commands, parameters, and usage patterns.

## Table of Contents
1. [Configuration Commands](#configuration-commands)
2. [Agent Commands](#agent-commands)
3. [Tool Commands](#tool-commands)
4. [Document Management Commands](#document-management-commands)
5. [Thread Management Commands](#thread-management-commands)
6. [Profile Management Commands](#profile-management-commands)
7. [List Commands](#list-commands)
8. [Utility Commands](#utility-commands)
9. [Incident Handler Commands](#incident-handler-commands)

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
srectl agent validate --file <path> | --name <AgentName> | --all
```

**Parameters:**
- `--file`: Path to specific agent YAML file to validate
- `--name`: Validate the agent matching the provided name (looks in `agents/<name>/<name>.yaml`)
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
srectl agent validate --name incident_agent
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

### `srectl agent test`

Test an agent with a specific message to verify its functionality and behavior.

**Syntax:**
```bash
srectl agent test --name <AgentName> --message "<test-message>" [options]
```

**Required Parameters:**
- `--name`: Name of the agent to test
- `--message`: The test message to send to the agent

**Optional Parameters:**
- `--user-id`: User ID for the test message (defaults to current user)
- `--display-name`: Display name for the test message (defaults to current user)
- `--no-wait`: Don't wait for the agent's response, just send the test message

**What it does:**
- Creates a new conversation thread
- Sends a specially formatted message: "Use the {AgentName} agent for the below user query\n{your-message}"
- Waits for the agent's response (unless `--no-wait` is specified)
- Provides the thread ID for continued interaction

**Use Cases:**
- Verify agent configuration and behavior
- Test agent responses to specific scenarios
- Debug agent interactions during development
- Validate agent handoff functionality

**Examples:**
```bash
# Basic agent test
srectl agent test --name DatabaseAgent --message "Help me optimize a slow query"

# Test without waiting for response
srectl agent test --name SecurityAgent --message "Check for vulnerabilities" --no-wait

# Test with custom user details
srectl agent test --name MonitoringAgent --message "What's the current system status?" \
  --user-id "test-user" --display-name "Test Engineer"
```

**Output:**
The command will display:
- Agent name being tested
- Original and formatted messages
- Thread creation status
- Agent response (if waiting)
- Thread ID for continuation

### `srectl agent delete`

Delete an agent from the remote server.

**Syntax:**
```bash
srectl agent delete --name <AgentName>
```

**Required Parameters:**
- `--name`: Name of the agent to delete

**Prerequisites:**
- SRECTL must be initialized (`srectl init`)
- Agent must exist on the remote server
- For remote servers: Azure CLI authentication (`az login`)

**What it does:**
- Validates that the agent exists on the server
- Checks for any dependent agents that reference this agent
- Removes the agent configuration from the server
- Provides confirmation of successful deletion

**Dependency Checking:**
The delete operation will fail if:
- Other agents have this agent listed in their `handoffs` configuration
- The agent is referenced by other system components

**Error Handling:**
- **Agent not found**: Returns clear error message if agent doesn't exist
- **Dependencies exist**: Lists all dependent agents that must be removed first
- **Server connection**: Handles authentication and connectivity issues gracefully

**Examples:**
```bash
# Delete a specific agent
srectl agent delete --name incident_agent

# Example output on success
# Agent 'incident_agent' deleted successfully.

# Example output when dependencies exist
# Cannot delete agent 'meta_agent': The following agents depend on it:
# - example_agent
# - database_agent
# Please remove these dependencies first or delete the dependent agents.
```

**Best Practices:**
- Always check agent dependencies before deletion using `srectl list agents`
- Consider backing up agent configurations before deletion
- Remove dependent agents first when cleaning up a group of related agents
- Verify deletion by running `srectl list agents` after the operation

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

### `srectl tool delete`

Delete a tool from the remote server.

**Syntax:**
```bash
srectl tool delete --name <ToolName>
```

**Required Parameters:**
- `--name`: Name of the tool to delete

**Prerequisites:**
- SRECTL must be initialized (`srectl init`)
- Tool must exist on the remote server
- For remote servers: Azure CLI authentication (`az login`)

**What it does:**
- Validates that the tool exists on the server
- Checks for any dependent agents or other tools that reference this tool
- Removes the tool configuration from the server
- Provides confirmation of successful deletion

**Dependency Checking:**
The delete operation will fail if:
- Agents have this tool listed in their `tools` configuration
- Other tools reference this tool as a dependency
- The tool is currently being used in active conversations

**Error Handling:**
- **Tool not found**: Returns clear error message if tool doesn't exist
- **Dependencies exist**: Lists all dependent agents/tools that must be updated first
- **Server connection**: Handles authentication and connectivity issues gracefully

**Examples:**
```bash
# Delete a specific tool
srectl tool delete --name GetServiceLogs

# Example output on success
# Tool 'GetServiceLogs' deleted successfully.

# Example output when dependencies exist
# Cannot delete tool 'example_tool': The following agents depend on it:
# - example_agent (uses: example_tool, other_tool)
# - test_agent (uses: example_tool)
# Please remove this tool from the dependent agents' configurations first.
```

**Best Practices:**
- Always check tool dependencies before deletion using `srectl list agents` and `srectl list extended-tools`
- Consider backing up tool configurations before deletion
- Update agent configurations to remove tool references before deletion
- Verify deletion by running `srectl list extended-tools` after the operation
- Be extra careful with widely-used utility tools that many agents depend on

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

## Document Management Commands

### `srectl doc upload`

Upload documents or folders to the SRE Agent's knowledge base for indexing and search.

**Syntax:**
```bash
srectl doc upload --file <path> | --folder <path> [--recursive] [--no-index]
```

**Parameters:**
- `--file`: Path to a specific file to upload
- `--folder`: Path to a folder containing documents to upload
- `--recursive`: Include files from subdirectories (only with `--folder`)
- `--no-index`: Upload without immediate indexing (deferred indexing)

**Supported File Types:**
- Text files (.txt, .md, .yaml, .yml, .json, .xml)
- Microsoft Office documents (.docx, .xlsx, .pptx)
- PDF documents (.pdf)
- Source code files (.cs, .js, .ts, .py, .java, .cpp, etc.)

**What it does:**
- Validates file/folder existence and accessibility
- Discovers files recursively when using `--folder --recursive`
- Filters supported file types automatically
- Uploads files using multipart form data
- Triggers indexing for immediate searchability (unless `--no-index`)
- Provides progress feedback during upload

**Examples:**
```bash
# Upload a single document
srectl doc upload --file troubleshooting-guide.md

# Upload all files in a folder
srectl doc upload --folder ./documentation

# Upload folder recursively with all subdirectories
srectl doc upload --folder ./knowledge-base --recursive

# Upload without immediate indexing
srectl doc upload --folder ./docs --no-index
```

**Prerequisites:**
- SRECTL must be initialized (`srectl init`)
- Authentication required for remote servers (`az login`)
- Sufficient permissions to access the specified files/folders

### `srectl doc search`

Search the indexed document knowledge base for relevant information.

**Syntax:**
```bash
srectl doc search --query "<search-terms>" [--limit <number>]
```

**Parameters:**
- `--query` (required): Search terms or question to find relevant documents
- `--limit`: Maximum number of results to return (default: 10, max: 50)

**Search Features:**
- Semantic search using natural language queries
- Relevance-based result ranking
- Content snippet extraction
- Document metadata display

**What it displays:**
- Document titles and file paths
- Relevance scores
- Content snippets matching the query
- Total number of results found

**Examples:**
```bash
# Basic search
srectl doc search --query "troubleshooting Redis performance"

# Search with custom result limit
srectl doc search --query "Azure monitoring best practices" --limit 5

# Search for specific procedures
srectl doc search --query "how to restart web services"
```

**Use Cases:**
- Finding relevant troubleshooting guides
- Locating specific procedures or runbooks
- Discovering related documentation
- Research for incident response

### `srectl doc reindex`

Rebuild the document search index to improve search performance and incorporate newly uploaded documents.

**Syntax:**
```bash
srectl doc reindex
```

**What it does:**
- Triggers a complete rebuild of the document search index
- Processes all uploaded documents for improved searchability
- Updates search relevance algorithms
- Incorporates any documents uploaded with `--no-index`
- Provides progress feedback during the reindexing process

**When to use:**
- After uploading large batches of documents
- When search results seem outdated or incomplete
- After uploading documents with `--no-index` option
- To improve search performance and relevance
- As part of regular maintenance procedures

**Examples:**
```bash
# Rebuild the entire document index
srectl doc reindex
```

**Note:** Reindexing may take several minutes depending on the number and size of documents in the knowledge base.

---

## Thread Management Commands

### `srectl thread new`

Create a new conversation thread and start an interactive chat session with the SRE Agent.

**Syntax:**
```bash
srectl thread new --message "<your-question>" [--no-wait]
```

**Parameters:**
- `--message` (required): Initial message to send to the agent
- `--user-id` (optional): User ID for the message (defaults to current user)
- `--display-name` (optional): Display name for the message (defaults to current user)
- `--no-wait`: Don't wait for agent response, exit after sending message
- `--wait`: Wait for agent response and start interactive chat (default behavior)

**Interactive Chat Features:**
- **Seamless Conversation**: After the agent responds, you'll be prompted to continue the conversation
- **Real-time Responses**: Agent messages appear immediately as they're received
- **Easy Exit**: Press Ctrl+C or type 'exit', 'quit', '/exit', or '/quit' to end the chat session
- **Thread Persistence**: Your conversation is saved and can be resumed later

**What it does:**
- Creates new conversation thread on remote server
- Sends initial message to agent
- Displays agent response in real-time
- Starts interactive chat session (unless `--no-wait` specified)
- Stores thread ID for future reference

**Examples:**
```bash
# Start interactive chat session
srectl thread new --message "Help me troubleshoot a Redis container issue"

# Send message without waiting for response
srectl thread new --message "What's the current system health status?" --no-wait

# Specify custom user details
srectl thread new --message "Check system status" --user-id "admin" --display-name "System Admin"
```

### `srectl thread continue`

Continue an existing conversation thread with a follow-up message or resume interactive chat.

**Syntax:**
```bash
srectl thread continue [--message "<follow-up-message>"] [--thread-id <thread-id>] [--no-wait]
```

**Parameters:**
- `--message` (optional): Follow-up message to send
- `--thread-id` (optional): Specific thread ID to continue (defaults to most recent)
- `--user-id` (optional): User ID for the message (defaults to current user)
- `--display-name` (optional): Display name for the message (defaults to current user)
- `--no-wait`: Don't wait for agent response or start interactive mode
- `--wait`: Wait for agent response and start interactive chat (default behavior)

**Interactive Mode:**
- **With Message**: Sends the message, waits for agent response, then starts interactive chat
- **Without Message**: Shows conversation history and starts interactive chat immediately
- **Exit Options**: Use Ctrl+C or exit commands to end the session

**Examples:**
```bash
# Continue with a specific message and start interactive mode
srectl thread continue --message "The issue is still persisting"

# Resume interactive chat without sending a new message
srectl thread continue

# Continue specific thread
srectl thread continue --thread-id thread_abc123 --message "Can you check again?"

# Send message without starting interactive mode
srectl thread continue --message "Status update please" --no-wait
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

### `srectl thread track`

Track an existing thread for new messages in real-time.

**Syntax:**
```bash
srectl thread track --thread-id <thread-id>
```

**Parameters:**
- `--thread-id` (required): ID of the thread to track

**What it does:**
- Monitors the specified thread for new messages
- Displays new messages as they arrive
- Continues tracking until interrupted (Ctrl+C)

**Example:**
```bash
srectl thread track --thread-id thread_abc123
```

---

## Profile Management Commands

### `srectl profile list`

List all available profiles and show which one is currently active.

**Syntax:**
```bash
srectl profile list
```

**What it displays:**
- Profile names
- Resource URLs for each profile
- Authentication requirements
- Current active profile indicator (marked with *)

**Example:**
```bash
srectl profile list
```

---

### `srectl profile get`

Get details of a specific profile or the current active profile.

**Syntax:**
```bash
srectl profile get [--name <ProfileName>]
```

**Parameters:**
- `--name`: Specific profile name (optional, defaults to current profile)

**Example:**
```bash
# Get current profile details
srectl profile get

# Get specific profile details
srectl profile get --name production
```

---

### `srectl profile create`

Create a new profile to connect to an SRE Agent instance (local or remote).

**Syntax:**
```bash
srectl profile create --name <ProfileName> --resource-url <ResourceURL> [--set-current]
```

**Required Parameters:**
- `--name`: Unique profile name
- `--resource-url`: URL of the SRE Agent instance

**Optional Parameters:**
- `--set-current`: Switch to this profile immediately after creation

**Examples:**
```bash
# Create local development profile
srectl profile create --name local-dev --resource-url https://localhost:7023

# Create production profile and switch to it
srectl profile create --name production --resource-url https://prod.azuresre.ai --set-current
```

---

### `srectl profile set`

Switch to a different profile to change which SRE Agent instance you're connected to.

**Syntax:**
```bash
srectl profile set --name <ProfileName>
```

**Required Parameters:**
- `--name`: Profile name to switch to

**Example:**
```bash
srectl profile set --name production
```

---

### `srectl profile delete`

Delete a profile (cannot delete the currently active profile).

**Syntax:**
```bash
srectl profile delete --name <ProfileName>
```

**Required Parameters:**
- `--name`: Profile name to delete

**Example:**
```bash
srectl profile delete --name old-dev-instance
```

**Note:** You must switch to a different profile before deleting the current one.

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

### `srectl list data-connectors`

List all data connectors configured on the server.

**Syntax:**
```bash
srectl list data-connectors
```

**What it displays:**
- Data connector names and types
- Configuration status
- Connection endpoints
- Authentication methods
- Total connector count

**Example:**
```bash
srectl list data-connectors
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

### `srectl chat`

Start a persistent interactive chat session with the SRE Agent.

**Syntax:**
```bash
srectl chat
```

**Features:**
- Interactive conversation mode
- Maintains context across messages
- Type 'exit' or 'quit' to end the session
- Automatic thread management

**Example:**
```bash
srectl chat
```

**Usage:**
```
> srectl chat
Starting interactive chat session...
Type 'exit' or 'quit' to end the session.

You: How do I troubleshoot a Kubernetes pod that's not starting?
Agent: I can help you troubleshoot a Kubernetes pod that's not starting...

You: exit
Chat session ended.
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

---

## Incident Handler Commands

### `srectl incidenthandler create`

Create a new incident filter with comprehensive filtering criteria and agent mapping capabilities.

**Syntax:**
```bash
srectl incidenthandler create --id <FilterID> [options]
```

**Required Parameters:**
- `--id`: Unique identifier for the incident filter

**Optional Parameters:**
- `--name`: Human-readable name for the filter
- `--impacted-service`: Service affected by incidents matching this filter  
- `--priority`: Priority level for incidents (e.g., 1, 2, 3, 4)
- `--incident-type`: Type of incident (e.g., LiveSite, Monitoring, Service Issue)
- `--alert-id`: Alert ID pattern to match
- `--title-contains`: Text that must be contained in the incident title
- `--agent-mode`: Agent mode (autonomous, manual) [default: autonomous]
- `--handling-agent`: YAML agent to handle incidents for this filter
- `--owning-team-id`: ID of the team that owns this filter
- `--max-attempts`: Maximum automated investigation attempts [default: 3]

**What it does:**
1. Validates that the filter ID doesn't already exist
2. Creates a new incident filter with specified criteria
3. Configures filtering rules for incident matching
4. Sets up agent handling and automation parameters
5. Enables the filter for active incident processing

**API Integration:**
- GET `/api/v1/incidentplayground/filters` - Check for duplicates
- PUT `/api/v1/incidentplayground/filters/{filterId}` - Create new filter

**Examples:**
```bash
# Create basic incident filter
srectl incidenthandler create --id web_service_outages --name "Web Service Outages"

# Create comprehensive filter with all options
srectl incidenthandler create \
  --id database_critical \
  --name "Database Critical Issues" \
  --impacted-service "Database Service" \
  --priority "1" \
  --incident-type "LiveSite" \
  --title-contains "database" \
  --handling-agent "database_expert_agent" \
  --owning-team-id "team_database" \
  --max-attempts 5

# Create filter for specific alert pattern
srectl incidenthandler create \
  --id redis_alerts \
  --name "Redis Performance Alerts" \
  --alert-id "REDIS_*" \
  --impacted-service "Cache Service" \
  --handling-agent "redis_agent"
```

**Filter Criteria Behavior:**
- **Empty values**: Optional parameters left empty will match any value
- **Combining criteria**: All specified criteria must match for incident processing
- **Pattern matching**: `title-contains` and `alert-id` support substring/pattern matching
- **Priority handling**: Numeric priority levels (1=highest, 4=lowest)

**Error Handling:**
- **Duplicate ID**: Prevents creating filters with existing IDs
- **Invalid parameters**: Validates parameter formats and ranges
- **API failures**: Detailed error messages for troubleshooting

**Prerequisites:**
- Initialized SRECTL configuration
- Valid authentication for remote servers
- Unique filter ID

### `srectl incidenthandler map-agent`

Map a YAML-based agent to handle incidents for a specific filter, replacing traditional incident handlers.

**Syntax:**
```bash
srectl incidenthandler map-agent --name <FilterName> --handling-agent <AgentName>
```

**Required Parameters:**
- `--name`: Name of the incident filter to update
- `--handling-agent`: Name of the YAML agent to handle incidents

**What it does:**
1. Fetches the specified incident filter from the server
2. Validates that the specified agent exists on the server
3. Updates the filter's `HandlingAgent` property
4. Searches for existing incident handlers linked to the filter
5. Deletes any traditional incident handlers found
6. Configures YAML-based incident handling for the filter

**API Integration:**
- GET `/api/v1/incidentplayground/filters` - List all filters
- GET `/api/v1/extendedAgent/agents` - Verify agent exists
- POST `/api/v1/incidentplayground/filters/{filterId}` - Update filter
- GET `/api/v1/incidentplayground/handlers` - List handlers
- DELETE `/api/v1/incidentplayground/handlers/{handlerId}` - Delete handlers

**Examples:**
```bash
# Map agent to handle Redis incidents
srectl incidenthandler map-agent --name redis_incidents --handling-agent redis_agent

# Map agent to handle database performance issues
srectl incidenthandler map-agent --name db_perf_filter --handling-agent database_performance_agent
```

**Error Handling:**
- **Filter not found**: Clear error if the filter doesn't exist
- **Agent not found**: Validates agent existence before updating
- **API failures**: Comprehensive error messages for troubleshooting
- **Partial success**: Reports which operations succeeded/failed

**Prerequisites:**
- Initialized SRECTL configuration
- Valid authentication for remote servers
- Both filter and agent must exist on the server

### `srectl list incidenthandlers`

List all incident handlers configured on the server.

**Syntax:**
```bash
srectl list incidenthandlers [--verbose]
```

**Optional Parameters:**
- `--verbose, -v`: Show detailed information including filter details

**What it does:**
1. Fetches all incident handlers from the server
2. Displays handler information (ID, name, filter associations)
3. In verbose mode, fetches and displays associated filter details
4. Shows which handlers have YAML agents mapped

**API Integration:**
- GET `/api/v1/incidentplayground/handlers` - List all handlers
- GET `/api/v1/incidentplayground/filters` - Get filter details (verbose mode)

**Examples:**
```bash
# Basic list of incident handlers
srectl list incidenthandlers

# Detailed view with filter information
srectl list incidenthandlers --verbose

# Alternative command format
srectl incidenthandler list --verbose
```

**Sample Output:**
```
📋 Fetching incident handlers...
Found 2 incident handler(s):

[1] Database Performance Handler
    ID: handler_001
    Filter ID: filter_db_perf
    Filter Name: database_performance_filter
    Handling Agent: database_perf_agent
    Created: 2024-08-15T10:30:00Z
    Updated: 2024-08-20T14:22:15Z

[2] Network Diagnostics Handler  
    ID: handler_002
    Filter ID: filter_network
    Filter Name: network_issues
    Created: 2024-08-16T09:15:30Z
    Updated: 2024-08-16T09:15:30Z
```

**Use Cases:**
- Audit existing incident handler configurations
- Verify handler-to-filter associations
- Monitor handler activity and updates
- Identify handlers with YAML agent mappings

**Prerequisites:**
- Initialized SRECTL configuration
- Valid authentication for remote servers
