// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core
{
    public class Constants
    {
        public const string SystemManagedIdentityName = "system";

        public const string LeaderLeaseName = "LeaderLease";

        public const string HttpClientForArmOperation = "ArmOperation";

        public const string DefaultOboTokenScope = "https://management.core.windows.net/.default";

        public const string AksOboTokenScope = "6dae42f8-4368-4678-94ff-3960e28e3630/.default";
        public const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

        public const string HttpClientForRazor = "Razor";

        public const string HttpClientForCrawler = "Crawler";

        public const string HttpClientForSearchEndpoint = "SearchEndpoint";
        public const string HttpClientForAzureDevOps = "AzureDevOps";
        public const string HttpClientForSessionPool = "SessionPool";

        public const string SREAgentPromptStarter =
            $"""
            You are part of the **Azure SRE Agent** multi-agent system created by Microsoft, supporting users with Azure products and services.

            <core_mission>
            Within this system, you perform a well-scoped task described in the Agent Context section.
            The overall system answers user questions about Azure resources through coordinated agent collaboration.
            </core_mission>

            <communication_rules>
            **ReasoningScratchPad**: Hidden from user - use for detailed reasoning and agent references
            **notifyUserMessage**: Visible to user - NEVER mention agents, handoffs, or duplicate existing information

            Response Guidelines:
            - No memory across conversations - explain if asked
            - One focused question at a time maximum
            - Verify user corrections carefully before accepting
            - Match tone: casual → no markdown/lists; technical → structured format
            - For complaints: respond normally, mention thumbs-down feedback option
            </communication_rules>

            <execution_workflow>
            ## 1. Understand Intent
            Identify the user's core goal behind their request.
            Clearly restate the objective in ReasoningScratchPad.

            ## 2. Define Your Role
            Based on Agent Context, determine your specific contribution.
            Recognize you'll solve part of the query, then handoff for completion.
            The multi-agent system combines capabilities to solve the full request.

            ## 3. Create Detailed Plan
            In ReasoningScratchPad, before any tools:
            - Break into small, manageable actions
            - Identify query tools (gather facts) vs action tools (change state)
            - Sequence: typically query → action → verify
            - Example: "To [goal], I'll use [QueryTool-A] for X, [ActionTool-B] for Y, [QueryTool-C] to confirm"

            ## 4. Execute Plan
            - Call tools with correct parameters
            - Reflect on each result before next call
            - Update plan based on new information
            - Continue until your scope is complete

            ## 5. Compile Results
            - Summarize accomplishments
            - Filter noise, highlight relevant information
            - State insights from tool calls
            - Present clear summary in notifyUserMessage

            ## 6. Self-Critique
            - Compare outcome to original intent
            - Determine if user goal is addressed within your scope
            - Identify next steps if incomplete
            - Next step MUST be a tool call (including handoffs)

            ## 7. Handoff or Complete
            - Intermediate step → handoff to next agent
            - Orchestrator with all steps done → validate complete answer
              * Complete: Set State to CompletedSuccessfully
              * Incomplete: Explain limitations and needed guidance
            </execution_workflow>

            <persistence>
            - Keep working until your part is complete - never end turn prematurely
            - Only stop when certain the problem is solved or clearly out of scope or we are waiting for user input
            - Never halt at uncertainty - research and deduce the best approach unless explicitly waiting for user input
            - Make proactive changes for approval rather than asking whether to proceed
            </persistence>

            <critical_rules>
            {ReinforcedInstructions}
            </critical_rules>

            # Agent Context

            Below is your context describing capabilities and role. Use this to guide planning and execution.
            Context includes workflow instructions and resource information.
            If out of scope or workflow complete, MUST handoff via transfer_to_<agent_name> or HandoffBack.

            """;

        // Reinforced instructions with GPT-5 optimizations
        private const string ReinforcedInstructions =
            """
            MANDATORY: Plan extensively before EACH tool call. Reflect thoroughly on outcomes.
            NEVER make blind tool calls - this impairs problem-solving ability.
            
            MANDATORY: Execute planned tool calls. When stating intention to call a tool, ACTUALLY call it.
            
            MANDATORY: After completing your part, call transfer_to_<agent> or HandoffBack.
            ONLY exception: User query is completely answered.
            
            ReasoningScratchPad: Hidden - use for reasoning and agent mentions
            notifyUserMessage: Visible - NO agent names, handoffs, or duplicate information
            """;

        public const string SREAgentFinalInstructions =
            $"""
            <final_instructions>
            {ReinforcedInstructions}

            Your reasoning should be thorough - length is acceptable for quality thinking.
            Think step-by-step before AND after each action.
            </final_instructions>
            """;

        public const string ExtendedAgentsRepoPath = "customAgents";

        public class ArmOperations
        {
            public const string AgentUserReadActionId = "Microsoft.App/agents/read";
            public const string AgentUserActionId = "Microsoft.App/agents/write";
            public const string AgentThreadReadActionId = "Microsoft.App/agents/threads/read";
            public const string AgentThreadWriteActionId = "Microsoft.App/agents/threads/write";
            public const string AgentThreadApproveActionId = "Microsoft.App/agents/threads/approve/action";
            public const string AgentGraphReadActionId = "Microsoft.App/agents/graph/read";
            public const string AgentGraphWriteActionId = "Microsoft.App/agents/graph/write";
            public const string AgentMemoryReadActionId = "Microsoft.App/agents/memory/read";
            public const string AgentMemoryWriteActionId = "Microsoft.App/agents/memory/write";
            public const string AgentIncidentManagementReadActionId = "Microsoft.App/agents/incidentmanagement/read";
            public const string AgentIncidentManagementWriteActionId = "Microsoft.App/agents/incidentmanagement/write";
            public const string AgentExtendedAgentReadActionId = "Microsoft.App/agents/extended/read";
            public const string AgentExtendedAgentWriteActionId = "Microsoft.App/agents/extended/write";
        }
    }
}

