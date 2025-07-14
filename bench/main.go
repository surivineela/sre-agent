package main

import (
    "context"
    "encoding/json"
    "flag"
    "fmt"
    "os"
    "path/filepath"
    "strings"

    "github.com/GoogleCloudPlatform/kubectl-ai/k8s-bench/pkg/model"
    "sigs.k8s.io/yaml"
)

const (
    defaultTasksDir      = "./tasks/azure-kubernetes-service"
    defaultAgentURL      = "https://localhost:7023"
    defaultKubeConfig    = "~/.kube/config"
    jsonOutputFormat     = "json"
    markdownOutputFormat = "markdown"
)

// Task represents a single evaluation task
type Task struct {
    Setup           string           `json:"setup,omitempty"`
    SetupWindows    string           `json:"setupWindows,omitempty"`
    Verifier        string           `json:"verifier,omitempty"`
    VerifierWindows string           `json:"verifierWindows,omitempty"`
    Cleanup         string           `json:"cleanup,omitempty"`
    CleanupWindows  string           `json:"cleanupWindows,omitempty"`
    Disabled        bool             `json:"disabled,omitempty"`
    Script          *ScriptStep      `json:"script,omitempty"`
    Isolation       IsolationMode    `json:"isolation,omitempty"`
    // Optional LLM verification
    LLMVerification *LLMVerification `json:"llmVerification,omitempty"`
}

// LLMVerification configuration for Q&A evaluation
type LLMVerification struct {
    Enabled          bool   `json:"enabled"`
    Prompt           string `json:"prompt"`
    SkipScriptVerify bool   `json:"skipScriptVerify,omitempty"` // Skip script verification if LLM passes
}

// IsolationMode defines how tasks should be isolated during execution
type IsolationMode string

const (
    // IsolationModeCluster creates a dedicated cluster for task execution
    IsolationModeCluster IsolationMode = "cluster"
)

// ScriptStep represents a single step in the task script
type ScriptStep struct {
    Prompt string `json:"prompt"`
}

// EvalConfig holds configuration for running evaluations
type EvalConfig struct {
    AgentConfigs          []model.AgentConfig
    KubeConfig            string
    TasksDir              string
    TaskPattern           string
    AgentBin              string
    Concurrency           int
    OutputDir             string
    AzureResourceGroup    string
    AzureSubscription     string
    AzureContainerAppName string
    AzureWebAppName       string
    AksClusterName        string
}

// validate checks if the configuration is valid
func (c *EvalConfig) validate() error {
    if c.OutputDir == "" {
        return fmt.Errorf("output directory is required")
    }
    if c.TasksDir == "" {
        return fmt.Errorf("tasks directory is required")
    }
    return nil
}

// AnalyzeConfig holds configuration for analyzing results
type AnalyzeConfig struct {
    InputDir          string
    OutputFormat      string
    IgnoreToolUseShim bool
    ResultsFilePath   string
}

// validate checks if the analyze configuration is valid
func (c *AnalyzeConfig) validate() error {
    if c.InputDir == "" {
        return fmt.Errorf("input directory is required")
    }
    if c.OutputFormat != jsonOutputFormat && c.OutputFormat != markdownOutputFormat {
        return fmt.Errorf("invalid output format: %s (must be 'json' or 'markdown')", c.OutputFormat)
    }
    return nil
}

func main() {
    if err := run(); err != nil {
        fmt.Fprintf(os.Stderr, "Error: %v\n", err)
        os.Exit(1)
    }
}

func run() error {
    // Handle top-level help
    if len(os.Args) > 1 && (os.Args[1] == "--help" || os.Args[1] == "-h") {
        printUsage()
        return nil
    }

    // Determine subcommand
    subCommand := "run"
    if len(os.Args) > 1 && !strings.HasPrefix(os.Args[1], "-") {
        subCommand = os.Args[1]
        os.Args = append(os.Args[:1], os.Args[2:]...)
    }

    switch subCommand {
    case "run":
        return runCommand()
    case "analyze":
        return analyzeCommand()
    default:
        printUsage()
        return fmt.Errorf("unknown subcommand: %s", subCommand)
    }
}

// printUsage displays the top-level usage information
func printUsage() {
    fmt.Fprintf(os.Stderr, `Usage: %s <command> [options]

Commands:
  run       Run evaluation benchmarks
  analyze   Analyze results from previous benchmark runs

Run '%s <command> --help' for more information on a command.
`, os.Args[0], os.Args[0])
}

// runCommand handles the 'run' subcommand
func runCommand() error {
    config := EvalConfig{
        TasksDir: defaultTasksDir,
    }

    // Configure flags
    fs := flag.NewFlagSet("run", flag.ExitOnError)
    var dotnetProjectPath string

    // Azure-specific flags
    fs.StringVar(&config.AzureResourceGroup, "azure-rg", "", "Azure resource group")
    fs.StringVar(&config.AzureSubscription, "azure-sub", "", "Azure subscription ID")
    fs.StringVar(&config.AzureContainerAppName, "azure-capp-name", "", "Azure Container App name")
    fs.StringVar(&config.AzureWebAppName, "azure-webapp-name", "", "Azure Web App name")
    fs.StringVar(&config.AksClusterName, "aks-cluster-name", "", "AKS cluster name")

    // Kubernetes-specific flags
    fs.StringVar(&config.KubeConfig, "kubeconfig", defaultKubeConfig, "Path to the kubeconfig file")

    // General flags
    fs.StringVar(&dotnetProjectPath, "dotnet-project-path", "", "Path to Azure SRE agent project")
    fs.StringVar(&config.TasksDir, "tasks-dir", config.TasksDir, "Directory containing task definitions")
    fs.StringVar(&config.OutputDir, "output-dir", "output", "Directory to store evaluation results")
    fs.StringVar(&config.TaskPattern, "task-pattern", "", "Filter tasks by pattern")
    fs.IntVar(&config.Concurrency, "concurrency", 0, "Number of concurrent tasks (0 for auto)")

    // Quiet mode flag
    quiet := fs.Bool("quiet", false, "Suppress non-error output")

    // Parse flags
    if err := fs.Parse(os.Args[1:]); err != nil {
        return err
    }

    // Set defaults and expand paths
    config.KubeConfig = expandPath(config.KubeConfig)
    config.TasksDir = expandPath(config.TasksDir)
    config.OutputDir = expandPath(config.OutputDir)

    // Configure agent
    agentURL := os.Getenv("AGENT_URL")
    if agentURL == "" {
        agentURL = defaultAgentURL
    }

    config.AgentConfigs = []model.AgentConfig{
        {
            Name:     "default",
            AgentURL: agentURL,
        },
    }

	// Set dotnet project path if provided
	if dotnetProjectPath != "" {
		os.Setenv("DOTNET_AGENT_PATH", dotnetProjectPath)
	}

    // Validate configuration
    if err := config.validate(); err != nil {
        return err
    }

    // Load tasks to determine auto-concurrency
    if config.Concurrency == 0 {
        // This will be set in runEvaluation based on task count
        config.Concurrency = 1
    }

    // Auto-configure concurrency if not set
    if *quiet {
        // Suppress output in quiet mode
        // You might want to configure logging here
    }

    // Run evaluation
    ctx := context.Background()
    return runEvaluation(ctx, config)
}

// analyzeCommand handles the 'analyze' subcommand
func analyzeCommand() error {
    config := AnalyzeConfig{
        OutputFormat: markdownOutputFormat,
    }

    fs := flag.NewFlagSet("analyze", flag.ExitOnError)
    fs.StringVar(&config.InputDir, "input-dir", "", "Directory containing evaluation results (required)")
    fs.StringVar(&config.OutputFormat, "format", config.OutputFormat, "Output format: json or markdown")
    fs.BoolVar(&config.IgnoreToolUseShim, "ignore-tool-use-shim", false, "Ignore tool use shim tasks in results")
    fs.StringVar(&config.ResultsFilePath, "output", "", "Output file path (optional, defaults to stdout)")

    if err := fs.Parse(os.Args[1:]); err != nil {
        return err
    }

    if err := config.validate(); err != nil {
        return err
    }

    // Expand paths
    config.InputDir = expandPath(config.InputDir)

    // Collect results
    results, err := collectResults(config.InputDir)
    if err != nil {
        return fmt.Errorf("collecting results: %w", err)
    }

    // Filter results if needed
    if config.IgnoreToolUseShim {
        filtered := []model.TaskResult{}
        for _, result := range results {
            if !strings.Contains(result.Task, "tool-use-shim") {
                filtered = append(filtered, result)
            }
        }
        results = filtered
    }

    // Output results
    var output []byte
    switch config.OutputFormat {
    case jsonOutputFormat:
        output, err = outputJSONResults(results)
    case markdownOutputFormat:
        output, err = outputMarkdownResults(results)
    default:
        return fmt.Errorf("unknown format: %s", config.OutputFormat)
    }

    if err != nil {
        return fmt.Errorf("formatting output: %w", err)
    }

    // Write output
    if config.ResultsFilePath != "" {
        if err := os.WriteFile(config.ResultsFilePath, output, 0644); err != nil {
            return fmt.Errorf("writing output file: %w", err)
        }
    } else {
        fmt.Print(string(output))
    }

    return nil
}

// expandPath expands ~ and environment variables in a path
func expandPath(path string) string {
    if strings.HasPrefix(path, "~/") {
        home, _ := os.UserHomeDir()
        path = filepath.Join(home, path[2:])
    }
    return os.ExpandEnv(path)
}

// collectResults walks the directory tree and collects all results.yaml files
func collectResults(inputDir string) ([]model.TaskResult, error) {
    var results []model.TaskResult

    err := filepath.Walk(inputDir, func(path string, info os.FileInfo, err error) error {
        if err != nil {
            return err
        }

        if info.Name() == "results.yaml" {
            data, err := os.ReadFile(path)
            if err != nil {
                return fmt.Errorf("reading %s: %w", path, err)
            }

            var result model.TaskResult
            if err := yaml.Unmarshal(data, &result); err != nil {
                return fmt.Errorf("parsing %s: %w", path, err)
            }

            results = append(results, result)
        }

        return nil
    })

    return results, err
}

// outputJSONResults writes results in JSON format
func outputJSONResults(results []model.TaskResult) ([]byte, error) {
    return json.MarshalIndent(results, "", "  ")
}

// outputMarkdownResults writes results in Markdown format
func outputMarkdownResults(results []model.TaskResult) ([]byte, error) {
    var builder strings.Builder

    builder.WriteString("# Evaluation Results\n\n")

    // Group results by task
    taskResults := make(map[string][]model.TaskResult)
    for _, result := range results {
        taskResults[result.Task] = append(taskResults[result.Task], result)
    }

    // Calculate statistics
    totalTasks := len(taskResults)
    successfulTasks := 0
    for _, results := range taskResults {
        allSuccess := true
        for _, result := range results {
            if result.Result != "success" {
                allSuccess = false
                break
            }
        }
        if allSuccess {
            successfulTasks++
        }
    }

    builder.WriteString(fmt.Sprintf("## Summary\n\n"))
    builder.WriteString(fmt.Sprintf("- Total tasks: %d\n", totalTasks))
    builder.WriteString(fmt.Sprintf("- Successful: %d\n", successfulTasks))
    builder.WriteString(fmt.Sprintf("- Failed: %d\n", totalTasks-successfulTasks))
    builder.WriteString(fmt.Sprintf("- Success rate: %.1f%%\n\n", float64(successfulTasks)/float64(totalTasks)*100))

    // Task details
    builder.WriteString("## Task Details\n\n")
    for task, results := range taskResults {
        builder.WriteString(fmt.Sprintf("### %s\n\n", task))
        for _, result := range results {
            builder.WriteString(fmt.Sprintf("- **Agent**: %s\n", result.AgentConfig.Name))
            builder.WriteString(fmt.Sprintf("- **Result**: %s\n", result.Result))
            if result.Error != "" {
                builder.WriteString(fmt.Sprintf("- **Error**: %s\n", result.Error))
            }
            builder.WriteString("\n")
		}
	}
	return []byte(builder.String()), nil
}