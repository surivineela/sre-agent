# SRECTL

## Overview

**SRECTL** is a cross-platform .NET 9 command-line tool designed to help developers efficiently create, manage, and validate YAML-based agent and tool definitions for SRE (Site Reliability Engineering) automation systems. It streamlines the process of authoring, organizing, and checking the integrity of agent and tool configuration files, enabling rapid local development and validation before deployment.

---

## Key Features

- **Agent and Tool Creation:**  
  Quickly scaffold new agent and tool YAML files with required and custom properties using simple CLI commands.

- **AI-Powered Smart Generation:**  
  Leverage AI to automatically generate comprehensive agent instructions and recommend appropriate tools based on agent names and context.

- **Interactive Thread Management:**  
  Engage in real-time conversations with SRE agents through persistent thread management, enabling iterative troubleshooting and testing.

- **Remote Server Integration:**  
  Initialize CLI configuration to connect with remote SRE Agent servers and apply agent configurations directly to the server.

- **Validation:**  
  Instantly validate agent YAML files for required fields, naming conventions, and description length, either individually or in bulk.

- **Extensible Arguments:**  
  Add any number of custom key-value pairs to your agent or tool definitions directly from the command line.

- **Self-contained Executable:**  
  Distributed as a single `.exe` (or platform-specific binary), requiring no external dependencies on the target machine.

---

## Installation

To install SRECTL on your machine, simply run the build-and-install script:

```powershell
.\build-and-install.ps1
```

**What it does:**
- Builds the project in Release configuration
- Creates a NuGet package
- Uninstalls any existing version of SRECTL
- Installs the new version globally as a .NET tool

**Prerequisites:**
- .NET 9 SDK installed on your machine
- PowerShell execution policy that allows running scripts

**After installation:**
- The `srectl` command will be available globally in your terminal
- You can run `srectl --help` from any directory to see available commands
- No additional configuration is required to start using the tool

**Alternative Installation:**
If you prefer manual installation:
1. Run `dotnet pack --configuration Release --output ./nupkg`
2. Run `dotnet tool install -g srectl --add-source ./nupkg`

---

## Commands

### 1. Configuration Management

#### Initialize SRECTL Configuration
Initialize the SRECTL with a remote server URL and create the necessary directory structure:

```bash
srectl init --resource-url <ResourceURL>
```

**Parameters:**
- **--resource-url**: The URL of the SRE Agent data plane API (required)
  - For localhost: `https://localhost:7023`
  - For remote: `https://your-sreagent-endpoint.azuresre.ai`

**What it does:**
- Creates a `.sreagent-config.json` file with server configuration
- Creates `agents/`, `tools/`, and `connectors/` directories
- Adds example YAML files in each directory
- Tests the connection to the remote server
- Determines if authentication is required (non-localhost URLs require Azure CLI login)

**Examples:**

```bash
# Initialize for local development
srectl init --resource-url https://localhost:7023

# Initialize for remote server
srectl init --resource-url https://ajsharmsreagentpublic--98c19030.6d6a35f1.swedencentral.azuresre.ai
```

**Note**: If your SRE Agent is in a different tenant (e.g. AME/PME) then you will have to add your CORP user identifier as a role into that SRE Agent resource.

```bash
az resource patch --ids <YOUR_SRE_AGENT_ARM_RESOURCE_ID> -p '{"adminUsers": [{"objectId":"YOUR_CORP_USER_OBJECT_ID","tenantId":"72f988bf-86f1-41af-91ab-2d7cd011db47"}]}'
```

You can find your azure corp user object id here: [Entra ID Overview Page](https://ms.portal.azure.com/#view/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/~/Overview)

E.g.

```bash
az resource patch --ids /subscriptions/ab32b825-51f2-41b0-8d25-85f7a0071a6f/resourceGroups/sre-icm-3p-rg/providers/Microsoft.App/agents/jefftest-sweden -p '{"adminUsers": [{"objectId":"ce2b0d61-1323-7db2-a7f3-12bcd48f2ebc","tenantId":"72f988bf-86f1-41af-91ab-2d7cd011db47"}]}'
```

---

### 2. Agent Management

#### Create an Agent
Create a new agent definition with comprehensive configuration options:

```bash
srectl agent create --name <AgentName> [options]
```

**Required Parameters:**
- **--name**: The unique name for the agent (no whitespace allowed)

**Optional Parameters:**
- **--instructions**: System prompt/instructions for the agent (default: auto-generated based on name)
- **--tools**: List of tools the agent can use (default: empty list)
- **--handoff-description**: Description for handoff to this agent
- **--handoffs**: List of agents this agent can handoff to
- **--allow-parallel-tool-calls**: Enable parallel tool calls
- **--max-reflection-count**: Maximum reflection count (default: 0)
- **--critic-prompt-path**: Path to critic prompt file
- **--critic-on-handoff**: Enable critic on handoff
- **--custom-reflection-note**: Custom reflection note
- **--common-prompts**: List of common prompts to include
- **--temperature**: Temperature for the agent (0.0-2.0)
- **--output-type**: Output type for the agent
- **--smart**: Use AI to automatically generate instructions and recommend tools

**Examples:**

```bash
# Create agent with just a name (minimal required)
srectl agent create --name my_simple_agent

# Create agent with full configuration
srectl agent create --name pagerduty_incident_agent \
  --instructions "You are an incident management assistant for PagerDuty. Help users acknowledge, add notes, and resolve incidents." \
  --tools ResolvePagerDutyIncident AcknowledgePagerDutyIncident AddNoteToPagerDutyIncident \
  --handoff-description "Use this agent for PagerDuty incident management tasks" \
  --handoffs meta_agent \
  --max-reflection-count 1 \
  --common-prompts format_guidelines guard_rail
```

Creates `agents/pagerduty_incident_agent/pagerduty_incident_agent.yaml` with proper YAML structure using snake_case naming convention.

#### Smart Agent Generation (AI-Powered)

The CLI includes an intelligent agent generation feature that leverages AI to automatically create comprehensive agent definitions with appropriate instructions and tool recommendations.

**Command:**
```bash
srectl agent create --name <AgentName> --smart [--instructions "<additional-guidance>"]
```

**How it works:**
1. **AI Analysis**: The CLI sends the agent name to an AI service that analyzes the intended purpose
2. **Instruction Generation**: AI generates detailed, context-aware instructions for the agent
3. **Tool Recommendation**: AI recommends appropriate tools based on the agent's purpose
4. **Automatic Creation**: The CLI creates a fully-formed agent YAML with generated content

**API Integration:**
The smart generation uses the `/api/v1/incidentplayground/generateInstructions` endpoint to:
- Analyze the agent name and any provided instructions
- Generate comprehensive execution plans and workflows
- Recommend relevant tools from the available tool catalog
- Return structured instructions ready for agent deployment

**Examples:**

```bash
# Generate a smart agent for Redis Container App incidents
srectl agent create --name "RedisContainerAppDown" --smart
```

**Sample Output:**
```
🤖 Generating smart agent with AI...
✅ AI generated instructions and 5 recommended tools!
📝 Generated Instructions Preview: ### EXECUTION_PLAN ###

- Identify the impacted Redis Container App by analyzing the incident details and logs...
🔧 Recommended Tools: SearchContainerAppsResourcesByName, GetContainerAppInfo, RestartContainerApp, ValidateContainerAppHealth, GetContainerAppLogs
✅ Agent YAML created at agents/RedisContainerAppDown/RedisContainerAppDown.yaml
```

**Generated Agent Structure:**
The AI-generated agent includes:
- **Comprehensive Instructions**: Detailed execution plans with step-by-step workflows
- **Recommended Tools**: Curated list of tools specific to the agent's purpose
- **Mitigation Strategies**: Built-in confirmation and validation steps
- **Escalation Procedures**: Guidance for handling edge cases

**Smart Generation with Custom Instructions:**
```bash
# Provide additional context to guide AI generation
srectl agent create --name "DatabasePerformanceIssue" --smart \
  --instructions "Focus on PostgreSQL performance optimization and query analysis"
```

The AI will incorporate your custom instructions into the generation process, creating more targeted and specific agent definitions.

**Benefits:**
- **Rapid Prototyping**: Quickly create fully-functional agents without manual instruction writing
- **Best Practices**: AI incorporates SRE best practices and proven incident response patterns
- **Tool Discovery**: Automatically discovers and recommends relevant tools you might not know about
- **Consistency**: Ensures all generated agents follow established patterns and conventions

---

#### Validate an Agent

Validate a single agent YAML file:

```bash
srectl agent validate --file <path/to/agent.yaml>
```

Validate all agent YAML files in the `agents/` directory:

```bash
srectl agent validate --all
```

**Validation includes:**
- **Name validation**: Non-empty, no whitespace
- **Instructions validation**: 50-5000 characters
- **Tools validation**: At least one tool, no empty/whitespace names
- **Handoffs validation**: No empty/whitespace names
- **Temperature validation**: 0.0-2.0 range if specified
- **Max reflection count**: Non-negative values
- **Handoff description**: Under 500 characters if specified
- **Common prompts**: No empty names
- **Agents as tools**: Complete object validation

Reports detailed validation results for each file with specific error messages.

---

#### Apply an Agent

Apply an agent configuration to the remote server configured during initialization:

```bash
srectl agent apply --name <AgentName>
```

**Parameters:**
- **--name**: The name of the agent to apply (required)

**Prerequisites:**
- SRECTL must be initialized with `srectl init` first
- Agent YAML file must exist in the `agents/` directory (either as `agents/AgentName.yaml` or `agents/AgentName/AgentName.yaml`)
- For non-localhost servers, you must be logged in with Azure CLI (`az login`)

**What it does:**
- Reads the agent YAML file from the local directory
- Sends a PUT request to the `/api/v1/extendedAgent/apply` endpoint
- Includes authentication headers for remote servers
- Reports success or failure with detailed error messages

**Examples:**

```bash
# Apply an agent to the configured server
srectl agent apply --name pagerduty_incident_agent

# Apply the example agent created during init
srectl agent apply --name example_agent
```

**Sample Output:**
```
✅ Agent 'pagerduty_incident_agent' applied successfully!
```

#### Delete an Agent

Delete an agent configuration from the remote server:

```bash
srectl agent delete --name <AgentName>
```

**Parameters:**
- **--name**: The name of the agent to delete (required)

**Prerequisites:**
- SRECTL must be initialized with `srectl init` first
- Agent must exist on the remote server
- For non-localhost servers, you must be logged in with Azure CLI (`az login`)

**What it does:**
- Validates that the agent exists on the remote server
- Checks for any dependent agents that reference this agent in their handoffs
- Sends a DELETE request to the `/api/v1/extendedAgent/agents/{name}` endpoint
- Reports success or failure with detailed error messages

**Dependency Checking:**
The delete operation will fail if other agents depend on this agent:
- Lists all agents that have this agent in their `handoffs` configuration
- Provides clear guidance on which agents need to be updated or deleted first

**Examples:**

```bash
# Delete an agent from the configured server
srectl agent delete --name pagerduty_incident_agent

# Delete a test agent
srectl agent delete --name test_agent
```

**Sample Successful Output:**
```
✅ Agent 'pagerduty_incident_agent' deleted successfully.
```

**Sample Dependency Error Output:**
```
❌ Cannot delete agent 'meta_agent': The following agents depend on it:
- example_agent
- database_agent
Please remove these dependencies first or delete the dependent agents.
```

**Sample Not Found Error Output:**
```
❌ Agent 'nonexistent_agent' not found on the server.
```

---

### 3. Tool Management

#### Create a Tool

Create a new tool definition with the following command:

```bash
srectl tool create --name <ToolName> --type <ToolType> [--extra key value ...]
```

**Required Parameters:**
- **--name**: The unique name for the tool
- **--type**: The type/category of the tool

**Optional Parameters:**
- **--extra**: Additional key-value pairs to customize the tool

**Special Feature - KustoTool Auto-Generation:**
When creating a tool with `--type KustoTool`, the CLI automatically generates a comprehensive template with default values for:
- `connector`: Default Kusto connector reference
- `description`: Template description with placeholder text
- `mode`: Set to "query" 
- `function`: Uses the tool name
- `query`: Template KQL query with examples
- `file`: Generated file path for the query
- `database`: Default database name
- `clusterHint`: Default cluster hint
- `parameters`: Sample parameter with proper structure
- `attributes`: Empty attributes array
- `metadata`: Template metadata with owner, version, tags, and timestamp

**Examples:**

```bash
# Create a simple tool
srectl tool create --name MyCustomTool --type HttpConnector

# Create a KustoTool with auto-generated template
srectl tool create --name GetServiceLogs --type KustoTool

# Create a tool with additional properties
srectl tool create --name GetWorkerHealthpings --type KustoQuery --extra version 1.0 description "A kusto tool to fetch worker health pings"
```

**Sample KustoTool Output:**
When you create a KustoTool, it generates a comprehensive YAML with structure similar to:

```yaml
name: GetServiceLogs
type: KustoTool
connector: default-kusto-connector
description: A Kusto query tool for GetServiceLogs. Please update this description with specific details about what this tool does.
mode: query
function: GetServiceLogs
query: |
  // Please provide your KQL query here
  // Example:
  // MyTable
  // | where TimeGenerated > ago(1h)
  // | take 10
file: Queries/GetServiceLogs.kql
database: DefaultDB
clusterHint: default-cluster
parameters:
  - name: timeRange
    type: string
    required: true
    description: Time range for the query (e.g., '1h', '24h')
    mapTo: args
    target: dictionary:args:string
attributes: []
metadata:
  owner: team-name
  version: 1.0
  tags: [query, kusto]
  lastUpdated: 2025-07-22
```

#### Apply a Tool
Apply a tool configuration to the remote server:

```bash
srectl tool apply --name <ToolName>
```

**Requirements:**
- Must have initialized SRECTL configuration with `srectl init`
- Tool YAML file must exist in `tools/<ToolName>/<ToolName>.yaml` or `tools/<ToolName>.yaml`
- For remote servers (non-localhost), must be authenticated with Azure CLI (`az login`)

**Examples:**

```bash
# Apply a specific tool to the server
srectl tool apply --name GetServiceLogs
```

**Sample Output:**
```
✅ Tool 'GetServiceLogs' applied successfully!
```

#### Delete a Tool

Delete a tool configuration from the remote server:

```bash
srectl tool delete --name <ToolName>
```

**Parameters:**
- **--name**: The name of the tool to delete (required)

**Prerequisites:**
- SRECTL must be initialized with `srectl init` first
- Tool must exist on the remote server
- For non-localhost servers, you must be logged in with Azure CLI (`az login`)

**What it does:**
- Validates that the tool exists on the remote server
- Checks for any dependent agents or tools that reference this tool
- Sends a DELETE request to the `/api/v1/extendedAgent/tools/{name}` endpoint
- Reports success or failure with detailed error messages

**Dependency Checking:**
The delete operation will fail if agents or other tools depend on this tool:
- Lists all agents that have this tool in their `tools` configuration
- Provides clear guidance on which agents need to be updated before deletion

**Examples:**

```bash
# Delete a tool from the configured server
srectl tool delete --name GetServiceLogs

# Delete a test tool
srectl tool delete --name TestTool
```

**Sample Successful Output:**
```
✅ Tool 'GetServiceLogs' deleted successfully.
```

**Sample Dependency Error Output:**
```
❌ Cannot delete tool 'example_tool': The following agents depend on it:
- example_agent (uses: example_tool, other_tool)
- test_agent (uses: example_tool)
Please remove this tool from the dependent agents' configurations first.
```

**Sample Not Found Error Output:**
```
❌ Tool 'nonexistent_tool' not found on the server.
```

#### Validate a Tool
Validate a single tool definition:
```bash
srectl tool validate --name <ToolName>
```
Validate all tool YAML files in the `tools/` directory:
```bash
srectl tool validate --all
```

#### Show Available Tool Types
Display all available tool types that can be used when creating tools:

```bash
srectl tool show-types [--verbose] [--type <ToolTypeName>]
```

**Parameters:**
- **--verbose**: Show detailed information including assembly and namespace
- **--type**: Show detailed information for a specific tool type

**What it does:**
- Scans the Agent.Plugins and Agent.Framework assemblies for tool types
- Discovers all types decorated with `ToolTypeAttribute`
- Displays tool type names and descriptions
- Shows sample YAML templates for each tool type

**Examples:**

```bash
# List all available tool types
srectl tool show-types

# List tool types with detailed information
srectl tool show-types --verbose

# Show details for a specific tool type
srectl tool show-types --type KustoTool
```

**Sample Output:**

```
=====================================
Available Tool Types
=====================================

[KustoQuery]
  Description: Execute raw Kusto queries with direct parameter support

[KustoTool]
  Description: Execute Kusto queries, functions, or scripts against Azure Data Explorer clusters

[SUCCESS] Found 2 tool type(s)

Usage: srectl tool show-types --type <ToolTypeName> for detailed information
```

**Sample Detailed Output (`--type KustoTool`):**

```
=====================================
Tool Type Details: KustoTool
=====================================
Description: Execute Kusto queries, functions, or scripts against Azure Data Explorer clusters
Type: KustoToolType
Assembly: Agent.Plugins
Namespace: Agent.Plugins.Kusto.Tools

Sample YAML:
-------------------------------------
name: MyKustoTool
type: KustoTool
connector: my-kusto-connector
description: Sample Kusto tool for querying logs
mode: query
database: MyDatabase
cluster_hint: westus
query: |
  MyTable
  | where TimeGenerated > ago(1h)
  | summarize count() by OperationName
parameters:
  - name: timeRange
    type: string
    required: true
    description: Time range for the query
    map_to: args
    target: dictionary:args:string
-------------------------------------

[SUCCESS] Tool type details displayed for 'KustoTool'
```

#### Show Available Connector Types
Display all available connector types that can be referenced in tools:

```bash
srectl tool show-connectors [--verbose]
```

**Parameters:**
- **--verbose**: Show detailed information including assembly and namespace

**What it does:**
- Scans the Agent.Plugins and Agent.Framework assemblies for connector types
- Discovers all types that inherit from `DataConnectorDefinitionBase`
- Displays connector type names and descriptions

**Example:**

```bash
# List all available connector types
srectl tool show-connectors

# List connector types with detailed information
srectl tool show-connectors --verbose
```

**Sample Output:**

```
=====================================
Available Connector Types
=====================================

[KustoConnector]
  Description: Connects to Azure Data Explorer (Kusto) clusters for data querying

[SUCCESS] Found 1 connector type(s)
```

---

### 4. Thread Management

SRECTL provides comprehensive thread management for interactive conversations with the SRE Agent. This allows you to have persistent conversations and manage multiple chat sessions.

#### Start a New Conversation Thread

Create a new conversation thread and send an initial message:

```bash
srectl thread new --message "<your-question>"
```

**Parameters:**
- **--message**: The initial message to send to the agent (required)
- **--no-wait**: Send the message and exit without waiting for response (optional)

**What it does:**
- Creates a new conversation thread on the remote server
- Sends your initial message to the agent
- By default, waits for and displays the agent's response in real-time with smart completion detection
- Stores the thread ID for future reference
- Use `--no-wait` to send message and exit immediately without waiting for response

**Example:**

```bash
srectl thread new --message "Help me troubleshoot a Redis container that keeps restarting"
```

**Sample Output:**
```
🧵 Creating new thread...
✅ Thread created successfully with ID: thread_abc123
💬 You: Help me troubleshoot a Redis container that keeps restarting

🤖 SRE Agent: I'll help you troubleshoot the Redis container restart issue. Let me gather some information...

[Agent response continues with troubleshooting steps]
```

#### Continue an Existing Conversation

Continue a previous conversation thread with a follow-up message:

```bash
srectl thread continue --message "<follow-up-message>"
```

**Parameters:**
- **--message**: The follow-up message to send (required)
- **--thread-id**: Specific thread ID to continue (optional, defaults to most recent)
- **--no-wait**: Send the message and exit without waiting for response (optional)

**What it does:**
- Uses the most recent thread or specified thread ID
- Sends your follow-up message to the existing conversation
- By default, waits for and displays the agent's response while maintaining conversation context
- Use `--no-wait` to send message and exit immediately without waiting for response

**Examples:**

```bash
# Continue the most recent thread
srectl thread continue --message "The container is still restarting after applying your suggestions"

# Continue a specific thread
srectl thread continue --thread-id thread_abc123 --message "Can you check the logs again?"
```

#### List All Conversation Threads

Display all your conversation threads:

```bash
srectl thread list
```

**What it does:**
- Retrieves all conversation threads from the server
- Displays thread IDs, titles, creation dates, and last activity
- Shows a summary of each conversation

**Sample Output:**
```
🧵 Your Conversation Threads:
============================

📋 thread_abc123
   Title: Help me troubleshoot a Redis container that keeps restarting
   Created: 2024-01-15T14:30:00Z
   Last Activity: 2024-01-15T15:45:00Z

📋 thread_def456
   Title: Kubernetes deployment scaling issues
   Created: 2024-01-14T09:15:00Z
   Last Activity: 2024-01-14T10:30:00Z

📋 thread_ghi789
   Title: Database performance optimization
   Created: 2024-01-13T16:20:00Z
   Last Activity: 2024-01-13T17:45:00Z

Total: 3 thread(s)
```

#### Delete a Conversation Thread

Remove a specific conversation thread:

```bash
srectl thread delete --thread-id <thread-id>
```

**Parameters:**
- **--thread-id**: The ID of the thread to delete (required)

**What it does:**
- Permanently removes the specified conversation thread
- Cleans up all messages and history for that thread
- Provides confirmation of deletion

**Example:**

```bash
srectl thread delete --thread-id thread_abc123
```

**Sample Output:**
```
🗑️  Deleting thread thread_abc123...
✅ Thread thread_abc123 deleted successfully
```

#### Track a Conversation Thread in Real-Time

Monitor a conversation thread for new messages in real-time:

```bash
srectl thread track --thread-id <thread-id>
```

**Parameters:**
- **--thread-id**: The ID of the thread to track (required)

**What it does:**
- Continuously monitors the specified thread for new messages
- Displays new messages from agents and users as they arrive
- Provides real-time updates with timestamps
- Can be stopped with Ctrl+C
- Useful for monitoring ongoing conversations or waiting for agent responses

**Example:**

```bash
srectl thread track --thread-id thread_abc123
```

**Sample Output:**
```
🔍 Tracking thread thread_abc123...
📡 Monitoring for new messages (Press Ctrl+C to stop)

[2024-01-15T15:30:00Z] 🤖 SRE Agent: Let me check the Redis container logs...
[2024-01-15T15:30:15Z] 🤖 SRE Agent: Found the issue! The container is failing due to insufficient memory allocation.
[2024-01-15T15:30:30Z] 🤖 SRE Agent: Here's how to fix it:
1. Update the memory limits in your deployment YAML
2. Restart the container
3. Monitor the memory usage

🔄 Waiting for new messages...
```

**Thread Management Features:**
- **Persistent Conversations**: Threads maintain context across multiple interactions
- **Real-time Responses**: Live display of agent responses with status indicators
- **Automatic Thread Tracking**: The CLI automatically tracks your most recent thread
- **Cross-session Continuity**: Threads persist between CLI sessions
- **Conversation History**: Full message history preserved for each thread
- **Real-time Monitoring**: Track ongoing conversations for new messages as they arrive
- **Smart Completion Detection**: Automatically detects when agent responses are complete

---

## How It Helps Developers

- **Comprehensive Agent Creation:**  
  Create fully-featured agent definitions with all supported properties including system prompts, tools, handoffs, and advanced configuration options.

- **AI-Powered Development:**  
  Accelerate agent development with smart generation that automatically creates detailed instructions and recommends appropriate tools based on context analysis.

- **Interactive Agent Testing:**  
  Test and interact with your agents in real-time using the thread management system, allowing for immediate feedback and iterative development.

- **Persistent Conversation Management:**  
  Maintain multiple conversation threads for different testing scenarios, enabling comprehensive agent behavior validation across various use cases.

- **Framework Integration:**  
  Uses the same `YamlAgentDescriptor` class and validation logic as the Agent Framework, ensuring consistency between SRECTL-created agents and runtime agents.

- **Robust Validation:**  
  Comprehensive validation covering all agent properties with detailed error reporting to catch issues early in development.

- **Proper YAML Structure:**  
  Generates correctly formatted YAML files with snake_case naming convention that match the framework's expectations.

- **Bulk Operations:**  
  Validate all agent definitions in a single command, catching issues before integration or deployment.

- **Automation Ready:**  
  Integrates easily into CI/CD pipelines for automated validation with proper exit codes.

- **Developer-Friendly:**  
  Clear error messages and validation feedback help developers understand and fix issues quickly.

---

### 4. Remote Resource Listing

#### List Agents
Retrieve and display all agents available on the remote server:

```bash
srectl list agents
```

**What it does:**
- Connects to the configured remote server
- Fetches all agents from the `/api/v1/extendedAgent/agents` endpoint
- Displays formatted agent information including name, description, tools, and handoffs
- Includes pagination information and total count

**Sample Output:**
```
📋 Available Agents:
==================

🤖 meta_agent
   Description: The meta agent orchestrates conversations between specialized agents
   Created: 2024-01-15T10:30:00Z
   Tools: GetAgentRegistry, HandoffToAgent
   Handoffs: pagerduty_agent, kubernetes_agent

🤖 pagerduty_incident_agent
   Description: Use this agent for PagerDuty incident management tasks
   Created: 2024-01-15T09:15:00Z
   Tools: ResolvePagerDutyIncident, AcknowledgePagerDutyIncident, AddNoteToPagerDutyIncident
   Handoffs: meta_agent

Total: 2 agent(s)
```

#### List Tools
Retrieve and display all tools available on the remote server:

```bash
srectl list tools
```

**What it does:**
- Connects to the configured remote server
- Fetches all tools from the `/api/v1/incidentplayground/listTools` endpoint
- Displays formatted tool information including name, category, description, parameters, and plugin

**Sample Output:**
```
🔧 Available Tools:
==================

🛠️  ResolvePagerDutyIncident
   Category: PagerDuty
   Description: Resolves a PagerDuty incident with a given resolution note
   Plugin: PagerDutyPlugin
   Parameters: incident_id, resolution_note

🛠️  GetKubernetesLogs
   Category: Kubernetes
   Description: Retrieves logs from a Kubernetes pod
   Plugin: KubernetesPlugin
   Parameters: namespace, pod_name, container_name, lines

🛠️  QueryPrometheus
   Category: Monitoring
   Description: Executes a PromQL query against Prometheus
   Plugin: PrometheusPlugin
   Parameters: query, start_time, end_time

Total: 3 tool(s)
```

#### List Extended Tools
Retrieve and display all extended tools that have been applied to the remote server through the `apply` command:

```bash
srectl list extended-tools
```

**What it does:**
- Connects to the configured remote server
- Fetches all extended tools from the `/api/v1/extendedAgent/tools` endpoint
- Displays formatted tool information for tools that were added through the CLI or API
- Shows the same detailed information as `list tools` but specifically for extended/custom tools

**Use Case:**
This command is useful to see which custom tools have been successfully applied to the server, as opposed to the built-in tools available through the standard `/listTools` endpoint.

**Sample Output:**
```
🔧 Available Extended Tools:
============================

🛠️  MyCustomKustoQuery
   Category: Kusto
   Description: Custom Kusto query for service health monitoring
   Plugin: KustoPlugin
   Parameters: timeRange, serviceName

🛠️  CustomPagerDutyResolver
   Category: PagerDuty
   Description: Custom PagerDuty resolution tool with additional validation
   Plugin: PagerDutyPlugin
   Parameters: incident_id, resolution_note, validation_check

Total: 2 tool(s)
```

#### List Data Connectors
Retrieve and display all data connectors configured on the remote server:

```bash
srectl list data-connectors
```

**What it does:**
- Connects to the configured remote server
- Fetches all data connectors from the `/api/v1/extendedAgent/dataconnectors` endpoint
- Displays formatted data connector information including name, type, and identity
- Shows connectors that are available for use by agents and tools

**Sample Output:**
```
🔌 Available Data Connectors:
=============================

📊 kusto-cluster-prod
   Type: Kusto
   Identity: /subscriptions/.../userAssignedIdentities/kusto-reader

📊 sql-analytics-db
   Type: SqlDatabase
   Identity: /subscriptions/.../userAssignedIdentities/sql-reader

📊 cosmos-logs-container
   Type: CosmosDb
   Identity: /subscriptions/.../userAssignedIdentities/cosmos-reader

Total: 3 data connector(s)
```

**Use Case:**
This command helps you verify which data connectors are available for your agents and tools to connect to data sources like databases, analytics platforms, and monitoring systems.

**Authentication:**
- For localhost servers: No authentication required
- For remote servers: Requires Azure CLI authentication (`az login`)
- Uses the same configuration as the `apply` command

**Available List Commands:**
```bash
srectl list agents           # List all agents on the server
srectl list tools            # List all built-in tools on the server  
srectl list extended-tools   # List all custom/extended tools applied to the server
```

---

## Getting Started

1. **Build and Publish:**

2. **Add to PATH:**  
   Copy the resulting `srectl.exe` to a folder in your system PATH for easy access.

3. **Run Commands:**  
   Use the commands above to create and validate agents and tools.

---

## Help

Run any command with `--help` to see available options:

---

## CI/CD Integration

You can integrate **SRECTL** into your CI/CD pipeline to automatically validate all agent YAML files before merging or deploying. This ensures that only well-formed, standards-compliant agent definitions are accepted, reducing runtime errors and manual review effort.

Below are general steps and examples for popular CI/CD systems:

---

### 1. Add SRECTL to Your Pipeline Environment

- **Option 1:** Build and publish SRECTL as a self-contained executable and check it into your repo or artifact store.
- **Option 2:** Build SRECTL as part of your pipeline using `dotnet publish`.

---

### 2. Example: GitHub Actions

See [`example_github_action.yaml`](./example_github_action.yaml) for a complete workflow example.

---

### 3. Example: Azure DevOps Pipeline

See [`example_devops.yaml`](./example_devops.yaml) for a complete pipeline example.

---

### 4. General Steps for Any CI/CD System

1. **Checkout your repository.**
2. **Build/publish SRECTL** (or use a prebuilt binary).
3. **Run the validation command:**  
   ```bash
   srectl agent validate --all
   ```

   or (if not in PATH):
   ```bash
   ./srectl agent validate --all
   ```

4. **Fail the build if validation fails.**  
   SRECTL returns a non-zero exit code if validation fails, causing the pipeline to fail.

---

### 5. Benefits

- **Automated Quality Gate:** Prevents invalid agent definitions from being merged or deployed.
- **Immediate Feedback:** Developers get instant validation results in their PRs or builds.
- **Consistency:** Ensures all YAML files meet your standards before reaching production.

---

**Tip:**  
You can also use SRECTL in pre-commit hooks or as part of local developer workflows for even faster feedback.

---

## Troubleshooting

- Ensure you are running with .NET 9.
- For YAML validation errors, check the error messages for missing or invalid fields.
- If you see "No agents directory found" or "No agent YAML files found", ensure you have created agents first.

---

## FAQ

**Q:** How do I create an agent with multiple tools?  
**A:** Use `--tools Tool1 Tool2 Tool3` or `--tools Tool1,Tool2,Tool3`.

**Q:** What's the difference between SRECTL validation and framework validation?  
**A:** SRECTL validation is comprehensive and checks YAML structure/content, while framework validation focuses on runtime requirements like tool availability.

**Q:** Can I create agents with handoffs to other agents?  
**A:** Yes, use `--handoffs agent1 agent2` to specify which agents this agent can handoff to.

**Q:** Where are YAML files stored?  
**A:** Under `agents/<AgentName>/` and `tools/<ToolName>/` directories.

**Q:** What happens if I try to create an agent with the same name?  
**A:** The YAML file will be overwritten without warning.

**Q:** How do I set the temperature for an agent?  
**A:** Use `--temperature 0.7` (must be between 0.0 and 2.0).

**Q:** Is the `run` command implemented?  
**A:** No, it currently prints "Not implemented yet."

**Q:** Can I validate a specific agent file?  
**A:** Yes, use `srectl agent validate --file path/to/agent.yaml`.

---

## Summary

**SRECTL** provides a powerful and comprehensive toolset for creating and validating SRE automation agents. The updated agent creation functionality uses the same `YamlAgentDescriptor` class and validation logic as the Agent Framework, ensuring consistency and reliability. With support for all agent properties including system prompts, tools, handoffs, and advanced configuration options, developers can quickly create production-ready agent definitions with proper validation and error reporting.

---

## Test Cases

The following test cases have been validated for SRECTL. These tests cover agent creation, validation, and error handling scenarios.

### Manual Test Cases

#### 1. SRECTL Initialization - Localhost
**Test:** Initialize SRECTL for local development
```bash
srectl init --resource-url https://localhost:7023
```
**Expected:** 
- Creates `.sreagent-config.json` with auth_required: false
- Creates `agents/`, `tools/`, `connectors/` directories
- Adds example files in each directory
- Tests connection and shows list of existing agents

#### 2. SRECTL Initialization - Remote Server
**Test:** Initialize SRECTL for remote server
```bash
srectl init --resource-url https://ajsharmsreagentpublic--98c19030.6d6a35f1.swedencentral.azuresre.ai
```
**Expected:** 
- Creates `.sreagent-config.json` with auth_required: true
- Creates directory structure and example files
- Tests connection (requires `az login` for authentication)

#### 3. Agent Apply - Example Agent
**Test:** Apply the example agent created during initialization
```bash
srectl agent apply --name example_agent
```
**Expected:** 
- Reads `agents/example_agent.yaml`
- Sends PUT request to configured server
- Shows "Agent 'example_agent' applied successfully!"

#### 4. Basic Agent Creation
**Test:** Create a simple agent with minimal required parameters
```bash
srectl agent create --name test_agent --instructions "Test agent instructions" --tools TestTool1 TestTool2
```
**Expected:** Creates `agents/test_agent/test_agent.yaml` with proper YAML structure

#### 5. Smart Agent Generation
**Test:** Create an AI-generated agent using smart generation
```bash
srectl agent create --name "RedisContainerAppDown" --smart
```
**Expected:** 
- Shows "🤖 Generating smart agent with AI..." message
- Displays generated instructions preview
- Shows recommended tools list
- Creates `agents/RedisContainerAppDown/RedisContainerAppDown.yaml` with:
  - Comprehensive AI-generated instructions
  - Recommended tools specific to Redis Container App incidents
  - Proper YAML structure

#### 6. Smart Agent Generation with Custom Instructions
**Test:** Create a smart agent with additional user guidance
```bash
srectl agent create --name "DatabasePerformanceIssue" --smart --instructions "Focus on PostgreSQL performance optimization"
```
**Expected:** 
- Incorporates user instructions into AI generation
- Creates agent with PostgreSQL-focused content
- Shows both generated and user-provided context

#### 7. Agent Creation with All Options
**Test:** Create an agent with all supported options
```bash
srectl agent create --name full_featured_agent --instructions "Full featured test agent" --tools Tool1 Tool2 --handoff-description "Test handoff" --handoffs meta_agent --allow-parallel-tool-calls --max-reflection-count 2 --temperature 0.7 --common-prompts format_guidelines
```
**Expected:** Creates agent with all properties correctly set

#### 8. Agent Creation with Custom Properties
**Test:** Create an agent with additional custom properties
```bash
srectl agent create --name custom_agent --instructions "Custom agent with extra properties" --tools CustomTool --temperature 0.8 --max-reflection-count 2 --common-prompts format_guidelines
```
**Expected:** Creates agent with custom properties in YAML

#### 9. Agent Validation - Valid File
**Test:** Validate a correctly formatted agent file
```bash
srectl agent validate --file agents/test_agent/test_agent.yaml
```
**Expected:** Shows "Agent validation passed" message

#### 10. Agent Validation - All Agents
**Test:** Validate all agents in the agents directory
```bash
srectl agent validate --all
```
**Expected:** Shows validation results for all agent files

#### 11. Tool Creation
**Test:** Create a basic tool
```bash
srectl tool create --name TestTool --type KustoQuery --extra description "Test tool"
```
**Expected:** Creates `tools/TestTool/TestTool.yaml`

#### 7. Tool Validation
**Test:** Validate a tool file
```bash
srectl tool validate --name TestTool
```
**Expected:** Shows tool validation results

#### 8. List Agents
**Test:** List all agents from the remote server
```bash
srectl list agents
```
**Expected:** 
- Displays formatted list of agents with names, descriptions, and metadata
- Shows agent tools and handoffs
- Displays total count and pagination info
- Requires valid CLI configuration

#### 9. List Tools
**Test:** List all tools from the remote server
```bash
srectl list tools
```
**Expected:** 
- Displays formatted list of tools with names, categories, and descriptions
- Shows tool parameters and plugin information
- Displays total count
- Requires valid CLI configuration

#### 9.1. List Extended Tools
**Test:** List all extended tools from the remote server
```bash
srectl list extended-tools
```
**Expected:** 
- Displays formatted list of extended/custom tools applied through the CLI
- Shows same information format as regular tools
- Displays tools that were added via `srectl tool apply` command
- Requires valid CLI configuration

### Thread Management Test Cases

#### 10. Create New Thread
**Test:** Start a new conversation thread with the agent
```bash
srectl thread new --message "Help me troubleshoot a Redis container that keeps restarting"
```
**Expected:** 
- Creates new thread with unique ID
- Sends initial message to agent
- Displays agent response in real-time
- Shows thread ID for future reference

#### 11. Continue Thread
**Test:** Continue an existing conversation thread
```bash
srectl thread continue --message "The container is still restarting after applying your suggestions"
```
**Expected:** 
- Uses most recent thread ID
- Sends follow-up message maintaining conversation context
- Displays agent response with conversation history

#### 12. List All Threads
**Test:** Display all conversation threads
```bash
srectl thread list
```
**Expected:** 
- Shows all threads with IDs, titles, and timestamps
- Displays creation date and last activity
- Shows total thread count

#### 13. Track Thread in Real-Time
**Test:** Monitor a thread for new messages in real-time
```bash
srectl thread track --thread-id thread_abc123
```
**Expected:** 
- Continuously monitors thread for new messages
- Displays new messages as they arrive with timestamps
- Shows status indicators and real-time updates
- Can be stopped with Ctrl+C

#### 14. Delete Thread
**Test:** Delete a specific conversation thread
```bash
srectl thread delete --thread-id thread_abc123
```
**Expected:** 
- Removes specified thread permanently
- Shows confirmation message
- Thread no longer appears in list

### Error Handling Test Cases

#### 15. Agent Creation - Missing Required Parameters
**Test:** Try to create agent without required parameters
```bash
srectl agent create --name test_agent
```
**Expected:** Shows error about missing required parameters

#### 16. Agent Creation - Invalid Name
**Test:** Try to create agent with invalid name (contains spaces)
```bash
srectl agent create --name "invalid name" --instructions "Test" --tools Tool1
```
**Expected:** Shows validation error about invalid name

#### 17. Agent Creation - Invalid Instructions Length
**Test:** Try to create agent with too short instructions
```bash
srectl agent create --name test_agent --instructions "short" --tools Tool1
```
**Expected:** Shows validation error about instructions length

#### 18. Agent Creation - Invalid Temperature
**Test:** Try to create agent with invalid temperature
```bash
srectl agent create --name test_agent --instructions "Valid instructions for testing" --tools Tool1 --temperature 5.0
```
**Expected:** Shows validation error about temperature range

#### 18. Agent Creation - No Tools
**Test:** Try to create agent without tools
```bash
srectl agent create --name test_agent --instructions "Valid instructions for testing" --tools
```
**Expected:** Shows validation error about missing tools

#### 15. Agent Validation - Invalid File
**Test:** Try to validate a non-existent file
```bash
srectl agent validate --file non_existent_file.yaml
```
**Expected:** Shows file not found error

#### 16. Agent Validation - No Agents Directory
**Test:** Run validation in directory without agents folder
```bash
srectl agent validate --all
```
**Expected:** Shows "No agents directory found" message

### Directory Structure Test Cases

#### 17. Agent Directory Creation
**Test:** Verify agent directory structure is created correctly
```bash
srectl agent create --name snake_case_test --instructions "Test snake case conversion" --tools Tool1 --allow-parallel-tool-calls --max-reflection-count 1
```
**Expected:** YAML contains `allow_parallel_tool_calls` and `max_reflection_count` (not camelCase)

#### 18. Tool Directory Creation
**Test:** Verify tool directory structure is created correctly
```bash
srectl tool create --name DirectoryTestTool --type Test
```
**Expected:** Creates `tools/DirectoryTestTool/` directory with `DirectoryTestTool.yaml` file

### YAML Format Test Cases

#### 19. Snake Case Conversion
**Test:** Verify properties are converted to snake_case in YAML output
```bash
srectl agent create --name snake_case_test --instructions "Test snake case conversion" --tools Tool1 --allow-parallel-tool-calls --max-reflection-count 1
```
**Expected:** YAML contains `allow_parallel_tool_calls` and `max_reflection_count` (not camelCase)

#### 20. Boolean Properties
**Test:** Verify boolean properties are handled correctly
```bash
srectl agent create --name boolean_test --instructions "Test boolean properties" --tools Tool1 --allow-parallel-tool-calls --critic-on-handoff
```
**Expected:** YAML contains `allow_parallel_tool_calls: true` and `critic_on_handoff: true`

### Automated Test Cases

The following test cases are automated in the `srectl_tests.bat` file and can be run to verify SRECTL functionality:

1. **Basic agent creation and validation**
2. **Agent creation with all options**
3. **Tool creation and validation**
4. **Error handling for invalid inputs**
5. **Directory structure verification**
6. **YAML format validation**
7. **Bulk validation operations**
8. **Remote server list functionality (agents and tools)**
9. **CLI configuration and authentication**

### Running Automated Tests

To run all automated tests:
```bash
srectl_tests.bat
```

The batch file will:
- Create test agents with various configurations
- Validate created agents
- Test error conditions
- Clean up test files
- Report success/failure for each test case

**Example output:**
```
=====================================
SRECTL Automated Test Suite
=====================================
Starting CLI tests...
...
=====================================
TEST SUMMARY
=====================================
Total Tests: 25
Passed: 25
Failed: 0
[SUCCESS] All tests passed!
The SRECTL is functioning correctly.
```

### Test Environment Requirements

- Windows environment with PowerShell
- SRECTL built and available in PATH or current directory
- Write permissions in the current directory
- .NET 9 runtime installed

---

## Apply YAML Directly

You can apply any YAML file directly to the remote server using:

```
srectl apply-yaml --file <path-to-yaml-file>
```

This command will send the file as-is to the API endpoint, bypassing any local parsing or validation. Use this for advanced scenarios or bulk configuration updates.

