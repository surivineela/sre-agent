// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public static class Summarizer
{
    public static async Task<string> SummarizeUserTrajectoryAsync(
       IChatClient chatClient,
       IReadOnlyList<ChatMessage> userTrajectory)
    {
        var lastUserMessageIndex = -1;
        var curIndex = 0;
        foreach (var message in userTrajectory)
        {
            if (message.Role == ChatRole.User)
            {
                lastUserMessageIndex = curIndex;
            }
            curIndex++;
        }

        if (lastUserMessageIndex == -1)
        {
            return "No user messages to summarize.";
        }
        else if (lastUserMessageIndex == 0)
        {
            return ExtractUserQuestion(userTrajectory[0].Text);
        }

        var sb = new StringBuilder();
        var messages = userTrajectory
            .Take(lastUserMessageIndex + 1)
            .Where(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant)
            .SelectMany(m => m.Contents.OfType<TextContent>()
                .Where(c => !c.Text.Contains("overall_assessment", StringComparison.OrdinalIgnoreCase))
                .Select(c => (m.Role, c.Text)))
            .ToList();

        foreach (var message in messages)
        {
            sb.AppendLine($"Role: {message.Role}");

            if (message.Role == ChatRole.Assistant)
            {
                sb.AppendLine(ExtractNotifyUserMessage(message.Text));
                sb.AppendLine();
            }
            else if (message.Role == ChatRole.User)
            {
                sb.AppendLine(ExtractUserQuestion(message.Text));
                sb.AppendLine();
            }
        }

        var conversation = sb.ToString();
        var conversationMessage = $"Following is the conversation whose intent you need to extract:\n\n{conversation}";

        var summarizeMessages = new List<ChatMessage>
        {
            new(ChatRole.System,UserTrajectorySummarizerPrompt),
            new(ChatRole.User, conversationMessage)
        };

        var response = await chatClient.GetResponseAsync(summarizeMessages, new ChatOptions
        {
            Temperature = 0.0f,
            ToolMode = ChatToolMode.None,
            ResponseFormat = ChatResponseFormat.Text,
        });

        return response.Text;
    }

    public static string ExtractUserQuestion(string text)
    {
        return text.IndexOf(Markers.UserQuestionMarker, StringComparison.OrdinalIgnoreCase) is var i && i >= 0
            ? text[(i + Markers.UserQuestionMarker.Length)..].Trim()
            : text;
    }

    public static async Task<string> CompactChatHistoryAsync(
        string additionalInstructions,
        IEnumerable<ChatMessage> chatHistory,
        string startingAgent,
        bool autoHandOffEnabled,
        IChatClient chatClient)
    {
        // build compaction system prompt
        var compactPrompt = TrajectoryCompactionPrompt;
        if (!string.IsNullOrEmpty(additionalInstructions))
        {
            var compactPromptBuilder = new StringBuilder(TrajectoryCompactionPrompt);
            compactPromptBuilder.AppendLine();
            compactPromptBuilder.AppendLine(AdditionalInstructionsPrompt);
            compactPromptBuilder.AppendLine();
            compactPromptBuilder.AppendLine(additionalInstructions);
            compactPrompt = compactPromptBuilder.ToString();
        }

        // build chat trajectory
        var chatTrajectory = new AgentTrajectory(startingAgent, autoHandOffEnabled);
        foreach (var msg in chatHistory)
        {
            chatTrajectory.Append(msg);
        }
        var chatTranscript = chatTrajectory.GetFullTrajectory()
            .Replace("\r\n", "\n");

        var summarizerChat = new List<ChatMessage>
        {
            new(ChatRole.System, compactPrompt),
            new(ChatRole.User, chatTranscript),
        };

        var summarizerChatOptions = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0,
            ResponseFormat = ChatResponseFormat.Text,
        };

        var chatSummary = await chatClient.GetResponseAsync(summarizerChat, summarizerChatOptions);

        var responseBuilder = new StringBuilder(ChatSummaryMarker);
        responseBuilder.AppendLine();
        responseBuilder.AppendLine(chatSummary.Text);

        return responseBuilder.ToString();
    }

    public static string ExtractNotifyUserMessage(string contentText)
    {
        var extractedText = contentText;

        try
        {
            var op = JsonSerializer.Deserialize<Dictionary<string, string>>(contentText);
            if (op is not null
                && op.TryGetValue("notifyUserMessage", out var notifyText))
            {
                return notifyText;
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (ex is JsonException && ex.Message.Contains("Expected end of data"))
                {
                    //Detected multiple JSON objects. Attempting to parse individually

                    // Split using regex to find boundaries between JSON objects
                    var segments = Regex.Split(contentText, @"(?<=})\s*(?={)")
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .ToList();

                    foreach (var json in segments)
                    {
                        try
                        {
                            var op = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                            if (op != null && op.TryGetValue("notifyUserMessage", out var notifyText))
                            {
                                // take the first one
                                return notifyText;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        return extractedText;
    }

    public const string ChatSummaryMarker = "This session is being continued from a previous conversation that ran out of context. The conversation is summarized below:";

    private const string TrajectoryCompactionPrompt = """
    You are a principal reliability engineer with expertise in Azure infrastructure, incident management, and creating detailed operational handover documentation that enables seamless work continuity.
    Your task is to create a detailed summary of the conversation so far, paying close attention to the user's explicit requests and your previous actions.
    This summary should be thorough in capturing technical details, infrastructure configurations, operational procedures, and reliability decisions that would be essential for continuing operations work without losing context.

    Before providing your final summary, wrap your analysis in <analysis> tags to organize your thoughts and ensure you've covered all necessary points. In your analysis process:

    1. Chronologically analyze each message and section of the conversation. For each section thoroughly identify:
       - The user's explicit requests and intents
       - Your approach to addressing the user's operational requests
       - Key decisions, technical concepts and infrastructure patterns
       - Specific details like:
         - Azure resource names and types
         - full configuration snippets
         - command outputs
         - metrics and thresholds
         - resource specifications
         - service names and endpoints
       - Incidents or outages you investigated and how you resolved them
       - Pay special attention to specific user feedback that you received, especially if the user told you to approach something differently.
    2. Double-check for technical accuracy and completeness, addressing each required element thoroughly.

    Your summary should include the following sections:

    1. Primary Request and Intent: Capture all of the user's explicit requests and intents in detail (e.g., incident investigation, deployment, security audit, performance optimization)
    2. Key Technical Concepts: List all important technical concepts, technologies, platforms, and tools discussed (e.g., Kubernetes, Terraform, monitoring systems, cloud providers).
    3. Resources and Configurations: Enumerate specific Azure resources (with their exact names and types) that were examined, modified, or created. Pay special attention to the most recent messages and include full configuration snippets where applicable and include a summary of why this resource or configuration change is important.
    4. Incidents and Resolutions: List all incidents, outages, or issues that you investigated, and how you resolved them. Include specific metrics, error messages, and root causes. Pay special attention to specific user feedback about incident handling or different approaches they requested.
    5. Problem Solving: Document operational problems solved, performance improvements made, and any ongoing troubleshooting efforts.
    6. All user messages: List ALL user messages that are not tool results. These are critical for understanding the users' feedback and changing intent.
    7. Pending Tasks: Outline any pending operational tasks that you have explicitly been asked to work on (e.g., scheduled maintenance, pending deployments, unresolved incidents).
    8. Current Work: Describe in detail precisely what was being worked on immediately before this summary request, paying special attention to the most recent messages from both user and assistant. Include resource names, configurations, command outputs, and system states where applicable.
    9. Optional Next Step: List the next step that you will take that is related to the most recent work you were doing. IMPORTANT: ensure that this step is DIRECTLY in line with the user's explicit requests, and the task you were working on immediately before this summary request. If your last task was concluded, then only list next steps if they are explicitly in line with the users request. Do not start on tangential requests without confirming with the user first.
    If there is a next step, include direct quotes from the most recent conversation showing exactly what task you were working on and where you left off. This should be verbatim to ensure there's no drift in task interpretation.

    Here's an example of how your output should be structured:

    <example>
    <analysis>
    [Your thought process, ensuring all points are covered thoroughly and accurately]
    </analysis>

    <summary>
    1. Primary Request and Intent:
       [Detailed description of operational requests - e.g., "User requested investigation of high latency in production API endpoints and implementation of auto-scaling policies"]

    2. Key Technical Concepts:
       - [Kubernetes horizontal pod autoscaling]
       - [Prometheus metrics and alerting]
       - [Load balancer configuration]
       - [...]

    3. Resources and Configurations:
       - [Azure SQL Database: prod-api-db-eastus]
          - [Type: Azure SQL Database - Standard S3 tier]
          - [Summary of why this database resource is important]
          - [Summary of configuration changes - increased DTUs from 100 to 200]
          - [Important Configuration Snippet]
       - [Application Gateway: prod-app-gateway-01]
          - [Type: Application Gateway v2 - WAF_v2 SKU]
          - [Summary of routing rule changes and health probe configuration]
          - [Important Configuration Snippet]
       - [Virtual Machine Scale Set: api-vmss-prod]
          - [Type: VMSS with Standard_D4s_v3 instances]
          - [Auto-scaling configuration updates]
       - [...]

    4. Incidents and Resolutions:
        - [Database connection pool exhaustion at 14:30 UTC]:
          - [Increased pool size from 50 to 200 connections]
          - [Added connection timeout monitoring]
          - [User feedback: requested gradual rollout instead of immediate change]
        - [Memory leak in service-auth pod]:
          - [Identified root cause through heap dumps]
          - [Applied memory limits and implemented pod restart policy]
        - [...]

    5. Problem Solving:
       [Description of resolved operational issues, performance optimizations, and ongoing investigations]

    6. All user messages:
        - [Detailed non tool use user message]
        - [...]

    7. Pending Tasks:
       - [Deploy updated monitoring dashboard to production]
       - [Schedule maintenance window for database upgrade]
       - [Review and update disaster recovery runbooks]
       - [...]

    8. Current Work:
       [Precise description of current operational work - e.g., "Currently implementing rate limiting on API gateway to mitigate ongoing traffic spike"]

    9. Optional Next Step:
       [Optional Next step to take - e.g., "Deploy rate limiting configuration to remaining API endpoints as per user request"]

    </summary>
    </example>

    Additional context-specific examples to consider when summarizing:

    <example>
    ## Incident Investigation Summary
    When summarizing incident investigations, focus on:
    - Timeline of events
    - Metrics and logs analyzed
    - Root cause analysis findings
    - Temporary mitigations applied
    - Permanent fixes implemented
    - Post-incident action items
    </example>

    <example>
    ## Deployment and Change Management Summary
    When summarizing deployments and changes, include:
    - Pre-deployment health checks
    - Configuration changes made
    - Rollout strategy used
    - Validation steps performed
    - Rollback procedures prepared
    - Any issues encountered during deployment
    </example>

    <example>
    ## Security and Compliance Review Summary
    When summarizing security reviews, capture:
    - Security vulnerabilities identified
    - Compliance requirements checked
    - Access control changes
    - Security patches applied
    - Audit logs reviewed
    - Remediation steps taken
    </example>

    <example>
    ## Performance Optimization Summary
    When summarizing performance work, document:
    - Baseline metrics captured
    - Bottlenecks identified
    - Optimizations implemented
    - Resource adjustments made
    - Performance improvements measured
    - Monitoring enhancements added
    </example>

    Please provide your summary based on the conversation so far, following this structure and ensuring precision and thoroughness in your response.
    """;

    private const string AdditionalInstructionsPrompt =
    """
    There may be additional summarization instructions provided in the included context. If so, remember to follow these instructions when creating the above summary.

    ## Additional Instructions:
    When summarizing the conversation:
    """;

    private const string UserTrajectorySummarizerPrompt =
    """
    You are an expert conversation analyst.

    Goal
    Summarize** only the user's current intent** in an ongoing, multi-turn chat between a User and an Assistant.
    - Ignore earlier intents that have been fulfilled, abandoned, or superseded.
    - Include any refinements or follow-ups that the user is still pursuing.
    - Use earlier dialogue only to resolve pronouns or context needed to state the latest intent clearly.
    - Express the intent in one concise sentence (≤ 25 words) that starts with a verb(e.g., “Get status of …”, “Show 5xx error trend …”, etc.).

    Example:
    Role: User
    What all apps do you manage?
     → Intent: Get the list of managed apps.

    Role: Assistant
    App A, App B, Cosmos DB.

    Role: User
    What is status of App A?
     → Intent: Get the current status of App A.

    Role: Assistant
    App A is healthy.Which metric would you like to see?

    Role: User
    Show me 5xx errors.
     → Intent: Show the 5xx-error metric for App A.

    Role: Assistant
    (hands back a 5xx-error chart)

    Role: User
    How about Cosmos?
     → Intent: Get the status/metrics for Cosmos DB.
    """;
}
