package main

import (
    "bytes"
    "context"
    "crypto/tls"
    "encoding/json"
    "errors"
    "fmt"
    "io"
    "net/http"
    "os"
    "os/exec"
    "path/filepath"
    "runtime"
    "strings"
    "sync"
    "text/template"
    "time"

    "github.com/GoogleCloudPlatform/kubectl-ai/k8s-bench/pkg/model"
    "k8s.io/klog/v2"
    "sigs.k8s.io/yaml"
)

const (
    defaultWaitTime       = 3 * time.Minute
    agentStartupTimeout   = 90 * time.Second
    httpClientTimeout     = 30 * time.Second
    healthCheckTimeout    = 2 * time.Second
    taskWorkerIDFormat    = "Worker %d: Evaluating task: %s\n"
    clusterNameFormat     = "k8s-bench-%s"
    kubeconfigEnvVar      = "KUBECONFIG"
    dotnetAgentPathEnvVar = "DOTNET_AGENT_PATH"
)

// Add these new types for the thread API response
type ThreadMessagesResponse struct {
    Value []ThreadMessage `json:"value"`
}

type ThreadMessage struct {
    Id        string    `json:"id"`
    TimeStamp time.Time `json:"timeStamp"`
    Author    struct {
        Role        string `json:"role"`
        UserId      string `json:"userId"`
        DisplayName string `json:"displayName"`
    } `json:"author"`
    Text string `json:"text"`
}

// taskJob represents a task to be processed by workers
type taskJob struct {
    taskID string
    task   Task
}

// runEvaluation orchestrates the evaluation of tasks with concurrent execution
func runEvaluation(ctx context.Context, config EvalConfig) error {
    if err := config.validate(); err != nil {
        return fmt.Errorf("invalid configuration: %w", err)
    }

    tasks, err := loadTasks(config)
    if err != nil {
        return fmt.Errorf("failed to load tasks: %w", err)
    }

    if len(tasks) == 0 {
        return fmt.Errorf("no tasks found matching criteria")
    }

    // Set default concurrency if not specified
    if config.Concurrency <= 0 {
        config.Concurrency = 1
    }

    results, err := executeTasks(ctx, config, tasks)
    if err != nil {
        return fmt.Errorf("task execution failed: %w", err)
    }

    printResults(results)
    return nil
}

// executeTasks runs tasks concurrently and collects results
func executeTasks(ctx context.Context, config EvalConfig, tasks map[string]Task) ([]model.TaskResult, error) {
    taskCh := make(chan taskJob, len(tasks))
    resultsCh := make(chan model.TaskResult, len(tasks)*len(config.AgentConfigs))
    errorsCh := make(chan error, config.Concurrency)

    // Load tasks into channel
    for taskID, task := range tasks {
        taskCh <- taskJob{taskID: taskID, task: task}
    }
    close(taskCh)

    var wg sync.WaitGroup
    fmt.Printf("Running %d tasks with concurrency: %d\n", len(tasks), config.Concurrency)

    // Start worker goroutines
    for i := 0; i < config.Concurrency; i++ {
        wg.Add(1)
        go taskWorker(ctx, i, taskCh, resultsCh, errorsCh, config, &wg)
    }

    // Wait for all workers to complete
    wg.Wait()
    close(resultsCh)
    close(errorsCh)

    // Check for errors
    for err := range errorsCh {
        if err != nil {
            return nil, err
        }
    }

    // Collect results
    var results []model.TaskResult
    for result := range resultsCh {
        results = append(results, result)
    }

    return results, nil
}

// taskWorker processes tasks from the task channel
func taskWorker(ctx context.Context, workerID int, taskCh <-chan taskJob,
    resultsCh chan<- model.TaskResult, errorsCh chan<- error,
    config EvalConfig, wg *sync.WaitGroup) {

    defer wg.Done()

    for job := range taskCh {
        fmt.Printf(taskWorkerIDFormat, workerID, job.taskID)

        for _, agentConfig := range config.AgentConfigs {
            result, err := processTask(ctx, config, job, agentConfig)
            if err != nil {
                errorsCh <- fmt.Errorf("task %s failed: %w", job.taskID, err)
                return
            }
            resultsCh <- result
        }
    }
}

// processTask handles the execution of a single task
func processTask(ctx context.Context, config EvalConfig, job taskJob,
    agentConfig model.AgentConfig) (model.TaskResult, error) {

    taskOutputDir := filepath.Join(config.OutputDir, job.taskID)
    if err := os.MkdirAll(taskOutputDir, 0755); err != nil {
        return model.TaskResult{}, fmt.Errorf("creating output directory: %w", err)
    }

    logFile, err := createLogFile(taskOutputDir)
    if err != nil {
        return model.TaskResult{}, err
    }
    defer logFile.Close()

    result := evaluateTask(ctx, config, job.taskID, job.task, agentConfig, logFile)

    if err := writeToYAMLFile(filepath.Join(taskOutputDir, "results.yaml"), result); err != nil {
        return result, fmt.Errorf("writing results: %w", err)
    }

    return result, nil
}

// createLogFile creates and returns a log file for task output
func createLogFile(taskOutputDir string) (*os.File, error) {
    logPath := filepath.Join(taskOutputDir, "log.txt")
    logFile, err := os.Create(logPath)
    if err != nil {
        return nil, fmt.Errorf("creating log file %q: %w", logPath, err)
    }
    return logFile, nil
}

// writeToYAMLFile marshals an object to YAML and writes it to a file
func writeToYAMLFile(path string, obj any) error {
    data, err := yaml.Marshal(obj)
    if err != nil {
        return fmt.Errorf("marshaling to yaml: %w", err)
    }
    if err := os.WriteFile(path, data, 0644); err != nil {
        return fmt.Errorf("writing to file %q: %w", path, err)
    }
    return nil
}

// loadTasks reads and parses task definitions from the tasks directory
func loadTasks(config EvalConfig) (map[string]Task, error) {
    tasks := make(map[string]Task)

    entries, err := os.ReadDir(config.TasksDir)
    if err != nil {
        return nil, fmt.Errorf("reading tasks directory: %w", err)
    }

    for _, entry := range entries {
        if !entry.IsDir() {
            continue
        }

        taskID := entry.Name()
        if config.TaskPattern != "" && !strings.Contains(taskID, config.TaskPattern) {
            continue
        }

        task, err := loadTask(config.TasksDir, taskID)
        if err != nil {
            return nil, err
        }

        if task.Disabled {
            fmt.Printf("Skipping disabled task: %s\n", taskID)
            continue
        }

        tasks[taskID] = task
    }

    return tasks, nil
}

// loadTask reads and parses a single task definition
func loadTask(tasksDir, taskID string) (Task, error) {
    taskFile := filepath.Join(tasksDir, taskID, "task.yaml")
    data, err := os.ReadFile(taskFile)
    if err != nil {
        return Task{}, fmt.Errorf("reading task file %s: %w", taskFile, err)
    }

    var task Task
    if err := yaml.Unmarshal(data, &task); err != nil {
        return Task{}, fmt.Errorf("parsing task file %s: %w", taskFile, err)
    }

    return task, nil
}

// evaluateTask executes a single task and returns the result
func evaluateTask(ctx context.Context, config EvalConfig, taskID string,
    task Task, agentConfig model.AgentConfig, log io.Writer) model.TaskResult {

    result := model.TaskResult{
        Task:        taskID,
        AgentConfig: agentConfig,
    }

    taskDir, err := filepath.Abs(filepath.Join(config.TasksDir, taskID))
    if err != nil {
        result.Result = "fail"
        result.Error = fmt.Sprintf("resolving task directory: %v", err)
        return result
    }

    execution := newTaskExecution(config, taskID, task, agentConfig, log, taskDir)

    // Ensure cleanup runs
    defer func() {
        if err := execution.runCleanup(ctx); err != nil {
            klog.Warningf("Cleanup failed for task %s: %v", taskID, err)
        }
    }()

    // Run setup
    if err := execution.runSetup(ctx); err != nil {
        result.Error = fmt.Sprintf("setup failed: %v", err)
        return result
    }

    // Run agent
    if err := execution.runAgent(ctx); err != nil {
        result.Error = fmt.Sprintf("agent execution failed: %v", err)
        return result
    }

    // Wait for agent operations to complete
    fmt.Printf("Waiting %v for agent operations to complete...\n", defaultWaitTime)
    time.Sleep(defaultWaitTime)

    // Run verification
    result = execution.runVerification(ctx, result)

    return result
}

// newTaskExecution creates a new TaskExecution instance
func newTaskExecution(config EvalConfig, taskID string, task Task,
    agentConfig model.AgentConfig, log io.Writer, taskDir string) *TaskExecution {

    return &TaskExecution{
        kubeConfig:            config.KubeConfig,
        AgentBin:              config.AgentBin,
        agentConfig:           agentConfig,
        log:                   log,
        task:                  &task,
        taskID:                taskID,
        taskDir:               taskDir,
        taskOutputDir:         filepath.Join(config.OutputDir, taskID),
        azureResourceGroup:    config.AzureResourceGroup,
        azureSubscription:     config.AzureSubscription,
        azureContainerAppName: config.AzureContainerAppName,
        azureWebAppName:       config.AzureWebAppName,
        aksClusterName:        config.AksClusterName,
    }
}

// TaskExecution encapsulates the execution context for a single task
type TaskExecution struct {
    kubeConfig            string
    AgentBin              string
    agentConfig           model.AgentConfig
    result                *model.TaskResult
    log                   io.Writer
    task                  *Task
    taskID                string
    taskDir               string
    taskOutputDir         string
    cleanupFunctions      []func() error
    azureResourceGroup    string
    azureSubscription     string
    azureContainerAppName string
    azureWebAppName       string
    aksClusterName        string
    threadID              string // Store thread ID for LLM verification
}

// runSetup prepares the environment for task execution
func (x *TaskExecution) runSetup(ctx context.Context) error {
    log := klog.FromContext(ctx)

    // Create isolated cluster if requested
    if x.task.Isolation == IsolationModeCluster {
        if err := x.createIsolatedCluster(ctx); err != nil {
            return err
        }
    }

    // Run setup script if provided
    if x.hasSetupScript() {
        return x.runSetupScript(ctx)
    }

    log.V(2).Info("No setup required", "task", x.taskID)
    return nil
}

// createIsolatedCluster creates a Kind cluster for isolated task execution
func (x *TaskExecution) createIsolatedCluster(ctx context.Context) error {
    kubeconfigPath := filepath.Join(x.taskDir, "kubeconfig.yaml")
    x.kubeConfig = kubeconfigPath

    clusterName := fmt.Sprintf(clusterNameFormat, x.taskID)
    klog.FromContext(ctx).Info("Creating Kind cluster", "name", clusterName)

    cmd := exec.CommandContext(ctx, "kind", "create", "cluster",
        "--name", clusterName,
        "--wait", "5m",
        "--kubeconfig", kubeconfigPath)
    cmd.Dir = x.taskDir

    // Register cleanup function
    x.cleanupFunctions = append(x.cleanupFunctions, func() error {
        return x.deleteKindCluster(ctx, clusterName, kubeconfigPath)
    })

    return x.runCommand(cmd)
}

// deleteKindCluster removes the Kind cluster created for the task
func (x *TaskExecution) deleteKindCluster(ctx context.Context, clusterName, kubeconfigPath string) error {
    cmd := exec.CommandContext(ctx, "kind", "delete", "cluster",
        "--name", clusterName,
        "--kubeconfig", kubeconfigPath)
    cmd.Dir = x.taskDir
    return x.runCommand(cmd)
}

// hasSetupScript checks if a setup script is defined for the current OS
func (x *TaskExecution) hasSetupScript() bool {
    return (runtime.GOOS == "windows" && x.task.SetupWindows != "") || x.task.Setup != ""
}

// runSetupScript executes the appropriate setup script for the current OS
func (x *TaskExecution) runSetupScript(ctx context.Context) error {
    scriptPath, err := x.getScriptPath(x.task.Setup, x.task.SetupWindows, "setup")
    if err != nil {
        return err
    }

    cmd := x.createScriptCommand(ctx, scriptPath)
    x.configureScriptEnvironment(cmd)

    fmt.Printf("Running setup script: %s\n", scriptPath)
    if err := x.runCommand(cmd); err != nil {
        return fmt.Errorf("setup script failed: %w", err)
    }

    fmt.Printf("Setup completed successfully for task %s\n", x.taskID)
    return nil
}

// getScriptPath returns the appropriate script path for the current OS
func (x *TaskExecution) getScriptPath(unixScript, windowsScript, scriptType string) (string, error) {
    isWindows := runtime.GOOS == "windows"

    if isWindows && windowsScript != "" {
        return filepath.Join(x.taskDir, windowsScript), nil
    } else if !isWindows && unixScript != "" {
        return filepath.Join(x.taskDir, unixScript), nil
    }

    return "", fmt.Errorf("no %s script available for %s", scriptType, runtime.GOOS)
}

// createScriptCommand creates the appropriate command for running a script
func (x *TaskExecution) createScriptCommand(ctx context.Context, scriptPath string) *exec.Cmd {
    if runtime.GOOS == "windows" && strings.HasSuffix(scriptPath, ".ps1") {
        return exec.CommandContext(ctx, "powershell.exe", "-ExecutionPolicy", "Bypass", "-File", scriptPath)
    }
    return exec.CommandContext(ctx, scriptPath)
}

// configureScriptEnvironment sets up environment variables for script execution
func (x *TaskExecution) configureScriptEnvironment(cmd *exec.Cmd) {
    cmd.Dir = x.taskDir
    cmd.Env = append(os.Environ(),
        fmt.Sprintf("%s=%s", kubeconfigEnvVar, x.kubeConfig),
        fmt.Sprintf("AZURE_RG=%s", x.azureResourceGroup),
        fmt.Sprintf("AZURE_SUB=%s", x.azureSubscription),
        fmt.Sprintf("AZURE_CAPP_NAME=%s", x.azureContainerAppName),
        fmt.Sprintf("AZURE_WEBAPP_NAME=%s", x.azureWebAppName),
        fmt.Sprintf("AKS_CLUSTER_NAME=%s", x.aksClusterName),
    )
}

// substituteVariables replaces template variables in the input string
func (x *TaskExecution) substituteVariables(input string) (string, error) {
    vars := map[string]string{
        "resourceGroup":    x.azureResourceGroup,
        "subscription":     x.azureSubscription,
        "containerAppName": x.azureContainerAppName,
        "webAppName":       x.azureWebAppName,
        "kubeconfig":       x.kubeConfig,
        "aksClusterName":   x.aksClusterName,
    }

    tmpl, err := template.New("prompt").Parse(input)
    if err != nil {
        return "", fmt.Errorf("parsing template: %w", err)
    }

    var buf bytes.Buffer
    if err := tmpl.Execute(&buf, vars); err != nil {
        return "", fmt.Errorf("executing template: %w", err)
    }

    return buf.String(), nil
}

// runCleanup executes cleanup operations for the task
func (x *TaskExecution) runCleanup(ctx context.Context) error {
    var errs []error

    // Run cleanup script if defined
    if x.hasCleanupScript() {
        if err := x.runCleanupScript(ctx); err != nil {
            errs = append(errs, err)
        }
    }

    // Run registered cleanup functions
    for _, cleanup := range x.cleanupFunctions {
        if err := cleanup(); err != nil {
            errs = append(errs, err)
        }
    }

    return errors.Join(errs...)
}

// hasCleanupScript checks if a cleanup script is defined for the current OS
func (x *TaskExecution) hasCleanupScript() bool {
    return (runtime.GOOS == "windows" && x.task.CleanupWindows != "") || x.task.Cleanup != ""
}

// runCleanupScript executes the appropriate cleanup script for the current OS
func (x *TaskExecution) runCleanupScript(ctx context.Context) error {
    scriptPath, err := x.getScriptPath(x.task.Cleanup, x.task.CleanupWindows, "cleanup")
    if err != nil {
        klog.V(2).Info("No cleanup script available", "task", x.taskID, "os", runtime.GOOS)
        return nil
    }

    cmd := x.createScriptCommand(ctx, scriptPath)
    x.configureScriptEnvironment(cmd)

    fmt.Printf("Running cleanup script: %s\n", scriptPath)
    if err := x.runCommand(cmd); err != nil {
        return fmt.Errorf("cleanup script failed: %w", err)
    }

    return nil
}

// runAgent executes the Azure SRE agent for the task
func (x *TaskExecution) runAgent(ctx context.Context) error {
    agentURL := x.agentConfig.AgentURL
    if agentURL == "" {
        agentURL = defaultAgentURL
    }

    // Ensure agent is running
    if err := x.ensureAgentRunning(ctx, agentURL); err != nil {
        return fmt.Errorf("ensuring agent is running: %w", err)
    }

    // Process the single script step
    return x.processScriptStep(ctx, agentURL)
}

// ensureAgentRunning checks if the agent is running and starts it if necessary
func (x *TaskExecution) ensureAgentRunning(ctx context.Context, agentURL string) error {
    client := createHTTPClient(healthCheckTimeout)

    // Check if agent is already running
    if x.isAgentRunning(client, agentURL) {
        fmt.Println("Agent is already running")
        return nil
    }

    // Start the agent
    return x.startAgent(ctx, client, agentURL)
}

// isAgentRunning checks if the agent is responding to health checks
func (x *TaskExecution) isAgentRunning(client *http.Client, agentURL string) bool {
    resp, err := client.Get(agentURL + "/static")
    if err != nil {
        return false
    }
    defer resp.Body.Close()
    return resp.StatusCode == http.StatusOK
}

// startAgent starts the Azure SRE agent
func (x *TaskExecution) startAgent(ctx context.Context, client *http.Client, agentURL string) error {
    fmt.Println("Starting Azure SRE agent...")

    agentPath := os.Getenv(dotnetAgentPathEnvVar)
    if agentPath == "" {
        return fmt.Errorf("please set the %s environment variable to the agent source directory", dotnetAgentPathEnvVar)
    }

    cmd := exec.Command("dotnet", "run",
        "--project", filepath.Join(agentPath, "src/Agent/Agent.Web/Agent.Web.csproj"),
        "--launch-profile", "https")
    cmd.Dir = agentPath

    if err := cmd.Start(); err != nil {
        return fmt.Errorf("starting agent: %w", err)
    }

    // Register cleanup function
    x.cleanupFunctions = append(x.cleanupFunctions, func() error {
        if cmd.Process != nil {
            return cmd.Process.Kill()
        }
        return nil
    })

    // Wait for agent to be ready
    return x.waitForAgent(client, agentURL)
}

// waitForAgent waits for the agent to become ready
func (x *TaskExecution) waitForAgent(client *http.Client, agentURL string) error {
    ticker := time.NewTicker(time.Second)
    defer ticker.Stop()

    timeout := time.After(agentStartupTimeout)

    for {
        select {
        case <-ticker.C:
            if x.isAgentRunning(client, agentURL) {
                fmt.Println("Agent started successfully")
                return nil
            }
        case <-timeout:
            return fmt.Errorf("agent failed to start after %v", agentStartupTimeout)
        }
    }
}

// processScriptStep handles a single script step
func (x *TaskExecution) processScriptStep(ctx context.Context, agentURL string) error {
    if x.task.Script == nil {
        return fmt.Errorf("no script step defined for task")
    }

    client := createHTTPClient(httpClientTimeout)

    prompt, err := x.substituteVariables(x.task.Script.Prompt)
    if err != nil {
        return fmt.Errorf("substituting variables: %w", err)
    }

    threadID, err := x.executeAgentRequest(client, agentURL, prompt)
    if err != nil {
        return fmt.Errorf("executing script step: %w", err)
    }

    // Store the thread ID
    x.threadID = threadID
    fmt.Printf("Stored thread ID %s for task %s\n", threadID, x.taskID)

    return nil
}

// executeAgentRequest sends a request to the agent and returns the thread ID
func (x *TaskExecution) executeAgentRequest(client *http.Client, agentURL, prompt string) (string, error) {
    fmt.Printf("Executing: %s\n", prompt)

    body := map[string]interface{}{
        "startMessage": map[string]interface{}{
            "text":        prompt,
            "userId":      "sreagent-eval",
            "displayName": "SRE Agent Eval",
        },
    }

    bodyJSON, err := json.Marshal(body)
    if err != nil {
        return "", fmt.Errorf("marshaling request: %w", err)
    }

    resp, err := client.Post(
        agentURL+"/api/v1/threads",
        "application/json",
        bytes.NewReader(bodyJSON),
    )
    if err != nil {
        return "", fmt.Errorf("sending request: %w", err)
    }
    defer resp.Body.Close()

    var result map[string]interface{}
    if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
        return "", fmt.Errorf("decoding response: %w", err)
    }

    x.logAgentResponse(result)

    // Extract thread ID from response
    threadID, ok := result["id"].(string)
    if !ok {
        return "", fmt.Errorf("no thread ID in response")
    }

    return threadID, nil
}

// logAgentResponse logs the agent's response
func (x *TaskExecution) logAgentResponse(result map[string]interface{}) {
    if x.log != nil {
        fmt.Fprintf(x.log, "Response: %v\n", result)
    }

    if toolOutput, ok := result["toolOutput"].(string); ok {
        output := fmt.Sprintf("Running: kubectl\n%s\n", toolOutput)
        fmt.Print(output)
        if x.log != nil {
            fmt.Fprint(x.log, output)
        }
    }
}

// getChatHistory retrieves the chat history for a thread
func (x *TaskExecution) getChatHistory(client *http.Client, agentURL, threadID string) ([]ThreadMessage, error) {
    fmt.Printf("Getting chat history for thread %s\n", threadID)

    resp, err := client.Get(agentURL + "/api/v1/threads/" + threadID + "/messages")
    if err != nil {
        return nil, fmt.Errorf("getting chat history: %w", err)
    }
    defer resp.Body.Close()

    if resp.StatusCode != http.StatusOK {
        body, _ := io.ReadAll(resp.Body)
        return nil, fmt.Errorf("unexpected status code %d: %s", resp.StatusCode, string(body))
    }

    var messagesResp ThreadMessagesResponse
    if err := json.NewDecoder(resp.Body).Decode(&messagesResp); err != nil {
        return nil, fmt.Errorf("decoding messages response: %w", err)
    }

    fmt.Printf("Retrieved %d messages from thread %s\n", len(messagesResp.Value), threadID)
    return messagesResp.Value, nil
}

// verifyWithLLM uses an LLM to verify the agent's response
func (x *TaskExecution) verifyWithLLM(client *http.Client, messages []ThreadMessage, verificationPrompt string) (bool, string) {
    // Extract the conversation history
    var conversation strings.Builder
    for _, msg := range messages {
        conversation.WriteString(fmt.Sprintf("[%s] %s: %s\n",
            msg.TimeStamp.Format("15:04:05"),
            msg.Author.DisplayName,
            msg.Text))
    }

    // Get LLM verifier
    llmVerifier := x.getLLMVerifier()
    if llmVerifier == nil {
        return false, "LLM verifier not configured"
    }

    return llmVerifier.Verify(conversation.String(), verificationPrompt)
}

// getLLMVerifier returns an LLM verifier instance
func (x *TaskExecution) getLLMVerifier() *LLMVerifier {
    apiKey := os.Getenv("LLM_API_KEY")
    endpoint := os.Getenv("LLM_ENDPOINT")

    if apiKey == "" || endpoint == "" {
        klog.Warning("LLM verification requested but LLM_API_KEY or LLM_ENDPOINT not set")
        return nil
    }

    return NewLLMVerifier(apiKey, endpoint)
}

// runVerificationScript executes the verification script
func (x *TaskExecution) runVerificationScript(ctx context.Context) model.TaskResult {
    var result model.TaskResult

    scriptPath, err := x.getScriptPath(x.task.Verifier, x.task.VerifierWindows, "verifier")
    if err != nil {
        result.Error = fmt.Sprintf("getting verification script: %v", err)
        return result
    }

    cmd := x.createScriptCommand(ctx, scriptPath)
    x.configureScriptEnvironment(cmd)

    fmt.Printf("\nRunning verifier script for task %s\n", x.taskID)

    err = x.runCommand(cmd)
    switch {
    case err == nil:
        result.Result = "success"
    case isExitError(err):
        result.Result = "fail"
    default:
        result.Error = fmt.Sprintf("verification error: %v", err)
    }

    return result
}

// runVerification executes the verification script and updates the result
func (x *TaskExecution) runVerification(ctx context.Context, result model.TaskResult) model.TaskResult {
    var scriptResult model.TaskResult
    var llmResult model.TaskResult

    // Check if we have LLM verification enabled
    hasLLMVerification := x.task.LLMVerification != nil && x.task.LLMVerification.Enabled

    // Run script verification if available
    if x.hasVerificationScript() {
        scriptResult = x.runVerificationScript(ctx)

        // If LLM verification is not enabled, return script result
        if !hasLLMVerification {
            return scriptResult
        }

        // If script failed and we should skip script verify, don't fail yet
        if scriptResult.Result != "success" && x.task.LLMVerification.SkipScriptVerify {
            fmt.Printf("Script verification failed but skipping due to skipScriptVerify=true\n")
        } else if scriptResult.Result != "success" {
            // Script failed and we're not skipping, return failure
            return scriptResult
        }
    }

    // Run LLM verification if enabled and we have a thread ID
    if hasLLMVerification && x.threadID != "" {
        fmt.Printf("\nRunning LLM verification for task %s\n", x.taskID)

        client := createHTTPClient(httpClientTimeout)
        agentURL := x.agentConfig.AgentURL
        if agentURL == "" {
            agentURL = defaultAgentURL
        }

        fmt.Printf("Using thread ID %s for LLM verification\n", x.threadID)

        messages, err := x.getChatHistory(client, agentURL, x.threadID)
        if err != nil {
            llmResult.Error = fmt.Sprintf("getting chat history: %v", err)
            llmResult.Result = "fail"
            return llmResult
        }

        // Run LLM verification
        passed, reason := x.verifyWithLLM(client, messages, x.task.LLMVerification.Prompt)
        if passed {
            llmResult.Result = "success"
        } else {
            llmResult.Result = "fail"
            llmResult.Error = reason
        }

        // Determine final result based on configuration
        if x.task.LLMVerification.SkipScriptVerify {
            // If skipScriptVerify is true, LLM result takes precedence
            return llmResult
        } else if x.hasVerificationScript() {
            // Both must pass
            if scriptResult.Result == "success" && llmResult.Result == "success" {
                return llmResult
            } else if scriptResult.Result != "success" {
                result.Result = "fail"
                result.Error = fmt.Sprintf("Script verification failed: %s", scriptResult.Error)
                return result
            } else {
                return llmResult
            }
        } else {
            // Only LLM verification
            return llmResult
        }
    }

    // If we only had script verification, return that result
    if x.hasVerificationScript() {
        return scriptResult
    }

    // No verification available
    klog.V(2).Info("No verification available", "task", x.taskID)
    return result
}

// hasVerificationScript checks if a verification script is defined
func (x *TaskExecution) hasVerificationScript() bool {
    return x.task.Verifier != "" || x.task.VerifierWindows != ""
}

// isExitError checks if an error is an exec.ExitError
func isExitError(err error) bool {
    var exitErr *exec.ExitError
    return errors.As(err, &exitErr)
}

// runCommand executes a command and logs its output
func (x *TaskExecution) runCommand(cmd *exec.Cmd) error {
    fmt.Printf("\nRunning command: %s\n", strings.Join(cmd.Args, " "))

    cmd.Stdout = os.Stdout
    cmd.Stderr = os.Stderr

    if x.log != nil {
        cmd.Stdout = io.MultiWriter(cmd.Stdout, x.log)
        cmd.Stderr = io.MultiWriter(cmd.Stderr, x.log)
    }

    if err := cmd.Run(); err != nil {
        return fmt.Errorf("command failed: %w", err)
    }

    return nil
}

// createHTTPClient creates an HTTP client with TLS verification disabled for localhost
func createHTTPClient(timeout time.Duration) *http.Client {
    return &http.Client{
        Timeout: timeout,
        Transport: &http.Transport{
            TLSClientConfig: &tls.Config{InsecureSkipVerify: true},
        },
    }
}

// printResults displays the evaluation results
func printResults(results []model.TaskResult) {
    fmt.Println("\nEvaluation Results:")
    fmt.Println("==================")

    for _, result := range results {
        fmt.Printf("\nTask: %s\n", result.Task)
        fmt.Printf("  Agent Config: %+v\n", result.AgentConfig)
        fmt.Printf("  Result: %s\n", result.Result)
        if result.Error != "" {
            fmt.Printf("  Error: %s\n", result.Error)
        }
    }
}