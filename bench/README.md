# SREAgent Evaluation Framework

A e2e test style evaluation framework for testing Azure SRE Agent on Site Reliability Engineering (SRE) tasks.

## Overview

SREAgent-Eval provides a structured way to evaluate how well Azure SRE Agent can handle real-world SRE operations, troubleshooting, and management tasks. The framework is fully extensible to support various evaluation scenarios such as Azure Kubernetes Service (AKS), Azure Container Apps (ACA), and other cloud services. In the initial version, we included sample test cases for both AKS and ACA.

## Features

- **Task-based evaluation**: Run predefined tasks to test AI agent capabilities
- **Multi-platform support**: Works with various Kubernetes distributions and cloud providers
- **Flexible configuration**: Customize evaluation parameters and execution environments
- **LLM-based verification**: Support for Large Language Model verification of agent responses
- **Result analysis**: Built-in tools for analyzing and comparing evaluation results
- **Concurrent execution**: Run multiple evaluations in parallel for faster results
- **Cross-platform compatibility**: Supports both Windows and Unix-based systems

## Installation

### Prerequisites

- Go 1.24.0 or later
- kubectl configured with access to a Kubernetes cluster
- Azure CLI (for Azure services)
- .NET runtime (if using dotnet-based agents)

### Environment Variables

- `AGENT_URL`: URL of the AI agent to evaluate (defaults to `https://localhost:7023`)
- `DOTNET_AGENT_PATH`: Path to the .NET agent project (set automatically when using `--dotnet-project-path`)
- `LLM_API_KEY`: API key for LLM verification (required for LLM-based verification)
- `LLM_ENDPOINT`: Endpoint URL for LLM API (supports Azure OpenAI)

## Usage

The framework provides two main commands: `run` for executing evaluations.

### Running Evaluations

Basic command structure:
```bash
go run . run [flags]
```

Example with Azure Kubernetes Service:
```bash
go run . run \
  --dotnet-project-path "/path/to/sreagent-runtime" \
  --output-dir .build/results \
  --concurrency 1 \
  --aks-cluster-name "aksevals" \
  --tasks-dir "./tasks/azure-kubernetes-service"
```

PowerShell example (Windows):
```powershell
go run . run `
  --output-dir .build/results `
  --azure-rg "aksevals" `
  --azure-sub "xxxxx-xxxx-xxxx-xxxx-xxxxxx" `
  --tasks-dir "C:\path\to\sreagent-eval\tasks\azure-kubernetes-service" `
  --aks-cluster-name "aksevals" `
  --dotnet-project-path "C:\path\to\sreagent-runtime" `
  --concurrency 1
```

Example with Azure Container App:
```bash
go run . run \
  --dotnet-project-path "/path/to/sreagent-runtime" \
  --output-dir .build/results \
  --concurrency 1 \
  --tasks-dir "./tasks/azure-containerapp" \
  --azure-rg "aksevals" \
  --azure-capp-name "yash-capp"
```

### Available Flags for `run` command

| Flag | Description | Default |
|------|-------------|---------|
| `--tasks-dir` | Directory containing evaluation tasks | `./tasks/azure-kubernetes-service` |
| `--output-dir` | Directory to store evaluation results | `output` |
| `--task-pattern` | Filter tasks by pattern | - |
| `--concurrency` | Number of tasks to run in parallel (0 for auto) | `0` |
| `--agent-url` | URL of the AI agent to evaluate (overrides AGENT_URL env) | `https://localhost:7023` |
| `--kubeconfig` | Path to kubeconfig file | `~/.kube/config` |
| `--aks-cluster-name` | Name of AKS cluster (for Azure evaluations) | - |
| `--azure-rg` | Azure resource group | - |
| `--azure-sub` | Azure subscription ID | - |
| `--azure-capp-name` | Azure Container App name | - |
| `--azure-webapp-name` | Azure Web App name | - |
| `--dotnet-project-path` | Path to .NET agent project | - |
| `--quiet` | Suppress non-error output | `false` |

## Task Structure

Each evaluation task is contained in its own directory with the following structure:

```
tasks/
└── azure-kubernetes-service/
    └── create-pod/
        ├── task.yaml         # Task definition
        ├── setup.sh          # Unix setup script
        ├── setup.ps1         # Windows setup script
        ├── cleanup.sh        # Unix cleanup script
        ├── cleanup.ps1       # Windows cleanup script
        ├── verify.sh         # Unix verification script
        └── verify.ps1        # Windows verification script
```

### Task Definition (task.yaml)

```yaml
script:
  prompt: "Please create a nginx pod named web-server in the create-pod-test namespace in {{.aksClusterName}} cluster"
setup: "setup.sh"
setupWindows: "setup.ps1"
verifier: "verify.sh"
verifierWindows: "verify.ps1"
cleanup: "cleanup.sh"
cleanupWindows: "cleanup.ps1"
```

### Task Fields

- **script.prompt**: The prompt to send to the AI agent (supports template variables)
- **setup/setupWindows**: Scripts to prepare the environment
- **verifier/verifierWindows**: Scripts to verify task completion
- **cleanup/cleanupWindows**: Scripts to clean up after the task
- **disabled**: Set to `true` to skip this task
- **llmVerification**: Configuration for LLM-based verification

### Template Variables

Task prompts support template variables that are automatically substituted:

- `{{.aksClusterName}}`: AKS cluster name
- `{{.azureResourceGroup}}`: Azure resource group
- `{{.azureSubscription}}`: Azure subscription ID
- `{{.azureContainerAppName}}`: Azure Container App name
- `{{.azureWebAppName}}`: Azure Web App name

## LLM Verification

The framework supports using Large Language Models to verify agent responses. This is useful for tasks where the success criteria are complex or subjective.

### Configuration

Set the following environment variables:
```bash
export LLM_API_KEY="your-api-key"
export LLM_ENDPOINT="https://your-resource.openai.azure.com/openai/deployments/your-deployment/chat/completions?api-version=2024-02-01"
```

The framework automatically detects Azure OpenAI endpoints and configures authentication accordingly.

### In Task Definition

```yaml
llmVerification:
  enabled: true
  prompt: "Verify that the agent correctly diagnosed the issue and provided appropriate remediation steps"
  skipScriptVerify: true  # Only use LLM verification
```

## Output

### Evaluation Results

Results are stored in the output directory with the following structure:
```
output/
└── task-name/
    ├── log.txt        # Complete execution log
    └── results.yaml   # Task result summary
```

### Result Format

Each `results.yaml` contains:
```yaml
name: "task-name"
agentConfig:
  name: "default"
  agentURL: "https://localhost:7023"
result: "success"  # or "fail"
error: ""  # Error message if any
```

## Development

### Adding New Tasks

1. Create a new directory under the appropriate service folder
2. Add a `task.yaml` file with the task definition
3. Create setup, verification, and cleanup scripts for both Unix and Windows

## Troubleshooting

### Common Issues

1. **Agent not responding**: Ensure the agent is running and accessible at the configured URL
2. **Tasks failing to start**: Check that all required environment variables are set
3. **Verification failures**: Review the task logs in the output directory
4. **LLM verification errors**: Verify your LLM API credentials and endpoint
