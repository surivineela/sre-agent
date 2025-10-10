# SRECTL CLI Examples and Consistency Guide

This guide demonstrates how to use the SRECTL CLI effectively and maintain consistency across all commands.

## Overview

The SRECTL CLI follows a consistent pattern across all commands to provide a predictable and intuitive user experience. This document outlines the consistency patterns and provides comprehensive examples.

## Consistency Patterns

### 1. Command Structure
All commands follow a hierarchical structure:
```
srectl <category> <action> [options]
```

**Categories:**
- `agent` - Agent lifecycle management
- `tool` - Tool configuration management
- `doc` - Document management
- `thread` - Conversation management
- `profile` - Environment profiles
- `init/list/chat` - General operations

**Common Actions:**
- `create` - Create new resources
- `validate` - Validate configurations
- `apply` - Deploy to server
- `delete` - Remove from server
- `list` - Display available items
- `show-*` - Display detailed information

### 2. Option Naming Conventions

**Global Options:**
- `--debug` - Enable verbose debug logging
- `--dry-run` - Preview changes without applying
- `--help` - Show command help

**Resource Identification:**
- `--name` - Specify resource name (required for most operations)
- `--file` - Specify file path
- `--type` - Specify resource type

**Behavioral Options:**
- `--all` - Apply to all resources
- `--verbose` - Show detailed information
- `--no-wait` - Don't wait for completion

### 3. Consistent Help and Examples

Every command includes:
- Clear description of purpose
- Multiple practical examples
- Common use cases
- Related commands

## Example Workflows

### Getting Started
```bash
# Initialize CLI with local development server
srectl init --resource-url https://localhost:7023

# Create your first agent using AI assistance
srectl agent create --name DevOpsAgent --smart --instructions "Help with DevOps tasks such as monitoring and incident response"

# Apply the agent to the server
srectl agent apply --name DevOpsAgent

# Test the agent
srectl agent test --name DevOpsAgent --message "Check pod status in production"
```

### Development Workflow
```bash
# Create a specialized tool
srectl tool create --name QueryPodMetrics --type KustoTool

# Validate the tool configuration
srectl tool validate --name QueryPodMetrics

# Preview deployment
srectl tool apply --name QueryPodMetrics --dry-run

# Deploy the tool
srectl tool apply --name QueryPodMetrics

# Create an agent that uses the tool
srectl agent create --name KubernetesAgent \
  --instructions "Kubernetes troubleshooting specialist" \
  --tools QueryPodMetrics

# Test the complete workflow
srectl agent test --name KubernetesAgent \
  --message "Investigate pod failures in payment-service namespace"
```

### Production Deployment
```bash
# Validate all configurations
srectl agent validate --all --check-tools
srectl tool validate --all

# Deploy with preview
srectl agent apply --name ProductionAgent --dry-run
srectl agent apply --name ProductionAgent

# Verify deployment
srectl list agents
```

## Real-World Examples

### 1. SRE Incident Response Agent

This example demonstrates a comprehensive incident response setup:

**Agent Configuration:** See `Examples/sre-incident-agent.yaml`
**Tool Configuration:** See `Examples/service-metrics-tool.yaml`

**Setup Commands:**
```bash
# Create the service metrics tool
srectl tool create --name QueryServiceMetrics --type KustoTool \
  --extra database:ProductionTelemetry cluster:prod-telemetry-cluster

# Create additional monitoring tools
srectl tool create --name CheckServiceHealth --type AzureTool \
  --path "HealthChecks/Services"

srectl tool create --name GetRecentDeployments --type KustoTool \
  --extra database:DeploymentLogs cluster:cicd-cluster

# Validate and deploy tools
srectl tool validate --all
srectl tool apply --name QueryServiceMetrics
srectl tool apply --name CheckServiceHealth
srectl tool apply --name GetRecentDeployments

# Create the incident response agent
srectl agent create --name SREIncidentAgent \
  --instructions "First-line incident response for service reliability issues" \
  --tools QueryServiceMetrics CheckServiceHealth GetRecentDeployments \
  --handoffs DatabaseSREAgent InfrastructureAgent SecurityAgent \
  --temperature 0.3 \
  --max-reflection-count 2

# Deploy and test
srectl agent apply --name SREIncidentAgent
srectl agent test --name SREIncidentAgent \
  --message "Payment service is experiencing high error rates"
```

### 2. Multi-Environment Setup

```bash
# Create profiles for different environments
srectl profile create --name local --resource-url https://localhost:7023
srectl profile create --name staging --resource-url https://staging-sreagent.company.com
srectl profile create --name production --resource-url https://prod-sreagent.company.com

# Develop locally
srectl profile set --name local
srectl agent create --name TestAgent --smart

# Deploy to staging
srectl profile set --name staging
srectl agent apply --name TestAgent --dry-run
srectl agent apply --name TestAgent

# Deploy to production
srectl profile set --name production
srectl agent validate --name TestAgent --check-tools
srectl agent apply --name TestAgent
```

## Best Practices

### 1. Command Naming
- Use descriptive names for agents and tools
- Follow kebab-case for multi-word names
- Include purpose or domain in the name (e.g., `kubernetes-sre-agent`)

### 2. Validation
- Always validate before applying: `srectl agent validate --all --check-tools`
- Use dry-run for production deployments: `--dry-run`
- Enable debug logging for troubleshooting: `--debug`

### 3. Tool Organization
- Group related tools using the `--path` option
- Use consistent naming conventions within teams
- Document tool purposes and dependencies

### 4. Agent Design
- Start with clear, specific instructions
- Use the `--smart` option for AI-assisted creation
- Configure appropriate handoffs for complex scenarios
- Set conservative temperature values for production agents

### 5. Documentation
- Upload relevant runbooks and procedures: `srectl doc upload`
- Keep documentation updated and indexed: `srectl doc reindex`
- Use searchable formats (Markdown, text)

## Troubleshooting

### Common Issues

**Connection Problems:**
```bash
# Check server connectivity
srectl list agents --debug

# Verify profile configuration
srectl profile get --debug
```

**Validation Failures:**
```bash
# Check tool dependencies
srectl agent validate --name MyAgent --check-tools --debug

# Validate individual tools
srectl tool validate --name ProblematicTool --debug
```

**Deployment Issues:**
```bash
# Use dry-run to preview changes
srectl agent apply --name MyAgent --dry-run

# Check server logs with debug output
srectl agent apply --name MyAgent --debug
```

### Debug Mode
Enable debug logging for any command by adding `--debug`:
```bash
srectl agent create --name TestAgent --debug
srectl tool apply --name TestTool --debug
srectl chat --debug
```

## Getting Help

- Use `--help` with any command: `srectl agent create --help`
- View available tool types: `srectl tool show-types`
- See command examples: `srectl agent create --help` (includes examples)
- Start interactive chat: `srectl chat`

## Contributing

When adding new commands or features, ensure:
1. Consistent option naming and behavior
2. Comprehensive help text with examples
3. Support for `--debug` and `--dry-run` where applicable
4. Clear error messages and user feedback
5. Integration with the existing command hierarchy
