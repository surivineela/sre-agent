// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class ToDoWriteTool : AIToolFunction<ToDoWriteTool>
{
    #region AITool overrides

    public override string Name { get; } = ToolName;

    public override string Description { get; } = ToolDescription;

    #endregion

    #region AIFunction overrides

    public override JsonElement JsonSchema { get; } = InputJsonScema;

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetValue("todos", out var todosObj) || todosObj is null)
        {
            return new("No todos were provided. Nothing to update.");
        }

        string serializedTodos;

        if (todosObj is JsonElement element)
        {
            // Re-serialize to string directly
            serializedTodos = element.GetRawText();
        }
        else
        {
            // Handle case where it's already some enumerable/dictionary
            serializedTodos = JsonSerializer.Serialize(todosObj, AIJsonUtilities.DefaultOptions);
        }

        var sb = new StringBuilder();

        sb.Append("Todos have been modified successfully. ");
        sb.Append("Ensure that you continue to use the todo list to track your progress. ");
        sb.Append("Please proceed with the current tasks if applicable");

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("<system-reminder>");
        sb.AppendLine("Your todo list has changed. DO NOT mention this explicitly to the user. Here are the latest contents of your todo list:");
        sb.AppendLine();
        sb.Append(serializedTodos);
        sb.AppendLine(". Continue on with the tasks at hand if applicable.");
        sb.AppendLine("</system-reminder>");

        return new(sb.ToString());
    }

    #endregion

    #region Statics

    public const string ToolName = nameof(ToDoWrite);

    public const string ToolDescription =
    """
    Use this tool to create and manage a structured task list for your current SRE operations session. This helps you track progress, organize complex infrastructure tasks, and demonstrate thoroughness to the user.
    It also helps the user understand the progress of the task and overall progress of their requests.

    ## When to Use This Tool
    Use this tool proactively in these scenarios:

    1. Complex multi-step operations - When an operation requires 3 or more distinct steps or actions
    2. Non-trivial infrastructure tasks - Tasks that require careful planning or multiple system operations
    3. User explicitly requests todo list - When the user directly asks you to use the todo list
    4. User provides multiple tasks - When users provide a list of things to be done (numbered or comma-separated)
    5. After receiving new instructions - Immediately capture user requirements as todos
    6. When you start working on a task - Mark it as in_progress BEFORE beginning work. Ideally you should only have one todo as in_progress at a time
    7. After completing a task - Mark it as completed and add any new follow-up tasks discovered during implementation

    ## When NOT to Use This Tool

    Skip using this tool when:
    1. There is only a single, straightforward task
    2. The task is trivial and tracking it provides no organizational benefit
    3. The task can be completed in less than 3 trivial steps
    4. The task is purely conversational or informational

    NOTE that you should not use this tool if there is only one trivial task to do. In this case you are better off just doing the task directly.

    ## Examples of When to Use the Todo List

    <example>
    User: I want to set up monitoring and alerting for our production database. Make sure you test the alerts and verify the dashboards are working!
    Assistant: I'll help set up monitoring and alerting for your production database. Let me create a todo list to track this implementation.
    *Creates todo list with the following items:*
    1. Configuring database metrics collection (CPU, memory, disk I/O)
    2. Setting up Prometheus/CloudWatch metrics scraping
    3. Creating Grafana dashboards for database performance
    4. Implementing alerting rules for critical thresholds
    5. Testing alerts and verifying dashboard functionality
    *Begins working on the first task*

    <reasoning>
    The assistant used the todo list because:
    1. Setting up monitoring is a multi-step task requiring metrics, dashboards, and alerting configuration
    2. The user explicitly requested testing and verification afterward
    3. The assistant inferred that alerts and dashboards need to work properly by adding verification as the final task
    </reasoning>
    </example>

    <example>
    User: Help me rotate the SSL certificates across all our load balancers
    Assistant: Let me first inventory all load balancers and their current certificate status.
    *Uses CLI tools or APIs to locate all load balancers and check certificate expiration dates*
    Assistant: I've found 12 load balancers across 3 regions with certificates expiring in different timeframes. Let me create a todo list to track these rotations.
    *Creates todo list with specific items for each load balancer that needs certificate rotation*

    <reasoning>
    The assistant used the todo list because:
    1. First, the assistant inventoried to understand the scope of the task
    2. Upon finding multiple load balancers across different regions, it determined this was a complex task with multiple steps
    3. The todo list helps ensure every certificate is tracked and rotated systematically
    4. This approach prevents missing any certificates and maintains service security
    </reasoning>
    </example>

    <example>
    User: I need to implement these reliability improvements for our platform: auto-scaling policies, backup automation, disaster recovery plan, and incident response runbooks.
    Assistant: I'll help implement these reliability improvements. First, let's add all the improvements to the todo list.
    *Creates a todo list breaking down each improvement into specific tasks based on the infrastructure architecture*
    Assistant: Let's start with auto-scaling policies. This will involve analyzing current traffic patterns, defining scaling metrics, and configuring auto-scaling groups with appropriate thresholds.

    <reasoning>
    The assistant used the todo list because:
    1. The user provided multiple complex improvements to implement in a comma separated list
    2. The todo list helps organize these large improvements into manageable tasks
    3. This approach allows for tracking progress across the entire implementation
    </reasoning>
    </example>

    <example>
    User: Can you help investigate why our services are experiencing latency spikes? The p99 latency has been degrading over the past week.
    Assistant: I'll help investigate the latency issues affecting your services. First, let me examine your monitoring data and system metrics.
    *Reviews APM traces, system metrics, network patterns, and deployment history*
    Assistant: After analyzing your system metrics, I've identified several potential causes for the latency spikes. Let me create a todo list to track our investigation and remediation efforts.
    *Creates todo list with items like: 1) Analyzing database query performance during peak hours, 2) Investigating connection pool exhaustion in service mesh, 3) Reviewing recent deployment changes for performance regressions, 4) Checking for noisy neighbor issues on shared infrastructure, 5) Implementing distributed tracing for better visibility*
    Let's start by analyzing the database query performance during peak hours.

    <reasoning>
    The assistant used the todo list because:
    1. First, the assistant examined the monitoring data to identify specific issues
    2. Based on this analysis, it identified multiple investigation areas
    3. Performance investigation is a non-trivial task requiring multiple steps
    4. The todo list helps methodically track improvements across different system components
    5. This systematic approach ensures all potential causes are investigated
    </reasoning>
    </example>

    ## Examples of When NOT to Use the Todo List

    <example>
    User: How do I check the status of a Kubernetes pod?
    Assistant: You can check the status of a Kubernetes pod with this simple command:

    ```bash
    kubectl get pod <pod-name> -n <namespace>
    ```

    For more detailed information, you can use:
    ```bash
    kubectl describe pod <pod-name> -n <namespace>
    ```

    This will show you the pod's current state, events, and any error messages.

    <reasoning>
    The assistant did not use the todo list because this is a single, trivial task that can be completed in one step. There's no need to track multiple tasks or steps for such a straightforward request.
    </reasoning>
    </example>

    <example>
    User: What does a 502 Bad Gateway error mean?
    Assistant: A 502 Bad Gateway error indicates that a server acting as a gateway or proxy received an invalid response from the upstream server. This typically happens when a load balancer or reverse proxy can't reach the backend application server, often due to the backend being down, overloaded, or misconfigured. It's a server-side error that requires investigation of the proxy/gateway configuration and backend service health.

    <reasoning>
    The assistant did not use the todo list because this is an informational request with no actual operational task to complete. The user is simply asking for an explanation, not for the assistant to perform multiple steps or tasks.
    </reasoning>
    </example>

    <example>
    User: Can you restart the nginx service on prod-web-01?
    Assistant: Sure, let me restart the nginx service on prod-web-01.
    *Uses SSH or management tool to restart the nginx service*

    <reasoning>
    The assistant did not use the todo list because this is a single, straightforward task confined to one service on one server. Restarting a service doesn't require tracking multiple steps or systematic organization.
    </reasoning>
    </example>

    <example>
    User: Run terraform plan for me and tell me what changes will be applied.
    Assistant: I'll run the terraform plan command for you.

    *Executes: terraform plan*

    The command completed successfully. Here's the output:
    [Output of terraform plan command showing infrastructure changes]

    The plan shows 3 resources will be added, 2 will be modified, and no resources will be destroyed.

    <reasoning>
    The assistant did not use the todo list because this is a single command execution with immediate results. There are no multiple steps to track or organize, making the todo list unnecessary for this straightforward task.
    </reasoning>
    </example>

    ## Task States and Management

    1. **Task States**: Use these states to track progress:
       - pending: Task not yet started
       - in_progress: Currently working on (limit to ONE task at a time)
       - completed: Task finished successfully

       **IMPORTANT**: Task descriptions must have two forms:
       - content: The imperative form describing what needs to be done (e.g., "Run health checks", "Deploy to staging")
       - activeForm: The present continuous form shown during execution (e.g., "Running health checks", "Deploying to staging")

    2. **Task Management**:
       - Update task status in real-time as you work
       - Mark tasks complete IMMEDIATELY after finishing (don't batch completions)
       - Exactly ONE task must be in_progress at any time (not less, not more)
       - Complete current tasks before starting new ones
       - Remove tasks that are no longer relevant from the list entirely

    3. **Task Completion Requirements**:
       - ONLY mark a task as completed when you have FULLY accomplished it
       - If you encounter errors, blockers, or cannot finish, keep the task as in_progress
       - When blocked, create a new task describing what needs to be resolved
       - Never mark a task as completed if:
         - Health checks are failing
         - Implementation is partial
         - You encountered unresolved errors
         - You couldn't access necessary systems or credentials

    4. **Task Breakdown**:
       - Create specific, actionable items
       - Break complex operations into smaller, manageable steps
       - Use clear, descriptive task names
       - Always provide both forms:
         - content: "Fix database connection pool issue"
         - activeForm: "Fixing database connection pool issue"

    When in doubt, use this tool. Being proactive with task management demonstrates attentiveness and ensures you complete all requirements successfully.
    """;

    private static readonly JsonElement InputJsonScema = AIJsonUtilities.CreateFunctionJsonSchema(
        typeof(ToDoWriteTool).GetMethod(nameof(ToDoWrite))!,
        title: ToolName,
        description: ToolDescription);

    #endregion

    #region Models

    public static void ToDoWrite(
        [Description("The updated todo list")]
        List<TodoItem> todos)
    { }

    public enum TodoStatus
    {
        pending,
        in_progress,
        completed
    }

    public sealed record TodoItem(
        [property: MinLength(1)]
        string Content,
        TodoStatus Status,
        [property: MinLength(1)]
        string ActiveForm);

    #endregion
}
