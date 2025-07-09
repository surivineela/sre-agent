// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using ChatClientExtensions = Agent.Framework.ChatClientExtensions;

namespace Agent.Runtime.ThreadEvaluator;

/// <summary>
/// Scanner that periodically evaluates completed threads to assess their behavior and performance.
/// Filters threads based on configurable time windows:
/// - Evaluation history range: How far back to search for threads (default: 24 hours)
/// - Cool down period: Minimum time since last modification before evaluation (default: 30 minutes)
/// </summary>
public static class TrajectoryExtractor
{
    public static async Task<(string Trajectory, string PromptHash)> GenerateTrajectoryAsync(
        IChatClient chatClient,
        string chatTranscript,
        CancellationToken cancellationToken = default)
    {
        var modelInput = new List<ChatMessage>
        {
            new(ChatRole.System, TrajectoryExtractionPrompt),
            new(ChatRole.User, "<chat>\n" + chatTranscript + "\n</chat>")
        };

        (var resp, var _) = await ChatClientExtensions.GetResponseAsync(
            client: chatClient,
            messages: modelInput,
            outputType: typeof(TrajectoryOutput),
            options: new ChatOptions
            {
                ToolMode = ChatToolMode.None,
                Temperature = 0,
            },
            cancellationToken: cancellationToken);

        return (resp.Text, TrajectoryPromptHash);
    }

    private const string TrajectoryExtractionPrompt =
        """
        You are **Trajectory-Exractor**, a senior SRE who distils long agent chats
        into a compact investigation playbook.
        You MUST rely **only** on the messages provided between <chat> … </chat>.
        NO external knowledge. NO new facts. If data is missing, write "Unknown". Be concise.
        Generalise away low-level or sensitive detail (e.g., IPs, NSG rule text, full resource IDs). 
        Replace with placeholders like <NSG-Rule>, <ResourceID>, <IP-Addr>.  

        Think step-by-step using ReasoningScratchPad to capture your thoughts:

        1. Parse the user's final intent from the last user message.  
        2. Extract the *initial symptoms* explicity mentioned by the user.
        3. Skim the entire chat for key facts, decisions, tool calls, errors, fixes.  
        4. Map the **investigation steps** into a **decision tree** (tool used + branch logic). 
        5. Document the additional symptoms of problems that you observe.  
        6. Capture any system design or general knowledge explained in the chat
        7. Decide the most likely root cause & recommended next actions. Think step by step on why they are the most likely root cause or recommended next actions.  
        8. Document pitfalls and unlikely paths that the agent has explored and that the team should remember next time.  

        ### Few-shot example  (to lock the style)

        <chat>
        Role: user
        "Check the network connectivity between container app and redis"
        Role: assistant
        *… (handoff chatter & tool calls as in real logs) …*
        </chat>

        <expected>
        {
          "ReasoningScratchPad": "Quick skim of transcript:\n• Lines 3-6 – user shows timeout logs → record as initial symptom.\n• Lines 8-10 – assistant asks for VNets; confirm both resources in same VNet.\n\nStep mapping:\n1. az resource show (ln 11-14) → collected subnet IDs.\n2. az network watcher test-connectivity (ln 15-21) → timeout + “no external IP found” → flag NSG/quota branch.\n3. az network nsg rule list (ln 22-28) → rule <NSG-Rule> Deny 6379 TCP → likely culprit.\n4. az network list-usages (ln 29-32) → public-IP quota 60 % used → not blocking.\n\nObserved symptoms noted:\n• Redis inbound connections = 0 (ln 25)\n\nRoot-cause judgment:\n• Deny rule matches port 6379 exactly; no other blocks; quota fine → confident RCA.\n\nSystem design knowledge:\n• Only brief note that container app traffics privately to Redis; NSG sits on subnet.\n\nResources collected:\n• Container App ID, Redis Cache ID, NSG ID (ln 11-13, 22-23).\n• Resource types deduped to Container App; Azure Cache for Redis; Network Security Group.\n\nPitfalls captured:\n• Transcript warns no networking specialist agent; must rely on AzCli agent. Ensure every NSG rule enumerated.\n\nSelf-check: all output fields non-empty; initial vs observed symptoms distinct; root cause concise.",

          "Title": "Checking network connectivity between container app and Redis",

          "InitialSymptoms": "- Timeout logs in container-app",

          "StepsFollowed": "- az resource show – locate VNets/subnets for both resources\n- az network watcher test-connectivity – container-app → Redis:6379 – timeout; no external IP; quota exhaustion flagged\n- az network nsg rule list – inspect NSG rules – rule <NSG-Rule> blocks 6379\n- az network list-usages – check public-IP quota – within limits (not root cause)",

          "SymptomsObserved": "- Redis inbound connections = 0 during tests",

          "RootCause": "NSG rule blocks port 6380 between container app and Redis.",

          "SystemDesignKnowledge": "- Container app relies on private VNet peering\n- Redis expects inbound TCP 6379 Traffic constrained by subnet-level NSG.",

          "ResourcesInvolved": "/subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourcegroups/aca-sre-agent-demo-eastus/providers/microsoft.app/containerapps/iot-dashboard; /subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourceGroups/aca-sre-agent-demo-eastus/providers/Microsoft.Cache/Redis/iot-dashboard-redis-eastus; iot-dashboard-redis-eastus.redis.cache.windows.net",

          "ResourceTypesInvolved": "Container App; Azure Cache for Redis; Network Security Group",

          "Pitfalls": "- No networking specialist agent; must use AzCli agent for NSG inspection\n- Risk of overlooking blocking rule if each NSG rule not listed individually"
        }

        </expected>
        """;

    private const int HashBytesToKeep = 16;

    private static readonly string TrajectoryPromptHash = Convert.ToHexString(
        SHA256.HashData(
            Encoding.UTF8.GetBytes(TrajectoryExtractionPrompt)))
        .ToLowerInvariant()[..HashBytesToKeep];
}
