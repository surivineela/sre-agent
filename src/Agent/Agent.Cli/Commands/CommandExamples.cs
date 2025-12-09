// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Commands;

/// <summary>
/// Provides consistent examples and usage patterns for all CLI commands.
/// This centralizes examples to ensure consistency and easier maintenance.
/// </summary>
public static class CommandExamples
{
    #region Agent Command Examples

    public static class Agent
    {
        public const string CreateDescription = @"Create a new agent YAML configuration file

Examples:
  # Create a basic agent
  srectl agent create --name DevOpsAgent --instructions ""Help with DevOps tasks such as monitoring and incident response""

  # Create an agent with tools
  srectl agent create --name KustoAgent --tools QueryKusto AnalyzeMetrics

  # Create an agent with AI assistance (smart mode)
  srectl agent create --name StorageAgent --smart --instructions ""Help troubleshoot Azure Storage issues""

  # Create an advanced agent with all options
  srectl agent create --name AdvancedAgent \
    --instructions ""Complex multi-step agent"" \
    --tools Tool1 Tool2 \
    --handoffs Agent1 Agent2 \
    --temperature 0.7 \
    --max-reflection-count 3";

        public const string ValidateDescription = @"Validate agent YAML configuration files

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
  srectl agent validate --file agents/MyAgent/MyAgent.yaml";

        public const string ApplyDescription = @"Apply an agent configuration to the remote server

Examples:
  # Apply an agent to the server
  srectl agent apply --name DevOpsAgent

  # Preview what would be applied (dry run)
  srectl agent apply --name KustoAgent --dry-run

  # Apply with debug logging
  srectl agent apply --name MyAgent --debug";

        public const string DeleteDescription = @"Delete an agent from the remote server

Examples:
  # Delete an agent from the server
  srectl agent delete --name OldAgent

  # Delete with debug logging
  srectl agent delete --name TestAgent --debug";

        public const string TestDescription = @"Test an agent with a specific message

Examples:
  # Test an agent with a simple message
  srectl agent test --name DevOpsAgent --message ""Check pod status in namespace production""

  # Test without waiting for response
  srectl agent test --name KustoAgent --message ""Query memory usage"" --no-wait

  # Test with custom user details
  srectl agent test --name MyAgent --message ""Help me"" --user-id john.doe --display-name ""John Doe""";

        public const string DiffDescription = @"Compare local and remote agent configurations

Examples:
  # Compare default using git-diff (default)
  srectl agent diff --name DevOpsAgent

  # Use VS Code diff
  srectl agent diff --name KustoAgent --tool code

  # Show inline diff
  srectl agent diff --name MyAgent --raw";

        public const string MigrateDescription = @"Migrate V1 agent format to V2

Examples:
  # Migrate a specific agent
  srectl agent migrate --name MyAgent

  # Migrate all agents
  srectl agent migrate --all

  # Preview migration changes (dry run)
  srectl agent migrate --all --dry-run

  # Migrate specific agent with dry run
  srectl agent migrate --name MyAgent --dry-run";

        public const string ListDescription = @"List remote extended agents from the server

Examples:
  # List all agents
  srectl agent list

  # List all agents with full YAML details
  srectl agent list --detail

  # Get a specific agent by name (full YAML output)
  srectl agent list --name MyAgent

  # Search for specific agents
  srectl agent list --search devops";
    }

    #endregion

    #region Tool Command Examples

    public static class Tool
    {
        public const string CreateDescription = @"Create a new tool YAML configuration file

Examples:
  # Create a KustoTool with all parameters (simple format)
  srectl tool create --name QueryMetrics --type KustoTool --connector analytics-cluster --database LogsDB --description ""Query performance metrics"" --query ""MyTable | take 10"" --parameter limit --parameter offset

  # Create a KustoTool with parameters including descriptions
  srectl tool create --name AnalyzeErrors --type KustoTool --connector logs-cluster --database LogsDB --parameter ""hours:Hours to look back"" --parameter ""severity:Error severity level""

  # Create a KustoTool with minimal options (query and parameters are optional)
  srectl tool create --name GetLogs --type KustoTool --connector logs-cluster --database LogsDB

  # Create a LinkTool with template and parameters
  srectl tool create --name ServiceDashboard --type LinkTool --description ""Link to service dashboard"" --template ""https://dashboard.example.com/{serviceId}/{region}"" --parameter serviceId --parameter region

  # Create a LinkTool with minimal options (template and parameters are optional)
  srectl tool create --name DocLink --type LinkTool --description ""Link to documentation""

  # Create a tool with custom path organization
  srectl tool create --name StorageOps --type KustoTool --path ""Storage/Operations"" --connector storage-cluster

  # View available tool types
  srectl tool show-types";

        public const string ValidateDescription = @"Validate tool YAML configuration files

Examples:
  # Validate a specific tool
  srectl tool validate --name QueryMetrics

  # Validate all tools
  srectl tool validate --all

  # Validate with debug output
  srectl tool validate --name MyTool --debug";

        public const string ApplyDescription = @"Apply a tool configuration to the remote server

Examples:
  # Apply a tool to the server
  srectl tool apply --name QueryMetrics

  # Preview what would be applied (dry run)
  srectl tool apply --name StorageOps --dry-run

  # Apply with debug logging
  srectl tool apply --name CustomTool --debug";

        public const string DeleteDescription = @"Delete a tool from the remote server

Examples:
  # Delete a tool from the server
  srectl tool delete --name OldTool

  # Preview what would be deleted (dry run)
  srectl tool delete --name TestTool --dry-run

  # Delete with debug logging
  srectl tool delete --name UnusedTool --debug";

        public const string ShowTypesDescription = @"Display available tool types and their details

Examples:
  # List all available tool types
  srectl tool show-types

  # Show detailed information for all types
  srectl tool show-types --verbose

  # Show details for a specific tool type
  srectl tool show-types --type KustoTool

  # Show specific type with verbose details
  srectl tool show-types --type AzureTool --verbose";

        public const string ShowConnectorsDescription = @"Display configured data connectors (names to use in YAML) and available connector types

Examples:
  # List all available connectors
  srectl tool show-connectors";

        public const string DiffDescription = @"Compare local and remote tool configurations

Examples:
  # Compare default using git
  srectl tool diff --name QueryMetrics

  # Use VS Code diff
  srectl tool diff --name MyTool --tool code

  # Show inline diff
  srectl tool diff --name MyTool --raw";

        public const string MigrateDescription = @"Migrate V1 tool configurations to V2 format

Examples:
  # Migrate a specific tool
  srectl tool migrate --name MyKustoTool

  # Migrate all V1 tools
  srectl tool migrate --all

  # Migrate specific tool with dry run
  srectl tool migrate --name MyKustoTool --dry-run

  # Preview migration without making changes (dry run)
  srectl tool migrate --all --dry-run";

        public const string ListDescription = @"List all tools from the remote server

Examples:
  # List all tools
  srectl tool list

  # List all tools with full YAML details
  srectl tool list --detail

  # Get a specific tool by name (full YAML output)
  srectl tool list --name TestMigrate

  # Search for specific tools
  srectl tool list --search kusto";
    }

    #endregion

    #region Skill Command Examples

    public static class Skill
    {
        public const string CreateDescription = @"Create a new skill directory with template files

Examples:
  # Create a new skill
  srectl skill create --name my-skill

  # Create to a custom path
  srectl skill create --name my-skill --output-path custom/path";

        public const string UploadDescription = @"Upload a custom skill or multiple skills from a directory

Examples:
  # Upload a single skill directory
  srectl skill upload --path skills/my-skill

  # Upload all skills from a folder
  srectl skill upload --folder skills";

        public const string ConvertDescription = @"Convert an existing agent to a skill

Examples:
  # Convert an agent to a skill
  srectl skill convert --agent-name my-agent

  # Convert with specific top-level agents for context
  srectl skill convert --agent-name my-agent --top-level-agents triage-agent support-agent

  # Specify custom output path
  srectl skill convert --agent-name my-agent --output-path custom/path";

        public const string ListDescription = @"List all available skills

Examples:
  # List all skills
  srectl skill list

  # List with pagination
  srectl skill list --page 2 --limit 25

  # Search for specific skills
  srectl skill list --search database";

        public const string DownloadDescription = @"Download a skill from the server

Examples:
  # Download a skill
  srectl skill download --name my-skill

  # Download to a specific path
  srectl skill download --name my-skill --output-path custom/path";

        public const string DeleteDescription = @"Delete a skill from the server

Examples:
  # Delete a skill
  srectl skill delete --name my-skill";
    }

    #endregion

    #region General Command Examples

    public static class General
    {
        public const string InitDescription = @"Initialize SREAgent CLI configuration and workspace

Examples:
  # Initialize with local development server
  srectl init --resource-url https://localhost:7023

  # Initialize with remote server
  srectl init --resource-url https://my-sreagent-dev.1abcdef.eastus2.azuresre.ai

  # Initialize with production environment
  srectl init --resource-url https://my-sreagent-prod.2abcdef.eastus2.azuresre.ai";

        public const string ListDescription = @"List various resources from the remote server

Examples:
  # List all agents on the server
  srectl list agents

  # List all tools on the server
  srectl list tools

  # List extended tools (user-added)
  srectl list extended-tools

  # List data connectors
  srectl list data-connectors";

        public const string ApplyYamlDescription = @"Apply any YAML configuration file to the server

Examples:
  # Apply an agent YAML file
  srectl apply-yaml --file agents/MyAgent/MyAgent.yaml

  # Apply a tool YAML file
  srectl apply-yaml --file tools/CustomTool/CustomTool.yaml

  # Apply any configuration file
  srectl apply-yaml --file configs/my-config.yaml";

        public const string ChatDescription = @"Start an interactive chat session with the SRE Agent

Examples:
  # Start interactive chat
  srectl chat

  # Start chat with debug logging
  srectl chat --debug

  # Start chat with minimal output
  srectl chat --quiet";

        public const string SyncDescription = @"Sync agents and tools YAML from the remote server into the local workspace (agents/, tools/)

Examples:
  # Sync all remote configurations
  srectl sync

Note: Requires prior 'srectl init --resource-url <url>'";
    }

    #endregion

    #region Thread Command Examples

    public static class Thread
    {
        public const string NewDescription = @"Create a new conversation thread with the SRE Agent

Examples:
  # Send a message and wait for response
  srectl thread new --message ""Check the status of pods in production namespace""

  # Send a message without waiting for response
  srectl thread new --message ""Investigate high memory usage"" --no-wait

  # Send with custom user information
  srectl thread new --message ""Help with deployment"" --user-id admin --display-name ""Administrator""";

        public const string ContinueDescription = @"Continue an existing conversation thread

Examples:
  # Continue the last thread
  srectl thread continue --message ""Can you also check the logs?""

  # Continue a specific thread
  srectl thread continue --thread-id abc123 --message ""What about the network?""

  # Continue without sending a new message (just get latest responses)
  srectl thread continue --thread-id abc123";

        public const string DeleteDescription = @"Delete a conversation thread

Examples:
  # Delete a specific thread
  srectl thread delete --thread-id abc123

  # Delete with confirmation
  srectl thread delete --thread-id abc123 --debug";
    }

    #endregion

    #region Document Command Examples

    public static class Document
    {
        public const string UploadDescription = @"Upload documents to the server for knowledge base

Examples:
  # Upload a single document
  srectl doc upload --file ./docs/runbook.md

  # Upload multiple documents
  srectl doc upload --file ./docs/guide1.pdf --file ./docs/guide2.md

  # Upload with specific category
  srectl doc upload --file ./runbook.md --category troubleshooting";

        public const string SearchDescription = @"Search documents in the knowledge base

Examples:
  # Search for documents containing specific terms
  srectl doc search --query ""kubernetes troubleshooting""

  # Search with multiple terms
  srectl doc search --query ""memory leak debugging""

  # Search with filters
  srectl doc search --query ""deployment"" --category runbooks";

        public const string ReindexDescription = @"Reindex all documents in the knowledge base

Examples:
  # Reindex all documents
  srectl doc reindex

  # Reindex with debug logging
  srectl doc reindex --debug";
    }

    #endregion

    #region Profile Command Examples

    public static class Profile
    {
        public const string ListDescription = @"List all available profiles and show the active one

Examples:
  # List all profiles
  srectl profile list

  # List with detailed information
  srectl profile list --verbose";

        public const string GetDescription = @"Get details of a specific profile or the current active profile

Examples:
  # Get current active profile
  srectl profile get

  # Get specific profile details
  srectl profile get --name production

  # Get profile with debug info
  srectl profile get --name local --debug";

        public const string CreateDescription = @"Create a new connection profile

Examples:
  # Create a local development profile
  srectl profile create --name local --url https://localhost:7023

  # Create a production profile
  srectl profile create --name production --url https://prod-sreagent.company.com

  # Create a profile with authentication
  srectl profile create --name staging --url https://staging.company.com --set-current";

        public const string SetDescription = @"Set the active profile

Examples:
  # Switch to local development
  srectl profile set --name local

  # Switch to production
  srectl profile set --name production

  # Switch with confirmation
  srectl profile set --name staging --debug";

        public const string DeleteDescription = @"Delete a profile

Examples:
  # Delete an unused profile
  srectl profile delete --name old-environment

  # Delete with confirmation
  srectl profile delete --name test --debug";
    }

    #endregion

    #region Scheduled Task Command Examples

    public static class ScheduledTask
    {
        public const string CreateDescription = @"Create a new scheduled task for automated agent operations

Examples:
  # Create a daily task
  srectl scheduledtask create --name ""Daily Health Check"" --cron ""0 9 * * *"" --prompt ""Check system health""

  # Create a weekly task with limited executions
  srectl scheduledtask create --name ""Weekly Report"" --cron ""0 9 * * 1"" --prompt ""Generate weekly report"" --max-executions 4

  # Create a task with specific agent
  srectl scheduledtask create --name ""Agent Task"" --cron ""0 10 * * *"" --prompt ""Run daily checks"" --agent ""ProductionAgent""";

        public const string ListDescription = @"List all scheduled tasks from the remote server

Examples:
  # List all scheduled tasks
  srectl scheduledtask list

  # List with detailed information
  srectl scheduledtask list --verbose

  # List with status filter
  srectl scheduledtask list --status Active";

        public const string GetDescription = @"Get detailed information about a specific scheduled task

Examples:
  # Get details of a task
  srectl scheduledtask get --id task-123

  # Get details by name
  srectl scheduledtask get --id daily-health-check";

        public const string PauseDescription = @"Pause a scheduled task to stop its execution

Examples:
  # Pause a task
  srectl scheduledtask pause --id task-123

  # Pause by name
  srectl scheduledtask pause --id daily-health-check";

        public const string ResumeDescription = @"Resume a paused scheduled task

Examples:
  # Resume a task
  srectl scheduledtask resume --id task-123

  # Resume by name
  srectl scheduledtask resume --id daily-health-check";

        public const string DeleteDescription = @"Permanently delete a scheduled task

Examples:
  # Delete a task
  srectl scheduledtask delete --id task-123

  # Delete an old task
  srectl scheduledtask delete --id old-maintenance-task";
    }

    #endregion

    #region Incident Handler Command Examples

    public static class IncidentHandler
    {
        public const string CreateDescription = @"Create a new incident filter with specified criteria

Examples:
  # Create a simple filter
  srectl incidenthandler create --id StorageFilter --name ""Storage Issues"" --title-contains ""storage""

  # Create a filter with priority and handling agent
  srectl incidenthandler create --id ProdFilter --priority 1 --incident-type LiveSite --handling-agent ProdAgent

  # Create a filter with impacted service
  srectl incidenthandler create --id APIFilter --impacted-service ""Web API"" --max-attempts 5";

        public const string MapAgentDescription = @"Map a YAML agent to an incident filter

Examples:
  # Map an agent to a filter
  srectl incidenthandler map-agent --name ProductionFilter --handling-agent ProductionAgent

  # Map a specialized agent
  srectl incidenthandler map-agent --name StorageIssues --handling-agent StorageAgent";

        public const string ListDescription = @"List all incident handlers from the remote server

Examples:
  # List all incident handlers
  srectl incidenthandler list

  # List with detailed information
  srectl incidenthandler list --verbose";
    }

    #endregion

    #region Extension Command Examples

    public static class Extension
    {
        public const string GenerateEv2Description = @"Generate EV2 deployment files by copying templates and processing agent/tool configurations

Examples:
  # Generate Bicep and ARM templates only
  srectl extension generate-ev2 --tools-folder ./tools --agent-folder ./agents --output ./ev2-output

  # Generate with full EV2 deployment artifacts
  srectl extension generate-ev2 --tools-folder ./tools --agent-folder ./agents --output ./deployment --service-identifier ""00000000-0000-0000-0000-000000000000"" --service-group ""MyServiceGroup"" --environment ""Test"" --tenant-id ""72f988bf-86f1-41af-91ab-2d7cd011db47"" --subscription-key ""Production"" --subscription-id ""00000000-0000-0000-0000-000000000000"" --resource-group ""my-resource-group""";
    }

    #endregion

    #region Real-World Examples

    /// <summary>
    /// Real-world examples and complete workflows demonstrating practical usage
    /// </summary>
    public static class RealWorld
    {
        public const string KubernetesSREAgent = @"
Real-World Example: Kubernetes SRE Agent

This example shows how to create a comprehensive SRE agent for Kubernetes troubleshooting:

1. Create Kubernetes monitoring tools:
   srectl tool create --name QueryPodMetrics --type KustoTool \
     --extra database:KubernetesLogs cluster:production-cluster

   srectl tool create --name CheckNodeHealth --type KustoTool \
     --extra database:InfrastructureLogs cluster:monitoring-cluster

   srectl tool create --name GetNamespaceResources --type AzureTool \
     --path ""Kubernetes/Resources""

2. Validate and apply tools:
   srectl tool validate --all
   srectl tool apply --name QueryPodMetrics
   srectl tool apply --name CheckNodeHealth
   srectl tool apply --name GetNamespaceResources

3. Create the SRE agent:
   srectl agent create --name KubernetesSREAgent \
     --instructions ""Specialized agent for Kubernetes cluster troubleshooting and monitoring.
                     Analyze pod failures, resource constraints, and cluster health issues."" \
     --tools QueryPodMetrics CheckNodeHealth GetNamespaceResources \
     --handoffs InfrastructureAgent NetworkAgent \
     --max-reflection-count 3 \
     --temperature 0.3

4. Apply and test the agent:
   srectl agent apply --name KubernetesSREAgent
   srectl agent test --name KubernetesSREAgent \
     --message ""Production pods are failing in the payment-service namespace""";

        public const string IncidentResponseAgent = @"
Real-World Example: Incident Response Agent

Complete workflow for setting up an incident response automation agent:

1. Create incident management tools:
   srectl tool create --name QueryServiceMetrics --type KustoTool \
     --extra database:ServiceTelemetry cluster:prod-telemetry

   srectl tool create --name CheckServiceHealth --type AzureTool \
     --path ""HealthChecks/Services""

   srectl tool create --name GetRecentDeployments --type KustoTool \
     --extra database:DeploymentLogs cluster:cicd-cluster

2. Create the incident response agent:
   srectl agent create --name IncidentResponseAgent --smart \
     --instructions ""First-line incident response agent that triages service outages,
                     gathers initial diagnostics, and escalates to appropriate teams.""

3. Full deployment workflow:
   srectl agent validate --name IncidentResponseAgent --check-tools
   srectl agent apply --name IncidentResponseAgent --dry-run
   srectl agent apply --name IncidentResponseAgent

4. Test incident scenarios:
   srectl agent test --name IncidentResponseAgent \
     --message ""Service degradation detected in payment API - 500 errors increasing""";

        public const string DatabaseSREAgent = @"
Real-World Example: Database SRE Agent

Setting up a database performance and reliability agent:

1. Create database monitoring tools:
   srectl tool create --name QueryDatabasePerformance --type KustoTool \
     --extra database:DatabaseMetrics cluster:db-monitoring \
     --extra query:""DatabasePerformanceMetrics | where TimeGenerated > ago(1h)""

   srectl tool create --name CheckConnectionPools --type KustoTool \
     --extra database:ApplicationLogs cluster:app-telemetry

2. Create the specialized agent:
   srectl agent create --name DatabaseSREAgent \
     --instructions ""Database reliability engineer agent specializing in performance analysis,
                     connection issues, and query optimization recommendations."" \
     --tools QueryDatabasePerformance CheckConnectionPools \
     --handoffs SecurityAgent InfrastructureAgent \
     --temperature 0.2 \
     --max-reflection-count 2

3. Upload relevant documentation:
   srectl doc upload --file ./runbooks/database-troubleshooting.md
   srectl doc upload --file ./procedures/db-performance-analysis.md

4. Deploy and validate:
   srectl agent apply --name DatabaseSREAgent
   srectl agent test --name DatabaseSREAgent \
     --message ""Database queries are slow and connection timeouts are increasing""";
    }

    #endregion

    #region Usage Patterns

    /// <summary>
    /// Common usage patterns and workflows
    /// </summary>
    public static class Workflows
    {
        public const string QuickStart = @"
Quick Start Workflow:
1. srectl init --resource-url https://localhost:7023
2. srectl agent create --name MyFirstAgent --smart
3. srectl agent apply --name MyFirstAgent
4. srectl agent test --name MyFirstAgent --message ""Hello""";

        public const string DevelopmentWorkflow = @"
Development Workflow:
1. srectl tool create --name MyTool --type KustoTool
2. srectl tool validate --name MyTool
3. srectl tool apply --name MyTool --dry-run
4. srectl tool apply --name MyTool
5. srectl agent create --name TestAgent --tools MyTool
6. srectl agent test --name TestAgent --message ""Test message""";

        public const string DeploymentWorkflow = @"
Deployment Workflow:
1. srectl agent validate --all --check-tools
2. srectl tool validate --all
3. srectl agent apply --name ProductionAgent --dry-run
4. srectl agent apply --name ProductionAgent
5. srectl list agents  # Verify deployment";

        public const string TeamCollaborationWorkflow = @"
Team Collaboration Workflow:
1. Setup profiles for different environments:
   srectl profile create --name local --resource-url https://localhost:7023
   srectl profile create --name staging --resource-url https://staging-sreagent.company.com
   srectl profile create --name production --resource-url https://prod-sreagent.company.com

2. Develop locally:
   srectl profile set --name local
   srectl agent create --name NewFeatureAgent --smart
   srectl agent test --name NewFeatureAgent --message ""Test new feature""

3. Deploy to staging:
   srectl profile set --name staging
   srectl agent apply --name NewFeatureAgent --dry-run
   srectl agent apply --name NewFeatureAgent

4. Deploy to production:
   srectl profile set --name production
   srectl agent validate --name NewFeatureAgent --check-tools
   srectl agent apply --name NewFeatureAgent";

        public const string TroubleshootingWorkflow = @"
Troubleshooting Workflow:
1. Enable debug logging:
   srectl agent validate --name MyAgent --debug

2. Test with detailed output:
   srectl agent test --name MyAgent --message ""Debug this issue"" --debug

3. Check server connectivity:
   srectl list agents --debug

4. Validate tool dependencies:
   srectl agent validate --all --check-tools

5. Use interactive chat for investigation:
   srectl chat --debug";

        public const string MaintenanceWorkflow = @"
Maintenance Workflow:
1. List current deployments:
   srectl list agents
   srectl list tools
   srectl list extended-tools

2. Clean up unused resources:
   srectl agent delete --name OldAgent
   srectl tool delete --name UnusedTool --dry-run
   srectl tool delete --name UnusedTool

3. Update documentation:
   srectl doc upload --file ./updated-runbook.md
   srectl doc reindex

4. Validate all configurations:
   srectl agent validate --all --check-tools
   srectl tool validate --all";
    }

    #endregion
}
