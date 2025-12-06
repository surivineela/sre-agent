# SRECTL - SRE Agent CLI Instructions

This file contains comprehensive documentation for all SRECTL commands and their usage.
Generated on: 2025-12-03 07:26:39 UTC

## Table of Contents

1. [Main Command](#main-command)
2. [General Commands](#general-commands)
   - [init](#init-command)
   - [list](#list-command)
   - [apply-yaml](#apply-yaml-command)
3. [Agent Commands](#agent-commands)
   - [agent create](#agent-create-command)
   - [agent validate](#agent-validate-command)
   - [agent apply](#agent-apply-command)
   - [agent run](#agent-run-command)
4. [Tool Commands](#tool-commands)
   - [tool create](#tool-create-command)
   - [tool validate](#tool-validate-command)
   - [tool apply](#tool-apply-command)
   - [tool show-types](#tool-show-types-command)
   - [tool show-connectors](#tool-show-connectors-command)
5. [Skills Commands](#skills-commands)
   - [skill create](#skill-create-command)
   - [skill upload](#skill-upload-command)
   - [skill list](#skill-list-command)
   - [skill delete](#skill-delete-command)
   - [skill convert](#skill-convert-command)
   - [skill download](#skill-download-command)

## Main Command

### Main Command {#main-command}

```
$ srectl --help

Description:
  SRE Agent CLI - Your intelligent assistant for managing SRE agents and automating incident response
  
  Incident Handler Commands: create, map-agent, list (run 'srectl incidenthandler --help' for details)

Usage:
  srectl [command] [options]

Options:
  -?, -h, --help  Show help and usage information
  --version       Show version information
  --debug         Enable debug logging
  --quiet         Minimize output

Commands:
  welcome          Show welcome screen and getting started guide
  help <topic>     Interactive help system with examples and troubleshooting
  status           Show workspace status and health check
  interactive      Start interactive guided mode for step-by-step assistance
  version          Show version information and build details
  init             Initialize SREAgent CLI configuration and workspace
  
                   Examples:
                     # Initialize with local development server
                     srectl init --resource-url https://localhost:7023
  
                     # Initialize with remote server
                     srectl init --resource-url https://my-sreagent-dev.1abcdef.eastus2.azuresre.ai
  
                     # Initialize with production environment
                     srectl init --resource-url https://my-sreagent-prod.2abcdef.eastus2.azuresre.ai
  sync             Sync agents and tools YAML from the remote server into the local workspace (agents/, tools/)
  list             List various resources from the remote server
  
                   Examples:
                     # List all agents on the server
                     srectl list agents
  
                     # List all tools on the server
                     srectl list tools
  
                     # List extended tools (user-added)
                     srectl list extended-tools
  
                     # List data connectors
                     srectl list data-connectors
  apply-yaml       Apply any YAML configuration file to the server
  
                   Examples:
                     # Apply an agent YAML file
                     srectl apply-yaml --file agents/MyAgent/MyAgent.yaml
  
                     # Apply a tool YAML file
                     srectl apply-yaml --file tools/CustomTool/CustomTool.yaml
  
                     # Apply any configuration file
                     srectl apply-yaml --file configs/my-config.yaml
  thread           Thread management commands
  chat             Start an interactive chat session with the SRE Agent
  
                   Examples:
                     # Start interactive chat
                     srectl chat
  
                     # Start chat with debug logging
                     srectl chat --debug
  
                     # Start chat with minimal output
                     srectl chat --quiet
  agent            Agent commands for managing SRE automation agents
  tool             Tool commands for managing SRE automation tools
  doc              Document management commands. Upload and manage documents like TSGs, architecture docs, runbooks, and other reference materials for agents to use
  profile          Profile management commands. Profiles store connection settings for different SRE Agent instances (local or remote)
  skill            Skill management commands. Upload and manage custom skills for agents to use, or convert an existing agent into a skill.
  incidenthandler  Manage incident handlers and filters
  scheduledtask    Manage scheduled tasks for automated agent operations
  extension        Extension commands for generating deployment files and configurations
```

## General Commands

### init Command {#init-command}

```
$ srectl init --help

Description:
  Initialize SREAgent CLI configuration and workspace
  
  Examples:
    # Initialize with local development server
    srectl init --resource-url https://localhost:7023
  
    # Initialize with remote server
    srectl init --resource-url https://my-sreagent-dev.1abcdef.eastus2.azuresre.ai
  
    # Initialize with production environment
    srectl init --resource-url https://my-sreagent-prod.2abcdef.eastus2.azuresre.ai

Usage:
  srectl init [options]

Options:
  --resource-url (REQUIRED)  Base URL of the SRE Agent server
  --debug                    Enable debug logging
  --quiet                    Minimize output
  -?, -h, --help             Show help and usage information
```

### list Command {#list-command}

```
$ srectl list --help

Description:
  List various resources from the remote server
  
  Examples:
    # List all agents on the server
    srectl list agents
  
    # List all tools on the server
    srectl list tools
  
    # List extended tools (user-added)
    srectl list extended-tools
  
    # List data connectors
    srectl list data-connectors

Usage:
  srectl list [command] [options]

Options:
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information

Commands:
  agents            List remote extended agents from the server
  extended-tools    List all extended tools added to the server through apply command
  data-connectors   List all data connectors configured on the server
  incidenthandlers  List all incident handlers from the remote server
```

### list agents Command {#list-agents-command}

```
$ srectl list agents --help

Description:
  List remote extended agents from the server

Usage:
  srectl list agents [options]

Options:
  --debug         Enable verbose debug logging for network calls and operations
  --all
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information
```

### list tools Command {#list-tools-command}

```
$ srectl list tools --help


┌──────────────────────────────────────────────────────────────────────────────┐
│ ✻ SRE Agent CLI (srectl)                                                     │
│                                                                              │
│   Your intelligent assistant for Incident Diagnosis and automation           │
│                                                                              │
│   cwd: Q:\Git\sreagent-runtime                                               │
└──────────────────────────────────────────────────────────────────────────────┘

✗ Unrecognized command or argument: 'tools'

  • Did you mean: srectl tool … ?

Valid subcommands for 'list'
────────────────────────────
srectl list agents          : List remote extended agents from the server
srectl list extended-tools  : List all extended tools added to the server through apply command
srectl list data-connectors : List all data connectors configured on the server
srectl list incidenthandlers: List all incident handlers from the remote server

Options
───────
----debug                   : Enable debug logging
----quiet                   : Minimize output

  • Use 'srectl list --help' for details
```

### apply-yaml Command {#apply-yaml-command}

```
$ srectl apply-yaml --help

Description:
  Apply any YAML configuration file to the server
  
  Examples:
    # Apply an agent YAML file
    srectl apply-yaml --file agents/MyAgent/MyAgent.yaml
  
    # Apply a tool YAML file
    srectl apply-yaml --file tools/CustomTool/CustomTool.yaml
  
    # Apply any configuration file
    srectl apply-yaml --file configs/my-config.yaml

Usage:
  srectl apply-yaml [options]

Options:
  --file          Path to the YAML file to apply
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information
```

## Agent Commands

### agent Command {#agent-command}

```
$ srectl agent --help

Description:
  Agent commands for managing SRE automation agents

Usage:
  srectl agent [command] [options]

Options:
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information

Commands:
  create    Create a new agent YAML configuration file
  
            Examples:
              # Create a basic agent
              srectl agent create --name DevOpsAgent --instructions "Help with DevOps tasks such as monitoring and incident response"
  
              # Create an agent with tools
              srectl agent create --name KustoAgent --tools QueryKusto AnalyzeMetrics
  
              # Create an agent with AI assistance (smart mode)
              srectl agent create --name StorageAgent --smart --instructions "Help troubleshoot Azure Storage issues"
  
              # Create an advanced agent with all options
              srectl agent create --name AdvancedAgent \
                --instructions "Complex multi-step agent" \
                --tools Tool1 Tool2 \
                --handoffs Agent1 Agent2 \
                --temperature 0.7 \
                --max-reflection-count 3
  validate  Validate agent YAML configuration files
  
            Examples:
              # Validate by agent name (searches in agents/ folder)
              srectl agent validate --name MyAgent
  
              # Validate specific agent by name and check tools
              srectl agent validate --name KustoAgent --check-tools
  
              # Validate all agent files
              srectl agent validate --all
  
              # Validate with tool availability checking
              srectl agent validate --all --check-tools
  
              # Alternative: Validate a specific agent file path
              srectl agent validate --file agents/MyAgent/MyAgent.yaml
  apply     Apply an agent configuration to the remote server
  
            Examples:
              # Apply an agent to the server
              srectl agent apply --name DevOpsAgent
  
              # Preview what would be applied (dry run)
              srectl agent apply --name KustoAgent --dry-run
  
              # Apply with debug logging
              srectl agent apply --name MyAgent --debug
  delete    Delete an agent from the remote server
  
            Examples:
              # Delete an agent from the server
              srectl agent delete --name OldAgent
  
              # Delete with debug logging
              srectl agent delete --name TestAgent --debug
  test      Test an agent with a specific message
  
            Examples:
              # Test an agent with a simple message
              srectl agent test --name DevOpsAgent --message "Check pod status in namespace production"
  
              # Test without waiting for response
              srectl agent test --name KustoAgent --message "Query memory usage" --no-wait
  
              # Test with custom user details
              srectl agent test --name MyAgent --message "Help me" --user-id john.doe --display-name "John Doe"
  diff      Compare local and remote agent configurations
  
            Examples:
              # Compare default using git-diff (default)
              srectl agent diff --name DevOpsAgent
  
              # Use VS Code diff
              srectl agent diff --name KustoAgent --tool code
  
              # Show inline diff
              srectl agent diff --name MyAgent --raw
  migrate   Migrate V1 agent format to V2
  list      List remote extended agents from the server
```

### agent create Command {#agent-create-command}

```
$ srectl agent create --help

Description:
  Create a new agent YAML configuration file
  
  Examples:
    # Create a basic agent
    srectl agent create --name DevOpsAgent --instructions "Help with DevOps tasks such as monitoring and incident response"
  
    # Create an agent with tools
    srectl agent create --name KustoAgent --tools QueryKusto AnalyzeMetrics
  
    # Create an agent with AI assistance (smart mode)
    srectl agent create --name StorageAgent --smart --instructions "Help troubleshoot Azure Storage issues"
  
    # Create an advanced agent with all options
    srectl agent create --name AdvancedAgent \
      --instructions "Complex multi-step agent" \
      --tools Tool1 Tool2 \
      --handoffs Agent1 Agent2 \
      --temperature 0.7 \
      --max-reflection-count 3

Usage:
  srectl agent create [options]

Options:
  --name (REQUIRED)
  --instructions
  --tools
  --handoff-description
  --handoffs
  --allow-parallel-tool-calls
  --max-reflection-count
  --critic-prompt-path
  --critic-on-handoff
  --custom-reflection-note
  --common-prompts
  --temperature
  --output-type
  --vanilla-mode
  --smart                      Use AI to automatically generate instructions and recommend tools
  --enable-skills              Enable skills for the agent [default: False]
  --add-system-skills          Add system skills to the agent. Only applicable if skills are enabled with --enable-skills. This is not recommended for custom meta-agents as system skills may interfere with intended behavior. [default: False]
  --debug                      Enable debug logging
  --quiet                      Minimize output
  -?, -h, --help               Show help and usage information
```

### agent validate Command {#agent-validate-command}

```
$ srectl agent validate --help

Description:
  Validate agent YAML configuration files
  
  Examples:
    # Validate by agent name (searches in agents/ folder)
    srectl agent validate --name MyAgent
  
    # Validate specific agent by name and check tools
    srectl agent validate --name KustoAgent --check-tools
  
    # Validate all agent files
    srectl agent validate --all
  
    # Validate with tool availability checking
    srectl agent validate --all --check-tools
  
    # Alternative: Validate a specific agent file path
    srectl agent validate --file agents/MyAgent/MyAgent.yaml

Usage:
  srectl agent validate [options]

Options:
  --name          Agent name to validate
  --file
  --all
  --check-tools   Validate that all referenced tools exist locally or on the remote server
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information
```

### agent apply Command {#agent-apply-command}

```
$ srectl agent apply --help

Description:
  Apply an agent configuration to the remote server
  
  Examples:
    # Apply an agent to the server
    srectl agent apply --name DevOpsAgent
  
    # Preview what would be applied (dry run)
    srectl agent apply --name KustoAgent --dry-run
  
    # Apply with debug logging
    srectl agent apply --name MyAgent --debug

Usage:
  srectl agent apply [options]

Options:
  --name (REQUIRED)
  --dry-run          Show what would be applied without making changes
  --debug            Enable debug logging
  --quiet            Minimize output
  -?, -h, --help     Show help and usage information
```

### agent run Command {#agent-run-command}

```
$ srectl agent run --help


┌──────────────────────────────────────────────────────────────────────────────┐
│ ✻ SRE Agent CLI (srectl)                                                     │
│                                                                              │
│   Your intelligent assistant for Incident Diagnosis and automation           │
│                                                                              │
│   cwd: Q:\Git\sreagent-runtime                                               │
└──────────────────────────────────────────────────────────────────────────────┘

✗ Unrecognized command or argument: 'run'

Valid subcommands for 'agent'
─────────────────────────────
srectl agent create         : Create a new agent YAML configuration file

Examples:
  # Create a basic agent
  srectl agent create --name DevOpsAgent --instructions "Help with DevOps tasks such as monitoring and incident response"

  # Create an agent with tools
  srectl agent create --name KustoAgent --tools QueryKusto AnalyzeMetrics

  # Create an agent with AI assistance (smart mode)
  srectl agent create --name StorageAgent --smart --instructions "Help troubleshoot Azure Storage issues"

  # Create an advanced agent with all options
  srectl agent create --name AdvancedAgent \
    --instructions "Complex multi-step agent" \
    --tools Tool1 Tool2 \
    --handoffs Agent1 Agent2 \
    --temperature 0.7 \
    --max-reflection-count 3
srectl agent validate       : Validate agent YAML configuration files

Examples:
  # Validate by agent name (searches in agents/ folder)
  srectl agent validate --name MyAgent

  # Validate specific agent by name and check tools
  srectl agent validate --name KustoAgent --check-tools

  # Validate all agent files
  srectl agent validate --all

  # Validate with tool availability checking
  srectl agent validate --all --check-tools

  # Alternative: Validate a specific agent file path
  srectl agent validate --file agents/MyAgent/MyAgent.yaml
srectl agent apply          : Apply an agent configuration to the remote server

Examples:
  # Apply an agent to the server
  srectl agent apply --name DevOpsAgent

  # Preview what would be applied (dry run)
  srectl agent apply --name KustoAgent --dry-run

  # Apply with debug logging
  srectl agent apply --name MyAgent --debug
srectl agent delete         : Delete an agent from the remote server

Examples:
  # Delete an agent from the server
  srectl agent delete --name OldAgent

  # Delete with debug logging
  srectl agent delete --name TestAgent --debug
srectl agent test           : Test an agent with a specific message

Examples:
  # Test an agent with a simple message
  srectl agent test --name DevOpsAgent --message "Check pod status in namespace production"

  # Test without waiting for response
  srectl agent test --name KustoAgent --message "Query memory usage" --no-wait

  # Test with custom user details
  srectl agent test --name MyAgent --message "Help me" --user-id john.doe --display-name "John Doe"
srectl agent diff           : Compare local and remote agent configurations

Examples:
  # Compare default using git-diff (default)
  srectl agent diff --name DevOpsAgent

  # Use VS Code diff
  srectl agent diff --name KustoAgent --tool code

  # Show inline diff
  srectl agent diff --name MyAgent --raw
srectl agent migrate        : Migrate V1 agent format to V2
srectl agent list           : List remote extended agents from the server

Options
───────
----debug                   : Enable debug logging
----quiet                   : Minimize output

  • Use 'srectl agent --help' for details
```

## Tool Commands

### tool Command {#tool-command}

```
$ srectl tool --help

Description:
  Tool commands for managing SRE automation tools

Usage:
  srectl tool [command] [options]

Options:
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information

Commands:
  create           Create a new tool YAML configuration file
  
                   Examples:
                     # Create a basic Kusto tool (currently only KustoTool is supported)
                     srectl tool create --name QueryMetrics --type KustoTool
  
                     # Create a tool with custom path organization
                     srectl tool create --name StorageOps --type KustoTool --path "Storage/Operations"
  
                     # Create a tool with extra parameters
                     srectl tool create --name CustomTool --type KustoTool --extra database:LogsDB cluster:prod-cluster
  
                     # View available tool types first
                     srectl tool show-types
  validate         Validate tool YAML configuration files
  
                   Examples:
                     # Validate a specific tool
                     srectl tool validate --name QueryMetrics
  
                     # Validate all tools
                     srectl tool validate --all
  
                     # Validate with debug output
                     srectl tool validate --name MyTool --debug
  apply            Apply a tool configuration to the remote server
  
                   Examples:
                     # Apply a tool to the server
                     srectl tool apply --name QueryMetrics
  
                     # Preview what would be applied (dry run)
                     srectl tool apply --name StorageOps --dry-run
  
                     # Apply with debug logging
                     srectl tool apply --name CustomTool --debug
  delete           Delete a tool from the remote server
  
                   Examples:
                     # Delete a tool from the server
                     srectl tool delete --name OldTool
  
                     # Preview what would be deleted (dry run)
                     srectl tool delete --name TestTool --dry-run
  
                     # Delete with debug logging
                     srectl tool delete --name UnusedTool --debug
  diff             Compare local and remote tool configurations
  
                   Examples:
                     # Compare default using git
                     srectl tool diff --name QueryMetrics
  
                     # Use VS Code diff
                     srectl tool diff --name MyTool --tool code
  
                     # Show inline diff
                     srectl tool diff --name MyTool --raw
  show-types       Display available tool types and their details
  
                   Examples:
                     # List all available tool types
                     srectl tool show-types
  
                     # Show detailed information for all types
                     srectl tool show-types --verbose
  
                     # Show details for a specific tool type
                     srectl tool show-types --type KustoTool
  
                     # Show specific type with verbose details
                     srectl tool show-types --type AzureTool --verbose
  show-connectors  Display configured data connectors (names to use in YAML) and available connector types
  
                   Examples:
                     # List all available connectors
                     srectl tool show-connectors
  
                     # Show detailed connector information
                     srectl tool show-connectors --verbose
  list             List all tools from the remote server
```

### tool create Command {#tool-create-command}

```
$ srectl tool create --help

Description:
  Create a new tool YAML configuration file
  
  Examples:
    # Create a basic Kusto tool (currently only KustoTool is supported)
    srectl tool create --name QueryMetrics --type KustoTool
  
    # Create a tool with custom path organization
    srectl tool create --name StorageOps --type KustoTool --path "Storage/Operations"
  
    # Create a tool with extra parameters
    srectl tool create --name CustomTool --type KustoTool --extra database:LogsDB cluster:prod-cluster
  
    # View available tool types first
    srectl tool show-types

Usage:
  srectl tool create [options]

Options:
  --name (REQUIRED)  ToolName
  --type (REQUIRED)  ToolType
  --path             Custom path under tools directory (e.g., 'StorageOperations')
  --extra            AdditionArgumentsKeyValuePairs
  --debug            Enable debug logging
  --quiet            Minimize output
  -?, -h, --help     Show help and usage information
```

### tool validate Command {#tool-validate-command}

```
$ srectl tool validate --help

Description:
  Validate tool YAML configuration files
  
  Examples:
    # Validate a specific tool
    srectl tool validate --name QueryMetrics
  
    # Validate all tools
    srectl tool validate --all
  
    # Validate with debug output
    srectl tool validate --name MyTool --debug

Usage:
  srectl tool validate [options]

Options:
  --name          ToolName
  --all           ValidateAllYAMLFilesInToolsDirectory
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information
```

### tool apply Command {#tool-apply-command}

```
$ srectl tool apply --help

Description:
  Apply a tool configuration to the remote server
  
  Examples:
    # Apply a tool to the server
    srectl tool apply --name QueryMetrics
  
    # Preview what would be applied (dry run)
    srectl tool apply --name StorageOps --dry-run
  
    # Apply with debug logging
    srectl tool apply --name CustomTool --debug

Usage:
  srectl tool apply [options]

Options:
  --name (REQUIRED)
  --dry-run          Show what would be applied without making changes
  --debug            Enable debug logging
  --quiet            Minimize output
  -?, -h, --help     Show help and usage information
```

### tool show-types Command {#tool-show-types-command}

```
$ srectl tool show-types --help

Description:
  Display available tool types and their details
  
  Examples:
    # List all available tool types
    srectl tool show-types
  
    # Show detailed information for all types
    srectl tool show-types --verbose
  
    # Show details for a specific tool type
    srectl tool show-types --type KustoTool
  
    # Show specific type with verbose details
    srectl tool show-types --type AzureTool --verbose

Usage:
  srectl tool show-types [options]

Options:
  --verbose       Show detailed information including assembly and namespace
  --type          Show details for a specific tool type
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information
```

### tool show-connectors Command {#tool-show-connectors-command}

```
$ srectl tool show-connectors --help

Description:
  Display configured data connectors (names to use in YAML) and available connector types
  
  Examples:
    # List all available connectors
    srectl tool show-connectors
  
    # Show detailed connector information
    srectl tool show-connectors --verbose

Usage:
  srectl tool show-connectors [options]

Options:
  --verbose       Show detailed information including assembly and namespace
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information
```

## Skills Commands

### skill Command {#skill-command}

```
$ srectl skill --help

Description:
  Skill management commands. Upload and manage custom skills for agents to use, or convert an existing agent into a skill.

Usage:
  srectl skill [command] [options]

Options:
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information

Commands:
  create    Create a new skill directory with template files
  upload    Upload a custom skill or multiple skills from a directory
  convert   Convert an existing agent to a skill
  list      List all available skills
  download  Download a skill from the server
  delete    Delete a skill from the server
```

### skill create Command {#skill-create-command}

```
$ srectl skill create --help

Description:
  Create a new skill directory with template files

Usage:
  srectl skill create [options]

Options:
  --name (REQUIRED)  Name of the skill to create
  --output-path      Output path for the created skill (default: skills/{skill-name})
  --debug            Enable verbose debug logging for network calls and operations
  --debug            Enable debug logging
  --quiet            Minimize output
  -?, -h, --help     Show help and usage information
```

### skill upload Command {#skill-upload-command}

```
$ srectl skill upload --help

Description:
  Upload a custom skill or multiple skills from a directory

Usage:
  srectl skill upload [options]

Options:
  --path          Path to a single skill directory to upload (e.g., skills/my-skill)
  --folder        Path to a folder containing multiple skill directories to upload
  --debug         Enable verbose debug logging for network calls and operations
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information
```

### skill list Command {#skill-list-command}

```
$ srectl skill list --help

Description:
  List all available skills

Usage:
  srectl skill list [options]

Options:
  --limit         Number of skills per page (1-200, default: 50) [default: 50]
  --page          Page number (1-based, default: 1) [default: 1]
  --search        Search skills by name or description
  --debug         Enable verbose debug logging for network calls and operations
  --debug         Enable debug logging
  --quiet         Minimize output
  -?, -h, --help  Show help and usage information
```

### skill delete Command {#skill-delete-command}

```
$ srectl skill delete --help

Description:
  Delete a skill from the server

Usage:
  srectl skill delete [options]

Options:
  --name (REQUIRED)  Name of the skill to delete
  --debug            Enable verbose debug logging for network calls and operations
  --debug            Enable debug logging
  --quiet            Minimize output
  -?, -h, --help     Show help and usage information
```

### skill convert Command {#skill-convert-command}

```
$ srectl skill convert --help

Description:
  Convert an existing agent to a skill

Usage:
  srectl skill convert [options]

Options:
  --agent-name (REQUIRED)  Name of the agent to convert to a skill
  --top-level-agents       List of top-level agent names for handoff context
  --output-path            Output path for the generated skill (default: skills/{agent-name})
  --debug                  Enable verbose debug logging for network calls and operations
  --debug                  Enable debug logging
  --quiet                  Minimize output
  -?, -h, --help           Show help and usage information
```

### skill download Command {#skill-download-command}

```
$ srectl skill download --help

Description:
  Download a skill from the server

Usage:
  srectl skill download [options]

Options:
  --name (REQUIRED)  Name of the skill to download
  --output-path      Output path for the downloaded skill (default: skills/{skill-name})
  --debug            Enable verbose debug logging for network calls and operations
  --debug            Enable debug logging
  --quiet            Minimize output
  -?, -h, --help     Show help and usage information
```

## Common Usage Examples

### Quick Start

```bash
# Initialize SRECTL with a resource URL
srectl init --resource-url https://localhost:7023

# Create a new agent
srectl agent create --name my_agent --instructions "Agent instructions" --tools MyTool

# Validate the agent
srectl agent validate --name my_agent

# Apply the agent to the server
srectl agent apply --name my_agent
```

### Creating Tools

```bash
# Create a basic tool
srectl tool create --name MyTool --type KustoQuery

# Create a KustoTool with auto-generated template
srectl tool create --name GetServiceLogs --type KustoTool

# Validate and apply the tool
srectl tool validate --name MyTool
srectl tool apply --name MyTool
```

### Smart Agent Generation

```bash
# Use AI to generate comprehensive agent instructions and recommended tools
srectl agent create --name "RedisContainerAppDown" --smart

# Smart generation with custom guidance
srectl agent create --name "DatabasePerformanceIssue" --smart \
  --instructions "Focus on PostgreSQL performance optimization"
```

### Working with Skills

```bash
# Create a new skill with template files
srectl skill create --name my-skill

# Edit the generated files:
# - skills/my-skill/metadata.yaml (add tools and description)
# - skills/my-skill/SKILL.md (add instructions and workflows)

# Upload the skill to the server
srectl skill upload --path skills/my-skill

# Convert an existing agent to a skill
srectl skill convert --agent-name my-agent

# List all available skills
srectl skill list
```

### Remote Server Operations

```bash
# List all agents on the remote server
srectl list agents

# List all tools on the remote server
srectl list tools

# Apply a YAML file directly
srectl apply-yaml --file my-config.yaml
```

---

*This file was automatically generated by `srectl init`. For the most up-to-date information,*
*refer to the individual command help outputs using `srectl <command> --help`.*
