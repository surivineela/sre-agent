// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using Agent.Framework;
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
        IEnumerable<ChatMessage> chatMessages,
        CancellationToken cancellationToken = default)
    {
        var chatTrajectory = new Trajectory();

        foreach (var msg in chatMessages)
        {
            chatTrajectory.Append(msg);
        }

        var chatTranscript = chatTrajectory.GetFullTrajectory();

        var modelInput = new List<ChatMessage>
        {
            new(ChatRole.System, TrajectoryExtractionPrompt),
            new(ChatRole.User, "<chat>\n" + chatTranscript + "\n</chat>")
        };

        (var extractedTrajectory, var _) = await ChatClientExtensions.GetResponseAsync(
            client: chatClient,
            messages: modelInput,
            outputType: typeof(TrajectoryOutput),
            options: new ChatOptions
            {
                ToolMode = ChatToolMode.None,
                Temperature = 0,
            },
            cancellationToken: cancellationToken);

        return (extractedTrajectory.Text, LkgPromptHash);
    }

    private const string TrajectoryExtractionPrompt =
        """
        You are **Trajectory-Extractor**, a senior SRE who distills long agent chats
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

        ### More guidance how to generate StepsFollowed

        It should be end-to-end decision flow for the investigation, written as a **linear or lightly branched playbook** that another agent can follow.
        It must be presented as a numbered list.

        • One line per step, formatted:
            "<action> - <tool> – <expected signal / observation placeholder>"

          – *Tool*  → the interface to use (kubectl, az, kusto, ping, curl…).  
          – *Medium-grain action*  
              · Specific enough to be executable (“list NSG rules”, “run test-connectivity”),  
                but NOT tied to a single instance name or ID unless essential.  
              · Avoid vague verbs like “investigate networking” (too broad) and
                over-specific commands like “check inbound rule port 6379 on
                vnet-prod-westus-subnet-app” (too narrow).  
          – *Expected signal* → what to look for; keeps the agent outcome-oriented.

        • Keep nouns generic where possible (“target Redis cache”, “app subnet NSG”),
          letting the executing agent substitute actual resource identifiers.

        • Omit chatter, acknowledgments, or user guidance - only actionable steps.

        • Preserve chronological order; if you collapse minor retries, ensure the
          logical flow still reads coherently.

        • If the plan forks, show the **condition** that dictates the branch right in
          the line (e.g. “if test-connectivity fails → …”).

        The resulting plan should let a peer agent replay or simulate the
        investigation with minimal additional context.

        ### Few-shot example  (to lock the style)

        <chat>
        Role: user
        "Check the network connectivity between container app and redis"
        Role: assistant
        *… (handoff chatter & tool calls as in real logs) …*
        </chat>

        Expected Output:
        {
          "ReasoningScratchPad": "Quick skim of transcript:\n• Lines 3-6 – user shows timeout logs → record as initial symptom.\n• Lines 8-10 – assistant asks for VNets; confirm both resources in same VNet.\n\nStep mapping:\n1. az resource show (ln 11-14) → collected subnet IDs.\n2. az network watcher test-connectivity (ln 15-21) → timeout + “no external IP found” → flag NSG/quota branch.\n3. az network nsg rule list (ln 22-28) → rule <NSG-Rule> Deny 6379 TCP → likely culprit.\n4. az network list-usages (ln 29-32) → public-IP quota 60 % used → not blocking.\n\nObserved symptoms noted:\n• Redis inbound connections = 0 (ln 25)\n\nRoot-cause judgment:\n• Deny rule matches port 6379 exactly; no other blocks; quota fine → confident RCA.\n\nSystem design knowledge:\n• Only brief note that container app traffics privately to Redis; NSG sits on subnet.\n\nResources collected:\n• Container App ID, Redis Cache ID, NSG ID (ln 11-13, 22-23).\n• Resource types deduped to Container App; Azure Cache for Redis; Network Security Group.\n\nPitfalls captured:\n• Transcript warns no networking specialist agent; must rely on AzCli agent. Ensure every NSG rule enumerated.\n\nSelf-check: all output fields non-empty; initial vs observed symptoms distinct; root cause concise.",

          "Title": "Checking network connectivity between container app and Redis",

          "InitialSymptoms": "- Timeout logs in container-app",

          "StepsFollowed": "
          1. Locate network context - az resource show – source and target subnet/VNet IDs obtained
          2. Verify VNet connectivity - az network vnet peering list – peering state = Connected
          3. Test reachability - az network watcher test-connectivity – verdict = Reachable → if verdict = Unreachable → go to step 4
          4. List security rules - az network nsg rule list – Deny rule for TCP 6379 present
          5. Show effective routes - az network nic show-effective-route-table – black-hole or UDR to NVA detected
          6. Check service firewall - az redis firewall-rules list – source subnet CIDR allowed
          7. Verify DNS resolution - nslookup target Redis cache – private IP resolves
          8. Assess platform quotas - az network list-usages – usage below limit (no exhaustion)
          9. Retest reachability post-fix - az network watcher test-connectivity – verdict = Reachable with acceptable latency",

          "SymptomsObserved": "- Redis inbound connections = 0 during tests",

          "RootCause": "NSG rule blocks port 6380 between container app and Redis.",

          "SystemDesignKnowledge": "- Container app relies on private VNet peering\n- Redis expects inbound TCP 6379 Traffic constrained by subnet-level NSG.",

          "ResourcesInvolved": "/subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourcegroups/aca-sre-agent-demo-eastus/providers/microsoft.app/containerapps/iot-dashboard; /subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourceGroups/aca-sre-agent-demo-eastus/providers/Microsoft.Cache/Redis/iot-dashboard-redis-eastus; iot-dashboard-redis-eastus.redis.cache.windows.net",

          "ResourceTypesInvolved": "Container App; Azure Cache for Redis; Network Security Group",

          "Pitfalls": "- No networking specialist agent; must use AzCli agent for NSG inspection\n- Risk of overlooking blocking rule if each NSG rule not listed individually"
        }
        """;

    private const int HashBytesToKeep = 16;

    private static readonly string TrajectoryPromptHash = Convert.ToHexString(
        SHA256.HashData(
            Encoding.UTF8.GetBytes(TrajectoryExtractionPrompt)))
        .ToLowerInvariant()[..HashBytesToKeep];

    private const string PreviousPromptHash = "307a921f76161949";

    // only update this if significant changes made to the prompt. Otherwise keep to PreviousPromptHash
    public static readonly string LkgPromptHash = TrajectoryPromptHash;
}
