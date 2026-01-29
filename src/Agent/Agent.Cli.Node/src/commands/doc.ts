/**
 * /doc Command - Interactive Documentation Browser
 *
 * Browse embedded documentation topics with an interactive menu
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandContext, CommandResult, WizardConfig } from './types';

// ============================================================================
// DOCUMENTATION CONTENT
// ============================================================================

interface DocTopic {
  id: string;
  title: string;
  description: string;
  content: string;
}

const DOCUMENTATION: DocTopic[] = [
  {
    id: 'overview',
    title: 'Overview',
    description: 'SRE Agent platform overview and architecture',
    content: `# SRE Agent Platform Overview

The SRE Agent platform provides intelligent automation for Site Reliability Engineering tasks.

## Key Components

- **Agents**: AI-powered assistants configured for specific tasks
- **Tools**: Actions agents can perform (API calls, scripts, etc.)
- **Skills**: Reusable prompt patterns and workflows
- **Incidents**: Integration with incident management systems

## Architecture

┌─────────────────────────────────────────────────────────┐
│                      CLI / Web Portal                    │
├─────────────────────────────────────────────────────────┤
│                     Agent Runtime                        │
├──────────────┬──────────────┬──────────────┬────────────┤
│    Agents    │    Tools     │    Skills    │  Incidents │
└──────────────┴──────────────┴──────────────┴────────────┘

## Getting Started

1. Run /init to configure your workspace
2. Use /agent create to create your first agent
3. Test with /agent test <name>
4. Deploy with /agent apply <name>`,
  },
  {
    id: 'agents',
    title: 'Agents',
    description: 'How to create and configure agents',
    content: `# Working with Agents

Agents are AI-powered assistants configured for specific tasks.

## Creating an Agent

\`\`\`bash
/agent create
\`\`\`

This starts an interactive wizard to create a new agent.

## Agent YAML Structure

\`\`\`yaml
apiVersion: srectl.agent/v2
kind: ExtendedAgent
metadata:
  name: my_agent
spec:
  instructions: |
    You are a helpful assistant...
  handoffDescription: Handles customer inquiries
  tools:
    - GetServiceHealth
    - RunKustoQuery
  handoffs:
    - escalation_agent
  allowParallelToolCalls: true
  maxReflectionCount: 3
  temperature: 0.7
\`\`\`

## Key Properties

- **instructions**: The system prompt for the agent
- **tools**: List of tools the agent can use
- **handoffs**: Other agents this agent can delegate to
- **temperature**: Creativity level (0-1)

## Commands

| Command | Description |
|---------|-------------|
| /agent list | List all agents |
| /agent create | Create new agent |
| /agent edit <name> | Edit agent |
| /agent delete <name> | Delete agent |
| /agent apply <name> | Deploy to server |
| /agent test <name> | Test agent |`,
  },
  {
    id: 'tools',
    title: 'Tools',
    description: 'Available tool types and configuration',
    content: `# Tools

Tools are actions that agents can perform.

## Tool Types

### 1. API Tools
Make HTTP requests to external services.

\`\`\`yaml
apiVersion: srectl.tool/v2
kind: Tool
metadata:
  name: GetServiceHealth
spec:
  type: api
  endpoint: https://api.example.com/health
  method: GET
\`\`\`

### 2. Script Tools
Execute scripts (PowerShell, Python, Bash).

\`\`\`yaml
apiVersion: srectl.tool/v2
kind: Tool
metadata:
  name: RunDiagnostics
spec:
  type: script
  runtime: powershell
  script: |
    Get-Service | Where-Object Status -eq "Stopped"
\`\`\`

### 3. Kusto Tools
Query Azure Data Explorer.

\`\`\`yaml
apiVersion: srectl.tool/v2
kind: Tool
metadata:
  name: QueryLogs
spec:
  type: kusto
  cluster: https://mycluster.kusto.windows.net
  database: Logs
\`\`\`

## Commands

| Command | Description |
|---------|-------------|
| /tool list | List all tools |
| /tool create | Create new tool |
| /tool edit <name> | Edit tool |
| /tool test <name> | Test tool |`,
  },
  {
    id: 'skills',
    title: 'Skills',
    description: 'Skill creation and management',
    content: `# Skills

Skills are reusable prompt patterns and workflows.

## What are Skills?

Skills let you:
- Define reusable prompt templates
- Create multi-step workflows
- Share common patterns across agents

## Creating a Skill

\`\`\`bash
/skill create
\`\`\`

## Skill YAML Structure

\`\`\`yaml
apiVersion: srectl.skill/v2
kind: Skill
metadata:
  name: incident_summary
spec:
  description: Summarize an incident
  prompt: |
    Analyze the following incident and provide:
    1. Root cause analysis
    2. Impact assessment
    3. Recommended actions

    Incident: {{incident}}
  parameters:
    - name: incident
      type: string
      required: true
\`\`\`

## Using Skills

Skills can be invoked by agents or directly:

\`\`\`bash
/skill run incident_summary --incident "Database connection timeout"
\`\`\`

## Commands

| Command | Description |
|---------|-------------|
| /skill list | List all skills |
| /skill create | Create new skill |
| /skill edit <name> | Edit skill |
| /skill run <name> | Run skill |`,
  },
  {
    id: 'yaml',
    title: 'YAML Reference',
    description: 'YAML schema reference for all resource types',
    content: `# YAML Schema Reference

## Common Structure

All resources follow a similar structure:

\`\`\`yaml
apiVersion: srectl.<type>/v2
kind: <Kind>
metadata:
  name: resource_name
  labels:
    environment: production
spec:
  # Resource-specific configuration
\`\`\`

## API Versions

| Resource | API Version |
|----------|-------------|
| Agent | srectl.agent/v2 |
| Tool | srectl.tool/v2 |
| Skill | srectl.skill/v2 |
| Filter | srectl.filter/v2 |
| ScheduledTask | srectl.scheduled/v2 |

## Applying YAML

Use the /apply command to deploy resources:

\`\`\`bash
/apply path/to/resource.yaml
/apply path/to/resource.yaml --dry-run
\`\`\`

## Validation

YAML files are validated against schemas before applying.
Use --dry-run to validate without deploying.`,
  },
  {
    id: 'quickstart',
    title: 'Quickstart',
    description: 'Getting started guide',
    content: `# Quickstart Guide

Get up and running with SRE Agent in 5 minutes.

## Step 1: Initialize Workspace

\`\`\`bash
/init
\`\`\`

Follow the prompts to configure your server connection.

## Step 2: Create Your First Agent

\`\`\`bash
/agent create
\`\`\`

Choose "AI-Assisted" for guided creation.

## Step 3: Test the Agent

\`\`\`bash
/agent test my_agent
\`\`\`

Send test messages to verify it works.

## Step 4: Deploy to Server

\`\`\`bash
/agent apply my_agent
\`\`\`

## Next Steps

- Explore /doc agents for detailed agent documentation
- Try /tool list to see available tools
- Use /skill create for reusable prompts

## Useful Commands

| Command | Description |
|---------|-------------|
| /help | Show all commands |
| /status | Check connection status |
| /config | View configuration |
| /doc <topic> | Read documentation |`,
  },
  {
    id: 'workflows',
    title: 'Workflows',
    description: 'Workflow patterns and examples',
    content: `# Workflow Patterns

Common patterns for building effective agent workflows.

## Pattern 1: Escalation Chain

Create agents that can escalate to specialists:

\`\`\`
Triage Agent → Database Agent
             → Network Agent
             → Security Agent
\`\`\`

Configure with handoffs:
\`\`\`yaml
handoffs:
  - database_specialist
  - network_specialist
  - security_specialist
\`\`\`

## Pattern 2: Tool Pipeline

Chain tools for complex operations:

1. Query logs with Kusto
2. Parse results with script
3. Take action via API

## Pattern 3: Scheduled Checks

Automate recurring tasks:

\`\`\`bash
/scheduled create
\`\`\`

Set up health checks, reports, or maintenance tasks.

## Pattern 4: Incident Response

Integrate with incident management:

1. Incident triggers agent
2. Agent gathers diagnostics
3. Agent suggests remediation
4. Human approves or modifies

## Best Practices

- Keep agents focused on specific domains
- Use handoffs for specialization
- Log all tool calls for auditing
- Test thoroughly before deployment`,
  },
];

// ============================================================================
// WIZARD HELPERS
// ============================================================================

/**
 * Create documentation browser wizard
 */
function createDocBrowserWizard(ctx: CommandContext): WizardConfig {
  const topicOptions = DOCUMENTATION.map(topic => ({
    key: topic.id,
    label: topic.title,
    description: topic.description,
  }));

  return {
    id: 'doc-browser',
    title: 'Documentation',
    steps: [
      {
        id: 'topic',
        title: 'Select Topic',
        prompt: 'What would you like to learn about?',
        type: 'select',
        options: topicOptions,
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      const topicId = data.topic;
      const topic = DOCUMENTATION.find(t => t.id === topicId);

      if (!topic) {
        return { success: false, message: 'Topic not found.' };
      }

      ctx.onOutput('\n' + topic.content + '\n');
      return { success: true, silent: true };
    },
  };
}

// ============================================================================
// COMMAND HANDLER
// ============================================================================

/**
 * Doc command handler
 */
async function handleDocCommand(ctx: CommandContext): Promise<CommandResult> {
  const { args, onOutput } = ctx;
  const topicArg = args[0]?.toLowerCase();

  // No argument - show interactive topic browser
  if (!topicArg) {
    return {
      success: true,
      silent: true,
      wizard: createDocBrowserWizard(ctx),
    };
  }

  // Find topic by id or partial match
  const topic = DOCUMENTATION.find(t =>
    t.id === topicArg ||
    t.id.startsWith(topicArg) ||
    t.title.toLowerCase().includes(topicArg)
  );

  if (!topic) {
    const availableTopics = DOCUMENTATION.map(t => `  /doc ${t.id} - ${t.description}`).join('\n');
    return {
      success: false,
      message: `Unknown topic: "${topicArg}"\n\nAvailable topics:\n${availableTopics}`,
    };
  }

  onOutput('\n' + topic.content + '\n');
  return { success: true, silent: true };
}

/**
 * Doc command definition
 */
const docCommand: SlashCommand = {
  name: 'doc',
  aliases: ['docs', 'documentation', 'help-topic'],
  description: 'Browse SRE Agent documentation',
  usage: '/doc [topic]',
  examples: [
    '/doc',
    '/doc agents',
    '/doc quickstart',
    '/doc yaml',
  ],
  execute: handleDocCommand,
};

/**
 * Register the doc command
 */
export function registerDocCommand(): void {
  commandRegistry.register(docCommand);
}
