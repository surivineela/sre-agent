# CLI E2E Tests

Manual End-to-end tests for `srectl` commands to run locally.

## Setup

### 1. Run Agent locally

Run the Agent.Web project same way done for local development.

The server should be running at `https://localhost:7023`

### 2. Configure Test Settings (Optional)

Edit `CliTestSettings.json`:

```json
{
  "ServerUrl": "https://localhost:7023", // Set this to a prod agent endpoint to run against prod
  "TimeoutSeconds": 30,
  "Cleanup": true,
  "Debug": false  // Set to true for verbose CLI output
}
```

Or set environment variables:
```powershell
$env:AGENT_SERVER_URL = "https://localhost:7023"
$env:AGENT_CLI_DEBUG = "true"  # Enable debug mode
```

### 3. Build/Install CLI

```powershell
 cd .\Agent.Cli\scripts\
 .\build_and_install_exe.ps1
```

### 4. Run Tests

```powershell
cd Agent.Cli.E2ETests
dotnet test
```

## Test Output Verbosity

Choose the verbosity level based on what you need to see:

### Minimal Output (Recommended for CI/CD)
Shows only test summary and failures:
```powershell
dotnet test
```

### Normal Output
Shows test names and failure details:
```powershell
dotnet test --verbosity normal
```

### Detailed Output (For Debugging)
Shows all test output including WriteLine statements and CLI command execution:
```powershell
dotnet test --logger "console;verbosity=detailed"
```

This will display:
- Each CLI command being executed
- Full CLI output (with proper Unicode rendering)
- Test progress messages
- All assertions and their results

## Running Individual Tests

```powershell
# Run all agent command tests
dotnet test --filter "FullyQualifiedName~AgentCommandE2ETests"

# Run specific test
dotnet test --filter "Name~AgentCreate_WithValidName"
```

## Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `ServerUrl` | Agent server endpoint | `https://localhost:7023` |
| `TimeoutSeconds` | Command timeout in seconds | `120` |
| `Cleanup` | Remove test data after tests | `true` |
| `CliPath` | Override CLI executable path | Auto-detect |
| `Debug` | Add --debug flag to all CLI commands | `false` |
