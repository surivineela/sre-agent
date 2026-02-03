---
name: testing_with_srectl
description: Use when the user asks to test their changes using SRECTL. These instructions will tell you how to build and install SRECTL locally, how to use it with a locally-running SRE Agent, or use it with an SRE Agent deployed to Azure.
---

# Testing with SRECTL

SRECTL is a cross-platform .NET CLI tool for managing SRE Agent configurations, agents, tools, and threads. This skill covers how to build, install, and use SRECTL for testing.

## Prerequisites

- .NET 9 SDK installed
- PowerShell (Windows) or Bash (Linux/macOS)
- Azure CLI (`az login`) for connecting to remote/deployed SRE Agents

## Installing SRECTL (Recommended)

Run the build and install script from the repository root:

```powershell
.\src\Agent\Agent.Cli\scripts\build_and_install.ps1
```

This script builds the project, creates a NuGet package, and installs SRECTL as a global .NET tool. After installation, `srectl` is available from any directory.

**After installation, use `srectl --help` to discover all available commands and options.** Each subcommand also supports `--help` (e.g., `srectl agent --help`, `srectl thread --help`).

If installation fails or the user specifies a different preferred installation method, you can either run SRECTL without installing (see below), or for alternative installation methods (self-contained executable, manual installation, bash aliases), see [src/Agent/Agent.Cli/INSTALLATION.md](../../../src/Agent/Agent.Cli/INSTALLATION.md).

## Running SRECTL Without Installing

For quick testing during development without reinstalling after each change (like when you are iterating on changes to SRECTL itself):

```powershell
cd src/Agent/Agent.Cli
dotnet run -- <command> [options]
```

Example:
```powershell
dotnet run -- agent validate --all
dotnet run -- --help
```

**Tip:** Use `--no-restore` to speed up subsequent runs:
```powershell
dotnet run --no-restore -- thread list
```

## Connecting SRECTL to an SRE Agent

### Connecting to a Locally-Running SRE Agent

**Important:** Before running any SRECTL commands against a local agent, you must start the Agent.Web project. It is recommended to build the project first, then run without building. Always run the server in an external process so it stays running.

Keep this running in a separate terminal. The server runs at `https://localhost:7023` by default.

### Setting Up a Test Directory

Create a new test directory inside `TestPlayground` and initialize SRECTL there:

```powershell
# From the repo root
mkdir TestPlayground/my-test
cd TestPlayground/my-test

# Initialize SRECTL for local development
srectl init --resource-url https://localhost:7023
```

The `init` command creates:
- `.sreagent-config.json` - Configuration file with server URL
- `agents/` - Directory for agent YAML definitions
- `tools/` - Directory for tool YAML definitions
- `connectors/` - Directory for connector definitions
- Example YAML files in each directory

**Note:** Always run `srectl init` from an empty or new directory to avoid conflicts with existing files.

### Connecting to an Azure-Deployed SRE Agent

For SRE Agents deployed to Azure:

```powershell
# First, authenticate with Azure CLI
az login

# Then initialize SRECTL with the deployed endpoint
srectl init --resource-url https://your-sreagent-endpoint.azuresre.ai
```

## Managing Multiple Profiles

SRECTL supports profiles for switching between different SRE Agent instances:

```powershell
# Create profiles for different environments
srectl profile create --name local-dev --url https://localhost:7023
srectl profile create --name staging --url https://staging.azuresre.ai
srectl profile create --name production --url https://prod.azuresre.ai --set-current

# List all profiles (active profile marked with *)
srectl profile list

# Switch between profiles
srectl profile set --name local-dev

# Get current profile details
srectl profile get
```

## Common Testing Commands

Use `srectl --help` and `srectl <command> --help` to explore all available commands. Here are some commonly used ones:

### Agent Commands

```powershell
# Create a new agent
srectl agent create --name my_test_agent --instructions "Test agent instructions"

# Validate an agent definition
srectl agent validate --name my_test_agent
srectl agent validate --all

# Apply agent configuration to server
srectl agent apply --name my_test_agent

# List agents on the server
srectl agent list
```

IMPORTANT: Create agents using the V2 API spec. Prefer setting `enableVanillaMode: true` on test agents.

### V2 Agent YAML Format

When creating agent YAML files manually, use this format:

```yaml
api_version: azuresre.ai/v2
kind: ExtendedAgent
metadata:
  name: my_test_agent
  tags:
    - test
spec:
  instructions: |
    Your agent instructions here.
  handoffDescription: ""   # Required - use empty string to disable handoff
  enableVanillaMode: true  # Recommended for test agents
  # other fields optional
```

**Common YAML mistakes to avoid:**
- ❌ `api_version: "v2"` → ✅ `api_version: azuresre.ai/v2`
- ❌ `kind: ExtendedAgentV2` → ✅ `kind: ExtendedAgent`
- ❌ Missing `handoffDescription` → ✅ Include `handoffDescription: ""` (can be empty string)

### Thread Commands (Testing with Agents)

Thread commands support three modes:
- **Interactive (default)**: Starts an interactive chat session that prompts for more messages
- **`--wait`**: Sends message, waits for agent response, displays it, then exits automatically (recommended for testing)
- **`--no-wait`**: Fire-and-forget - sends message and exits immediately without waiting for response

**For automated testing, use `--wait` to see the agent's response:**

```powershell
# Start a thread with a specific agent and wait for response
srectl thread new --agent my_agent_name --message "Help me troubleshoot this issue" --wait

# Start a thread with the default agent and wait for response
srectl thread new --message "What can you help me with?" --wait

# Continue an existing thread and wait for response
srectl thread continue --thread-id <thread-id> --message "Tell me more" --wait

# Fire-and-forget (don't wait for response)
srectl thread new --message "Background task" --no-wait

# List all threads
srectl thread list

# Delete a thread
srectl thread delete --thread-id <thread-id>
```

## Uninstalling SRECTL

```powershell
dotnet tool uninstall sreagent.cli --global
```

## Cleanup

When you are done testing, delete the test agents on the server and then finally clean up the folders you created in the `TestPlayground` folder.
Use the `--delete-local-files` option when running any `delete` command to bypass the confirmation prompt.

## Troubleshooting

- **Localhost connection issues:** Ensure Agent.Web is running in a separate terminal
- **Remote connection issues:** Verify `az login` is authenticated and you have access to the SRE Agent resource
- **Installation issues:** Clear NuGet cache with `dotnet nuget locals all --clear`, then reinstall
- **Debug mode:** Add `--debug` flag to any command for verbose output
- **"Unsupported or invalid YAML version" error:** Ensure `api_version: azuresre.ai/v2` (not `v2` or `azuresre.ai/v1`)
- **"Handoff description cannot be null" error:** Add `handoffDescription: ""` to your agent spec
- **Server build is slow:** Build first with `dotnet build src/Agent/Agent.Web/Agent.Web.csproj --no-restore`, then run with `--no-build` flag

## Reference

- Full CLI documentation: [src/Agent/Agent.Cli/Readme.md](../../../src/Agent/Agent.Cli/Readme.md)
- Installation options: [src/Agent/Agent.Cli/INSTALLATION.md](../../../src/Agent/Agent.Cli/INSTALLATION.md)
- Build scripts: [src/Agent/Agent.Cli/scripts/](../../../src/Agent/Agent.Cli/scripts/)


