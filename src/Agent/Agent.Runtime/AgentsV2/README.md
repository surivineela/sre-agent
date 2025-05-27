# YAML Agent Configuration Guide

This directory contains YAML configuration files that define agents using the Agent Framework's YAML parser. These YAML files provide a declarative way to configure agents without requiring code compilation.

## Overview

The Agent Framework supports loading agents from YAML files through the `AgentFactory` class. This allows for dynamic agent configuration, making it easier to update agent behavior, tools, and handoffs without code changes.

## YAML Agent Structure

Each YAML agent file follows this structure:

```yaml
# Required Fields
name: agent_name                    # Unique identifier for the agent
system_prompt: |                    # Multi-line system prompt for the agent
  Your agent instructions here...

# Optional Fields
handoff_description: "Description of when to handoff to this agent"
max_reflection_count: 0             # Number of reflection iterations (default: 0)

# Agent Relationships
handoffs:                           # List of other agents this agent can handoff to
  - other_agent_name
  - another_agent_name

# Tool Configuration
auto_tools:                         # Tools that are automatically available to the agent
  - ToolName1
  - ToolName2

manual_tools:                       # Tools that require explicit invocation by the agent
  - ManualToolName1
  - ManualToolName2
```

## How YAML Agents Are Processed

### 1. Loading Process

The `AgentFactory` loads YAML agents through the following process:

1. **File Discovery**: Scans the configured directory for `*.yaml` and `*.yml` files
2. **Parsing**: Uses YamlDotNet with underscored naming convention to deserialize the content
3. **Validation**: Validates required fields and tool availability
4. **Agent Creation**: Creates `Agent<TContext>` instances with the parsed configuration
5. **Handoff Resolution**: Resolves handoff relationships between agents after all agents are loaded

### 2. Key Processing Steps

#### System Prompt Enhancement
The `system_prompt` field is automatically enhanced with handoff instructions:
```csharp
agentDescriptor.Instructions = Prompt.PromptWithHandoffInstructions(agentDescriptor.SystemPrompt);
```

This adds the standard Agent SDK context about multi-agent systems and handoff mechanisms.

#### Tool Resolution
- **Auto Tools**: Tools listed here are automatically available and can be invoked by the LLM
- **Manual Tools**: Tools that require explicit function calls by the agent
- All tools must exist in the `IToolFactory` before the agent can be created

#### Handoff Creation
Handoffs are created as `Handoff<TContext>` objects that enable seamless agent transfers:
```csharp
agent.Handoffs = agentDescriptor.Handoffs.Select(h => Handoff<TContext>.Create(_agents[h])).ToList();
```

## Usage Conditions and Requirements

### Prerequisites

1. **Tool Factory Setup**: All tools referenced in `auto_tools` and `manual_tools` must be registered in the `IToolFactory`
2. **Assembly Scanning**: The agent factory must be configured with assemblies to scan for tool definitions
3. **Directory Configuration**: The YAML directory path must be provided to the `AgentFactory` constructor

### Validation Rules

The following validation occurs during agent loading:

- ✅ **Required Fields**: `name` and `system_prompt` must be present
- ✅ **Unique Names**: Agent names must be unique across all loaded agents
- ✅ **Tool Existence**: All referenced tools must exist in the tool factory
- ✅ **Handoff Validity**: All handoff targets must reference existing agents

### Error Handling

If validation fails:
- The specific agent will not be loaded
- Errors are logged with detailed information
- Other valid agents continue to load
- The factory throws exceptions for missing handoff targets during the handoff resolution phase

## Example Agents

### Basic Agent Example
```yaml
name: simple_agent
system_prompt: |
  You are a helpful assistant that can answer general questions.
  
auto_tools:
  - GetCurrentTime
  - SearchWeb

max_reflection_count: 1
```

### Agent with Handoffs Example
```yaml
name: coordinator_agent
system_prompt: |
  You are a coordinator that delegates specific tasks to specialized agents.
  
handoff_description: "Handoff to this agent for task coordination"

handoffs:
  - database_agent
  - analytics_agent

auto_tools:
  - ValidateInput
  - LogActivity
```

### Specialized Agent Example
```yaml
name: database_agent
system_prompt: |
  You are a database specialist that handles all database-related operations.
  
handoff_description: "Handoff to this agent for database operations"

manual_tools:
  - ExecuteQuery
  - BackupDatabase
  - OptimizeIndexes

max_reflection_count: 2
```

## Best Practices

### 1. Agent Design
- **Single Responsibility**: Each agent should have a clear, focused purpose
- **Clear Handoff Descriptions**: Provide meaningful descriptions for when to handoff to each agent
- **Tool Selection**: Choose between auto_tools and manual_tools based on usage patterns

### 2. System Prompts
- **Be Specific**: Clearly define the agent's role and capabilities
- **Include Context**: Provide relevant context about the domain or use case
- **Set Boundaries**: Specify what the agent should and shouldn't do

### 3. Handoff Strategy
- **Logical Flow**: Design handoff relationships that reflect natural conversation flow
- **Avoid Cycles**: Be careful with bidirectional handoffs to prevent infinite loops
- **Fallback Agents**: Consider having a meta-agent that can coordinate between specialized agents

### 4. Tool Organization
- **Auto Tools**: Use for tools that should be readily available (e.g., logging, basic utilities)
- **Manual Tools**: Use for specialized operations that require explicit decision-making

## Integration with Agent Factory

### Constructor Configuration
```csharp
var agentFactory = new AgentFactory<TContext>(
    logger,
    toolFactory,
    assembliesToScan,
    agentsYamlDirectory  // Path to this directory
);
```

Each agent is designed for specific Azure operational scenarios and includes appropriate tools and handoff relationships for seamless multi-agent collaboration.