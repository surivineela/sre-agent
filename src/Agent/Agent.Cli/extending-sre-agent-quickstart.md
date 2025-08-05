# Extending the SRE Agent - Quickstart Guide

This guide will walk you through extending the SRE Agent system by setting up cross-tenant access and creating a simple "Hello World" agent that uses a Kusto query tool.

## Table of Contents
1. [Installing SRECTL](#installing-srectl)
2. [Cross-Tenant Access Setup](#cross-tenant-access-setup)
3. [Creating a Hello World Agent with Kusto Tool](#creating-a-hello-world-agent-with-kusto-tool)
4. [Testing Your Agent](#testing-your-agent)
5. [Next Steps](#next-steps)

---

## Installing SRECTL

Before you can start extending SRE Agents, you need to install the SRECTL command-line tool.

There are two ways to install SRECTL from Azure Artifacts:

### Option 1: Pre-built Executables (Recommended)

Install platform-specific executables from Azure Artifacts Universal Packages. This option requires no .NET SDK installation.

**Use the automated installer script:**

```powershell
# Download and install the latest version (default behavior)
.\scripts\install_exe.ps1

# Upgrade existing installation to latest version
.\scripts\install_exe.ps1 -Upgrade

# Install or upgrade to a specific version
.\scripts\install_exe.ps1 -PackageVersion "1.0.3"
.\scripts\install_exe.ps1 -PackageVersion "1.0.3" -Upgrade

# For system-wide installation (requires admin)
.\scripts\install_exe.ps1 -SystemInstall

# For silent installation
.\scripts\install_exe.ps1 -Silent

# Combined options
.\scripts\install_exe.ps1 -Upgrade -SystemInstall -Silent
```

**Available platforms:**
- Windows x64 (self-contained .exe)
- Linux x64 (self-contained binary)
- macOS Intel x64 (self-contained binary)
- macOS Apple Silicon ARM64 (self-contained binary)

The installer will:
- ✅ Download the correct executable for your platform
- ✅ Install to an appropriate directory
- ✅ Configure PATH environment variable
- ✅ Create shortcuts and aliases
- ✅ Provide an uninstaller

**Version Handling:**
- By default, the script installs the **latest known stable version**
- Use `-PackageVersion "latest"` to explicitly request the latest known stable version
- Azure Artifacts Universal Packages don't support automatic latest version resolution, so the script uses a known stable version
- If you need a specific version, use `-PackageVersion "x.y.z"`
- Use `-Upgrade` to upgrade existing installations to the latest known stable or specified version

Check the [feed and version history here](https://dev.azure.com/msazure/One/_artifacts/feed/SREAgentCli/UPack/srectl-executables/overview).

### Option 2: .NET Tool Package

Install SRECTL as a .NET global tool from Azure Artifacts NuGet feed. This option requires .NET 9.0 SDK.

**Use the automated installer script:**

```powershell
# Download and install the latest version (default behavior)
.\scripts\install-nupkg.ps1

# Upgrade existing installation to latest version
.\scripts\install-nupkg.ps1 -Upgrade

# Install or upgrade to a specific version
.\scripts\install-nupkg.ps1 -Version "1.0.3"
.\scripts\install-nupkg.ps1 -Version "1.0.3" -Upgrade

# Or with custom feed parameters
.\scripts\install-nupkg.ps1 -FeedUrl "https://pkgs.dev.azure.com/myorg/myproject/_packaging/myfeed/nuget/v3/index.json"
```

**Manual installation (if you have .NET 9.0 SDK):**

```powershell
# Remove any existing installation
dotnet tool uninstall sreagent.cli --global

# Install from Azure Artifacts feed
dotnet tool install sreagent.cli --global --add-source https://pkgs.dev.azure.com/msazure/One/_packaging/SREAgentCli/nuget/v3/index.json
```

**Version Handling:**
- By default, the script installs the **latest available version** from the NuGet feed
- Use `-Version "latest"` to explicitly request the latest version
- For .NET tools, "latest" means the script omits the `--version` parameter, letting `dotnet tool install` automatically choose the newest version
- Use `-Upgrade` to upgrade existing installations to the latest or specified version
- The script automatically uninstalls any existing version before installing the new one

Check the [feed and version history here](https://dev.azure.com/msazure/One/_artifacts/feed/SREAgentCli/NuGet/SREAgent.CLI/overview).

### Verification

After installation with either method, verify SRECTL is working:

```powershell
srectl --version
srectl --help
```

### Which Option Should I Choose?

- **Choose Option 1 (Executables)** if you:
  - Don't have .NET SDK installed
  - Want a simple, dependency-free installation
  - Prefer platform-specific optimized binaries
  - Need offline installation capability

- **Choose Option 2 (.NET Tool)** if you:
  - Already have .NET 9.0 SDK installed
  - Want automatic updates through .NET tooling
  - Prefer smaller download sizes
  - Are familiar with .NET global tools

---

## Cross-Tenant Access Setup

When working with SRE Agents deployed in different Azure tenants (such as AME/PME environments), you need to configure cross-tenant access permissions.

### Prerequisites
- Azure CLI installed and configured
- Access to the target SRE Agent resource
- Your corporate user account object ID

### Step 1: Find Your User Object ID

1. Navigate to the [Entra ID Overview Page](https://ms.portal.azure.com/#view/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/~/Overview)
2. Copy your **Object ID** from the overview page

### Step 2: Configure Cross-Tenant Access

Use the Azure CLI to add your user as an admin to the SRE Agent resource:

```bash
az resource patch --ids <YOUR_SRE_AGENT_ARM_RESOURCE_ID> \
  -p '{"adminUsers": [{"objectId":"YOUR_CORP_USER_OBJECT_ID","tenantId":"72f988bf-86f1-41af-91ab-2d7cd011db47"}]}'
```

**Example:**
```bash
az resource patch --ids /subscriptions/ab32b825-51f2-41b0-8d25-85f7a0071a6f/resourceGroups/sre-icm-3p-rg/providers/Microsoft.App/agents/jefftest-sweden \
  -p '{"adminUsers": [{"objectId":"ce2b0d61-1323-7db2-a7f3-12bcd48f2ebc","tenantId":"72f988bf-86f1-41af-91ab-2d7cd011db47"}]}'
```

### Step 3: Authenticate with Azure CLI

Before using SRECTL with cross-tenant resources:

```bash
az login
```

Ensure you're logged in with the account that has been granted access to the SRE Agent resource.

---

## Creating a Hello World Agent with Kusto Tool

This section demonstrates how to create a simple agent that uses a Kusto query tool to fetch system health information.

### Step 1: Initialize SRECTL

First, set up your development environment:

```bash
# For local development
srectl init --resource-url https://localhost:7023

# For remote server (replace with your actual endpoint)
srectl init --resource-url https://your-sreagent-endpoint.azuresre.ai
```

### Step 2: Create a Kusto Tool

Create a simple Kusto tool that queries system health logs:

```bash
srectl tool create --name GetSystemHealthLogs --type KustoTool
```

This creates a tool template at `tools/GetSystemHealthLogs/GetSystemHealthLogs.yaml`. Let's customize it with a simple query:

**Edit the generated file to include:**

```yaml
name: GetSystemHealthLogs
type: KustoTool
connector: default-kusto-connector
description: A simple Kusto query tool that fetches recent system health logs for demonstration purposes
mode: query
function: GetSystemHealthLogs
query: |
  // Simple query to get recent system events
  union isfuzzy=true
    (Heartbeat | where TimeGenerated > ago(1h) | take 10),
    (Event | where TimeGenerated > ago(1h) and EventLevelName == "Information" | take 10),
    (Syslog | where TimeGenerated > ago(1h) | take 10)
  | project TimeGenerated, Computer, Category = Type, Message = coalesce(Category, EventData, SyslogMessage)
  | order by TimeGenerated desc
  | take 20
file: Queries/GetSystemHealthLogs.kql
database: DefaultDB
clusterHint: default-cluster
parameters:
  - name: timeRange
    type: string
    required: false
    description: Time range for the query (default: 1h)
    mapTo: args
    target: dictionary:args:string
    defaultValue: "1h"
attributes: []
metadata:
  owner: sre-team
  version: 1.0
  tags: [demo, system-health, kusto]
  lastUpdated: 2025-08-01
```

### Step 3: Create the Hello World Agent

Now create an agent that uses this tool:

```bash
srectl agent create --name HelloWorldAgent \
  --instructions "I am a Hello World SRE Agent. I help demonstrate basic system health monitoring using Kusto queries. When asked about system health, I will fetch recent logs and provide a summary." \
  --tools GetSystemHealthLogs \
  --handoff-description "Use this agent for basic system health demonstrations and Kusto query examples"
```

Alternatively, use the smart generation feature for more comprehensive instructions:

```bash
srectl agent create --name HelloWorldAgent --smart \
  --instructions "Focus on basic system health monitoring and demonstrate Kusto query capabilities"
```

### Step 4: Validate Your Configurations

Validate both the tool and agent:

```bash
# Validate the tool
srectl tool validate --name GetSystemHealthLogs

# Validate the agent
srectl agent validate --name HelloWorldAgent

# Or validate everything at once
srectl agent validate --all
srectl tool validate --all
```

### Step 5: Apply to Remote Server

Deploy your tool and agent to the SRE Agent server:

```bash
# Apply the tool first
srectl tool apply --name GetSystemHealthLogs

# Then apply the agent
srectl agent apply --name HelloWorldAgent
```

---

## Testing Your Agent

### Interactive Testing

Test your agent using the thread management system:

```bash
# Start a conversation with your agent
srectl thread new --message "Hello! Can you show me the current system health status?"

# Continue the conversation
srectl thread continue --message "What types of events are you seeing in the logs?"

# List all your conversation threads
srectl thread list
```

### Verify Deployment

Check that your agent and tool are deployed correctly:

```bash
# List all agents on the server
srectl list agents

# List all tools on the server
srectl list tools
```

### Expected Results

Your Hello World agent should:
1. **Respond to greetings** with information about its capabilities
2. **Execute the Kusto query** when asked about system health
3. **Provide summaries** of the log data retrieved
4. **Handle follow-up questions** about the system status

**Sample interaction:**
```
💬 You: Hello! Can you show me the current system health status?

🤖 HelloWorldAgent: Hello! I'll help you check the current system health status by querying recent logs. Let me fetch the latest system health information...

[Agent executes GetSystemHealthLogs tool]

Based on the recent logs, I can see:
- 15 heartbeat events from various computers
- 8 information-level system events  
- 3 syslog entries
- Most recent activity was 2 minutes ago

The systems appear to be functioning normally with regular heartbeat signals and standard informational events.
```

---

## Next Steps

### Enhance Your Agent

1. **Add More Tools:**
   ```bash
   srectl tool create --name GetErrorLogs --type KustoTool
   srectl tool create --name CheckServiceHealth --type KustoTool
   ```

2. **Create Specialized Agents:**
   ```bash
   srectl agent create --name DatabaseMonitorAgent --smart \
     --instructions "Focus on database performance monitoring and optimization"
   ```

3. **Set Up Agent Handoffs:**
   ```bash
   srectl agent create --name MetaAgent \
     --instructions "I coordinate between specialized agents" \
     --tools GetAgentRegistry HandoffToAgent \
     --handoffs HelloWorldAgent DatabaseMonitorAgent
   ```

### Advanced Features

- **Custom Connectors:** Create specialized data connectors for your specific systems
- **Complex Workflows:** Build agents that can execute multi-step troubleshooting procedures
- **Integration Testing:** Use the thread management system for comprehensive agent testing
- **CI/CD Integration:** Set up automated validation and deployment pipelines

### Learn More

- Explore the full [SRECTL Reference Guide](./srectl-reference.md)
- Review the [Agent Framework documentation](../../docs/)
- Check out more examples in the [Demo folder](../../Demo/)

### Troubleshooting

**Common Issues:**

1. **Connection Test Failures During Init:**
   - If `srectl init` completes but connection test fails with JSON parsing errors, this usually indicates authentication/permission issues
   - The improved error handling in SRECTL will now provide specific guidance:
     - HTTP 401 (Unauthorized): Run `az login` or check token scope
     - HTTP 403 (Forbidden): Configure cross-tenant access (see instructions above)
     - HTML error pages: Authentication issues - check if you have proper permissions
   - Even if connection test fails, you can still use SRECTL for local operations

2. **Authentication Errors:** Ensure you're logged in with `az login` and have proper permissions

3. **Cross-Tenant Access Issues:**
   - Get your Object ID from [Azure AD Overview](https://ms.portal.azure.com/#view/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/~/Overview)
   - Run: `az resource patch --ids <ARM_RESOURCE_ID> -p '{"adminUsers":[{"objectId":"<YOUR_OBJECT_ID>","tenantId":"72f988bf-86f1-41af-91ab-2d7cd011db47"}]}'`

4. **Tool Not Found:** Verify the tool is applied to the server with `srectl list tools`

5. **Query Errors:** Test your KQL queries in Azure Data Explorer first

6. **Agent Not Responding:** Check agent validation and ensure all referenced tools exist

**Getting Help:**
- Use `srectl --help` for command documentation
- Use `srectl <command> --help` for specific command help
- Check the validation output for detailed error messages
