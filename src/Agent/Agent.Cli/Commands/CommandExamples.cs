using System.Text;

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
  srectl agent create --name DevOpsAgent --instructions ""Help with DevOps tasks""

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
  # Validate a specific agent file
  srectl agent validate --file agents/MyAgent/MyAgent.yaml

  # Validate all agent files
  srectl agent validate --all

  # Validate with tool availability checking
  srectl agent validate --all --check-tools

  # Validate specific agent and check tools exist remotely
  srectl agent validate --file agents/KustoAgent/KustoAgent.yaml --check-tools";

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
    }

    #endregion

    #region Tool Command Examples

    public static class Tool
    {
        public const string CreateDescription = @"Create a new tool YAML configuration file

Examples:
  # Create a basic Kusto tool
  srectl tool create --name QueryMetrics --type KustoTool

  # Create a tool with custom path organization
  srectl tool create --name StorageOps --type AzureTool --path ""Storage/Operations""

  # Create a tool with extra parameters
  srectl tool create --name CustomTool --type KustoTool --extra database:LogsDB cluster:prod-cluster

  # View available tool types first
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

        public const string ShowConnectorsDescription = @"Display available connector types

Examples:
  # List all available connectors
  srectl tool show-connectors

  # Show detailed connector information
  srectl tool show-connectors --verbose";

  public const string DiffDescription = @"Compare local and remote tool configurations

Examples:
  # Compare default using git
  srectl tool diff --name QueryMetrics

  # Use VS Code diff
  srectl tool diff --name MyTool --tool code

  # Show inline diff
  srectl tool diff --name MyTool --raw";
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
