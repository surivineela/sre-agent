using System.Reflection;

namespace Agent.Cli.Helpers;

/// <summary>
/// Manages the creation and copying of example files for the CLI.
/// </summary>
public static class ExampleFileManager
{
    /// <summary>
    /// Copies example files from the templates directory, falling back to inline examples if templates don't exist.
    /// </summary>
    public static async Task CopyExampleFilesAsync()
    {
        try
        {
            // Get the directory where the CLI is running from
            var executableDir = AppContext.BaseDirectory;
            var templatesDir = Path.Combine(executableDir, "templates");

            // If templates directory doesn't exist, create the examples inline
            if (!Directory.Exists(templatesDir))
            {
                await CreateInlineAgentExamplesAsync();
                await CreateGenericPluginConfigurationExampleAsync();
                await CreateGenericConnectorListExampleAsync();
                await CreateGenericToolListExampleAsync();
                return;
            }

            // Copy example agent
            var exampleAgentSource = Path.Combine(templatesDir, "example_agent.yaml");
            var exampleAgentDest = Path.Combine("agents", "example_agent.yaml");
            if (File.Exists(exampleAgentSource))
            {
                await File.WriteAllTextAsync(exampleAgentDest, await File.ReadAllTextAsync(exampleAgentSource));
            }

            // Copy example tool
            var exampleToolSource = Path.Combine(templatesDir, "example_tool.yaml");
            var exampleToolDest = Path.Combine("tools", "example_tool.yaml");
            if (File.Exists(exampleToolSource))
            {
                await File.WriteAllTextAsync(exampleToolDest, await File.ReadAllTextAsync(exampleToolSource));
            }

            // Copy example connector
            var exampleConnectorSource = Path.Combine(templatesDir, "example_connector.yaml");
            var exampleConnectorDest = Path.Combine("connectors", "example_connector.yaml");
            if (File.Exists(exampleConnectorSource))
            {
                await File.WriteAllTextAsync(exampleConnectorDest, await File.ReadAllTextAsync(exampleConnectorSource));
            }

            // Copy example connector
            var examplePluginconfigSource = Path.Combine(templatesDir, "example_pluginconfig.yaml");
            var examplePluginconfigDest = Path.Combine("connectors", "example_pluginconfig.yaml");
            if (File.Exists(examplePluginconfigSource))
            {
                await File.WriteAllTextAsync(examplePluginconfigDest, await File.ReadAllTextAsync(examplePluginconfigSource));
            }
        }
        catch
        {
            // Fallback to inline examples
            await CreateInlineExamplesAsync();
        }
    }

    private static async Task CreateGenericPluginConfigurationExampleAsync()
    {
        var yaml = @"api_version: agent.platform.ai/v1
kind: PluginConfiguration
metadata:
  owner: sre-agent-team@example.com
  version: ""1.0.0""
  tags:
    - example
    - plugin
    - integration
  updated_at: 2025-07-30
  created_at: 2025-07-30

spec:
  plugin_name: ExampleIncidentPlugin
  config:
    Type: PagerDuty
    ConnectionName: ""ExamplePDConnection""
    ConnectionUrl: ""https://api.pagerduty.com""
    OboUser: ""oncall@example.com""
    ICMAPI:
      APIEndpoint: ""https://icm.example.com/api/v1/incidents""
      OwningServiceId: ""demo-service-id-001""
";

        var outputPath = Path.Combine("plugins", "plugin_config_example.yaml");
        Directory.CreateDirectory("plugins");
        await File.WriteAllTextAsync(outputPath, yaml);
    }

    private static async Task CreateGenericConnectorListExampleAsync()
    {
        var yaml = @"api_version: agent.platform.ai/v1
kind: ConnectorList
metadata:
  owner: sre-agent-team@example.com
  version: ""1.0.0""
  tags:
    - example
    - telemetry
    - demo
  updated_at: 2025-07-30
  created_at: 2025-07-30

spec:
  connectors:
    - name: example-kusto-connector
      enabled: true
      type: Kusto
      description: Example connector for accessing Kusto clusters in multiple regions.
      auth:
        authentication_type: UAMI
        authority: """"
        authority_host: """"
        application_client_id: """"
        application_certificate: """"
        managed_identity_client_id: ""00000000-0000-0000-0000-000000000000""
        managed_identity_resource_id: """"
      metadata:
        owner: sre-agent-team@example.com
        version: ""1.0.0""
        tags: [ ""example"", ""telemetry"", ""multi-region"" ]
        updated_at: 2025-07-30
        created_at: 2025-07-30
      cluster_url: ""https://example.westeurope.kusto.windows.net""
      database: ""exampledb""
      cluster_hint: """"
      regional_cluster_groups:
        - name: ExampleGroup
          regions:
            - region: westeurope
              database: exampledb
              cluster_uri: https://example.westeurope.kusto.windows.net

    - name: example-Kusto-connector
      enabled: true
      type: Kusto
      description: Sample Kusto connector for federated access to compliance data.
      auth:
        authentication_type: UAMI
        authority: """"
        authority_host: """"
        application_client_id: """"
        application_certificate: """"
        managed_identity_client_id: ""11111111-1111-1111-1111-111111111111""
        managed_identity_resource_id: """"
      metadata:
        owner: sre-agent-team@example.com
        version: ""1.0.0""
        tags: [ ""Kusto"", ""example"", ""compliance"" ]
        updated_at: 2025-07-30
        created_at: 2025-07-30
      cluster_url: ""https://example-Kusto.eastus.database.windows.net""
      database: ""compliance_logs""
      cluster_hint: """"
      regional_cluster_groups:
        - name: ComplianceGroup
          regions:
            - region: eastus
              database: compliance_logs
              cluster_uri: https://example-Kusto.eastus.database.windows.net
";

        var outputPath = Path.Combine("connectors", "connector_list_example.yaml");
        Directory.CreateDirectory("connectors");
        await File.WriteAllTextAsync(outputPath, yaml);
    }

    private static async Task CreateInlineAgentExamplesAsync()
    {
        var yaml = @"api_version: agent.platform.ai/v1
kind: AgentConfiguration
metadata:
  owner: your-team@example.com
  version: ""1.0.0""
  tags:
    - example
    - demo
    - generic
  updated_at: 2025-07-30
  created_at: 2025-07-30

spec:
  agent:
    name: ""example_agent""
    system_prompt: |
      # Example Agent
      You are an AI-powered SRE agent capable of investigating operational issues.
      This agent is intended to demonstrate how to structure an agent configuration file.
      Always act helpfully, clearly, and professionally.
    handoff_description: ""Use this agent for demonstration or onboarding purposes.""
    tools:
      - example_tool
    temperature: 0.5
    max_reflection_count: 2
    custom_reflection_note: |
      Reflect before executing any action:
      1. Do I already have this info in chat history?
      2. Is this command appropriate?
      3. Is syntax valid? Should I confirm parameters or check help?
      4. Have I fully diagnosed the situation before taking action?
      5. Is user informed of changes? Approval needed?
      6. Are potential risks assessed?
      7. If a command fails, is there a fallback strategy?

  tools:
    - name: ""example_tool""
      type: KustoTool
      connector: ""example_connector""
      description: ""An example tool that queries a telemetry database.""
      database: ""example_db""
      mode: Query
      query: |
        let from = datetime(""##fromDate##"");
        let to = datetime(""##toDate##"");
        example_table
        | where Timestamp between (from .. to)
        | take 100
      parameters:
        - name: ""fromDate""
          type: string
          required: true
          description: ""Start of time range""
          map_to: args
          target: dictionary:args:string
        - name: ""toDate""
          type: string
          required: true
          description: ""End of time range""
          map_to: args
          target: dictionary:args:string
        - name: ""region""
          type: string
          description: ""Region identifier""
          map_to: region
          target: direct
      attributes:
        - ExampleOnly
      metadata:
        owner: your-team@example.com
        version: ""1.0.0""
        tags: [ ""example"", ""tool"" ]
        created_at: 2025-07-30
        updated_at: 2025-07-30

  connectors:
    - name: example_connector
      enabled: true
      type: Kusto
      description: Connector used for demo purposes.
      auth:
        authentication_type: UAMI
        authority: """"
        authority_host: """"
        application_client_id: """"
        application_certificate: """"
        managed_identity_client_id: ""demo-client-id-guid""
        managed_identity_resource_id: """"
      metadata:
        owner: your-team@example.com
        version: ""1.0.0""
        tags: [ ""example"" ]
        updated_at: 2025-07-30
        created_at: 2025-07-30
      cluster_url: ""https://example.kusto.windows.net""
      database: ""example_db""
      cluster_hint: """"
      regional_cluster_groups:
        - name: ExampleGroup
          regions:
            - region: eastus
              database: example_db
              cluster_uri: https://example.kusto.windows.net
";

        var outputPath = Path.Combine("agents", "example_agent.yaml");
        Directory.CreateDirectory("agents");
        await File.WriteAllTextAsync(outputPath, yaml);
    }

    private static async Task CreateGenericToolListExampleAsync()
    {
        var yaml = @"api_version: agent.platform.ai/v1
kind: ToolList
metadata:
  owner: sre-agent-team@example.com
  version: ""1.0.0""
  tags:
    - example
    - demo
    - template
  updated_at: 2025-07-30
  created_at: 2025-07-30

spec:
  tools:
    - name: ""ExampleTool""
      type: KustoTool
      connector: ""example-kusto-connector""
      description: ""A sample tool used to demonstrate how to define a Kusto-based tool.""
      database: ""example_db""
      mode: Query
      query: |
        let from = datetime(""##fromDate##"");
        let to = datetime(""##toDate##"");
        ExampleLogs
        | where Timestamp between (from .. to)
        | take 100
      parameters:
        - name: ""fromDate""
          type: string
          required: true
          description: ""Start time for the log filter""
          map_to: args
          target: dictionary:args:string
        - name: ""toDate""
          type: string
          required: true
          description: ""End time for the log filter""
          map_to: args
          target: dictionary:args:string
        - name: ""region""
          type: string
          required: false
          description: ""Optional region for context""
          map_to: region
          target: direct
      attributes:
        - ExampleOnly
      metadata:
        owner: sre-agent-team@example.com
        version: ""1.0""
        tags: [ ""example"", ""demo"" ]
        created_at: 2025-07-30
        updated_at: 2025-07-30
";

        var outputPath = Path.Combine("tools", "tool_list_example.yaml");
        Directory.CreateDirectory("tools");
        await File.WriteAllTextAsync(outputPath, yaml);
    }

    /// <summary>
    /// Creates inline example files when templates are not available.
    /// </summary>
    private static async Task CreateInlineExamplesAsync()
    {
        // Create example agent
        var exampleAgent = @"agent:
  name: example_agent

  system_prompt: |
    You are an example SRE agent designed to demonstrate the capabilities of the SREAgent system.
    You can help with basic incident management and provide guidance on SRE best practices.
    Always be helpful, professional, and focused on solving operational problems.

  handoff_description: Use this agent for general SRE tasks and as an example of agent configuration.

  handoffs:
    - meta_agent

  tools:
    - ExampleTool

  common_prompts:
    - format_guidelines
    - guard_rail

  max_reflection_count: 0
  allow_parallel_tool_calls: false";
        await File.WriteAllTextAsync(Path.Combine("agents", "example_agent.yaml"), exampleAgent);

        // Create example tool
        var exampleTool = @"tool:
  name: example_tool
  type: KustoQuery
  description: An example tool that demonstrates how to create Kusto-based tools for SRE agents
  version: ""1.0.0""
  parameters:
    - name: query
      type: string
      description: The Kusto query to execute
      required: true
    - name: database
      type: string
      description: The target database name
      required: false
      default: ""DefaultDatabase""";
        await File.WriteAllTextAsync(Path.Combine("tools", "example_tool.yaml"), exampleTool);

        // Create example connector
        var exampleConnector = @"connector:
  name: example_connector
  type: HttpConnector
  description: An example HTTP connector for integrating with external services
  endpoint: ""https://api.example.com""
  authentication:
    type: bearer
    token_source: environment
    token_key: ""EXAMPLE_API_TOKEN""
  timeout: 30
  retry_policy:
    max_attempts: 3
    backoff_type: exponential";
        await File.WriteAllTextAsync(Path.Combine("connectors", "example_connector.yaml"), exampleConnector);
    }
}
