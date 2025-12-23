// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Framework.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class AgentToSkillService(
    IAgentFactory<AgentContext> agentFactory,
    IChatClientProvider chatClientProvider,
    ILogger<AgentToSkillService> logger
)
{
    public async Task<SkillSpec> ConvertAgentToSkillAsync(
        string agentName,
        IEnumerable<string> topLevelAgentSkills
    )
    {
        // Load the agent from the agent factory
        var agent = agentFactory.GetAgent(agentName);

        // generate skill description using LLM
        var description = await GenerateSkillDescriptionAsync(agent);

        // aggregate all tools used by the agent and its handoffs
        HashSet<string> toolSet = [];
        HashSet<string> visitedAgents = [];
        void CollectTools(Agent<AgentContext> currentAgent)
        {
            if (visitedAgents.Contains(currentAgent.Name))
            {
                return;
            }

            visitedAgents.Add(currentAgent.Name);

            foreach (var tool in currentAgent.FactoryTools)
            {
                toolSet.Add(tool);
            }

            foreach (var handoff in currentAgent.Handoffs)
            {
                if (handoff.AgentName.Equals("meta_agent", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInternalInformation("Skipping tool collection for handoff to meta_agent.");
                    continue;
                }

                if (topLevelAgentSkills.Contains(handoff.AgentName))
                {
                    logger.LogInternalInformation("Skipping tool collection for handoff to top-level skill agent {HandoffAgentName}.", handoff.AgentName);
                    continue;
                }

                var handoffAgent = agentFactory.GetAgent(handoff.AgentName);

                CollectTools(handoffAgent);
            }
        }
        CollectTools(agent);

        // remove certain tools
        toolSet.RemoveWhere(t =>
            t == "HandoffBack" ||
            t == "ToDoWrite" ||
            t == "NotifyUser");

        // create SKILL.md
        string skillMdContent = await GenerateSkillMdContentAsync(agent, topLevelAgentSkills);

        var result = new SkillSpec
        {
            Name = GetAgentToSkillName(agent.Name),
            Description = description,
            Tools = [.. toolSet],
            SkillMdContent = FormatFileContent(skillMdContent)
        };

        // create handoff markdown files
        // traverse agent tree to get all handoffs
        Queue<(string, Agent<AgentContext>)> handoffQueue = new();
        handoffQueue.Enqueue((skillMdContent, agent));

        visitedAgents.Clear();
        visitedAgents.Add(agent.Name);

        while (handoffQueue.Count > 0)
        {
            var (currentSkillMdContent, currentAgent) = handoffQueue.Dequeue();

            foreach (var handoff in currentAgent.Handoffs)
            {
                if (handoff.AgentName.Equals("meta_agent", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInternalInformation("Skipping handoff to meta_agent.");
                    continue;
                }

                if (visitedAgents.Contains(handoff.AgentName))
                {
                    logger.LogInternalInformation("Handoff agent {HandoffAgentName} already processed. Skipping.", handoff.AgentName);
                    continue;
                }

                if (topLevelAgentSkills.Contains(handoff.AgentName))
                {
                    logger.LogInternalInformation("Skipping handoff to top-level skill agent {HandoffAgentName}.", handoff.AgentName);
                    continue;
                }

                visitedAgents.Add(handoff.AgentName);

                var handoffAgent = agentFactory.GetAgent(handoff.AgentName);

                string handoffMdContent = await GenerateHandoffMdContentAsync(currentSkillMdContent, handoffAgent, topLevelAgentSkills);

                result.AdditionalFiles.Add(new SkillSubFile
                {
                    FilePath = $"{GetAgentToSkillName(handoffAgent.Name)}.md",
                    Content = FormatFileContent(handoffMdContent)
                });

                // Enqueue the handoff agent for further processing
                handoffQueue.Enqueue((handoffMdContent, handoffAgent));
            }
        }

        logger.LogInternalInformation("Converted agent {AgentName} to skill successfully.", agent.Name);

        return result;
    }

    public static string GetAgentToSkillName(string agentName)
    {
        if (agentName.EndsWith("_agent", StringComparison.OrdinalIgnoreCase))
        {
            agentName = agentName[..^"_agent".Length];
        }
        return agentName;
    }

    private static string FormatFileContent(string content)
    {
        var returned = new StringBuilder(content.Trim().ReplaceLineEndings());

        // Ensure it ends with a newline
        returned.AppendLine();

        return returned.ToString();
    }

    public async Task<string> GenerateSkillDescriptionAsync(
        Agent<AgentContext> agent)
    {
        var chatClient = chatClientProvider.SmallFastModel;

        List<ChatMessage> input = [
            new ChatMessage(ChatRole.System, AgentConversionSkillDescriptionPrompt),
            new ChatMessage(ChatRole.User, [
                new TextContent($"""
                Here is the agent definition to convert into a skill description:

                <agent_to_convert_definition>
                <name>
                {agent.Name}
                </name>
                <description>
                {agent.HandoffDescription}
                </description>
                <system_prompt>
                {agent.Instructions.GetOriginalText()}
                </system_prompt>
                </agent_to_convert_definition>
                """)
            ])
        ];

        var response = await chatClient.GetResponseAsync(input);
        return response.Text;
    }

    /// <summary>
    /// Generates the SKILL.md content for a skill based on the provided agent definition.
    /// </summary>
    /// <param name="agent">The agent definition to convert into SKILL.md content.</param>
    /// <param name="topLevelAgentSkills">The list of agent names that will / have already been converted into top-level skills.</param>
    /// <returns>Content of SKILL.md file</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<string> GenerateSkillMdContentAsync(
        Agent<AgentContext> agent,
        IEnumerable<string> topLevelAgentSkills)
    {
        var chatClient = chatClientProvider.ReasoningHeavyModel;

        // The agent's system prompt becomes the main content of SKILL.md.
        // We will use the LLM to help extract and organize the content, and to scrub any references to "handoffs" or "sub-agents".

        List<ChatMessage> input = [
            new ChatMessage(ChatRole.System, AgentConversionSkillMdPrompt),
            new ChatMessage(ChatRole.User, [

                new TextContent($"""
                Here is the agent's system prompt:

                <agent_to_convert_system_prompt>
                {agent.Instructions.GetOriginalText()}
                </agent_to_convert_system_prompt>
                """),

                new TextContent($"""
                Here are the agent's handoffs to other agents.
                These handoffs will be converted later into separate markdown files, and should be referenced in the SKILL.md file.
                The markdown files will be named "<agent_name>.md" where <agent_name> is the name of the handoff agent, minus the "_agent" suffix if present.
                For example, a handoff to "kubernetes_agent" becomes "kubernetes.md". You MUST create these references in the SKILL.md file.

                <agent_handoffs>
                {string.Join(Environment.NewLine, agent.Handoffs.Where(h => h.AgentName != "meta_agent" && !topLevelAgentSkills.Contains(h.AgentName)).Select(h =>
                    $"""
                    <handoff>
                      <agent_name>
                      {h.AgentName}
                      </agent_name>
                      <handoff_description>
                      {h.Description}
                      </handoff_description>
                    </handoff>
                    """))}
                </agent_handoffs>
                """),

                new TextContent($"""
                Here are the agent's handoffs to other agents that have already been converted into top-level skills.
                You must create references to these top-level skills in the SKILL.md file.
                The name of the skill will be the agent name minus the "_agent" suffix if present.
                For example, a handoff to "azure_cli_command_executor_agent" becomes the "azure_cli_command_executor" skill.
                <top_level_skill_handoffs>
                {string.Join(Environment.NewLine, agent.Handoffs.Where(h => topLevelAgentSkills.Contains(h.AgentName)).Select(h =>
                    $"""
                    <handoff>
                      <agent_name>
                      {h.AgentName}
                      </agent_name>
                      <handoff_description>
                      {h.Description}
                      </handoff_description>
                    </handoff>
                    """))}
                </top_level_skill_handoffs>
                """)

            ])
        ];

        var response = await chatClient.GetResponseAsync(input);

        return response.Text;
    }

    /// <summary>
    /// Generates the markdown content for a handoff agent based on the provided skill file content and handoff definition.
    /// </summary>
    /// <param name="originalMarkdownContent">The original markdown content of the skill file generated for the current agent</param>
    /// <param name="handoffAgent">The handoff agent to generate content for.</param>
    /// <param name="topLevelAgentSkills">The list of agent names that will / have already been converted into top-level skills.</param>
    /// <returns>The generated markdown content for the handoff agent.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<string> GenerateHandoffMdContentAsync(
        string originalMarkdownContent,
        Agent<AgentContext> handoffAgent,
        IEnumerable<string> topLevelAgentSkills)
    {
        var chatClient = chatClientProvider.ReasoningHeavyModel;

        List<ChatMessage> input = [
            new ChatMessage(ChatRole.System, AgentConversionHandoffMdPrompt),
            new ChatMessage(ChatRole.User, [

                new TextContent($"""
                Here is the content of the skill file that has already been generated:

                <skill_file_content>
                {originalMarkdownContent}
                </skill_file_content>
                """),

                new TextContent($"""
                Here is the handoff to convert:

                <handoff_to_convert>
                  <agent_name>
                  {handoffAgent.Name}
                  </agent_name>
                  <handoff_description>
                  {handoffAgent.HandoffDescription}
                  </handoff_description>
                  <agent_system_prompt>
                  {handoffAgent.Instructions.GetOriginalText()}
                  </agent_system_prompt>
                </handoff_to_convert>
                """),

                new TextContent($"""
                Here are the additional handoffs to other agents.
                These handoffs will be converted later into separate markdown files, and should be referenced in the the output file.
                The markdown files will be named "<agent_name>.md" where <agent_name> is the name of the handoff agent, minus the "_agent" suffix if present.
                For example, a handoff to "kubernetes_agent" becomes "kubernetes.md". You MUST create these references in the output file.

                <additional_handoffs>
                {string.Join(Environment.NewLine, handoffAgent.Handoffs.Where(h => h.AgentName != "meta_agent" && !topLevelAgentSkills.Contains(h.AgentName)).Select(h =>
                    $"""
                    <handoff>
                      <agent_name>
                      {h.AgentName}
                      </agent_name>
                      <handoff_description>
                      {h.Description}
                      </handoff_description>
                    </handoff>
                    """))}
                </additional_handoffs>
                """),


                new TextContent($"""
                Here are the handoff agent's handoffs to other agents that have already been converted into top-level skills.
                You must create references to these top-level skills in the output file.
                The name of the skill will be the agent name minus the "_agent" suffix if present.
                For example, a handoff to "azure_cli_command_executor_agent" becomes the "azure_cli_command_executor" skill.
                <top_level_skill_handoffs>
                {string.Join(Environment.NewLine, handoffAgent.Handoffs.Where(h => topLevelAgentSkills.Contains(h.AgentName)).Select(h =>
                    $"""
                    <handoff>
                      <agent_name>
                      {h.AgentName}
                      </agent_name>
                      <handoff_description>
                      {h.Description}
                      </handoff_description>
                    </handoff>
                    """))}
                </top_level_skill_handoffs>
                """)
            ])
        ];

        var response = await chatClient.GetResponseAsync(input);

        return response.Text;
    }

    private const string SkillsInformation = """
    <skills_information>
    ## Skills System Overview

    Skills provide specialized domain knowledge without requiring separate agents or handoffs. Instead of maintaining a complex agent graph, a single primary agent can load skills on-demand to gain expertise in specific domains.

    ## Skill Structure

    Each skill is a directory containing:

    ```text
    Skills/
    └── example_skill/
        ├── metadata.yaml   # Skill metadata (name, description, tools)
        ├── SKILL.md        # Core domain expertise (loaded first)
        ├── examples.md     # Optional: Examples and patterns
        ├── reference.md    # Optional: Reference documentation
        └── scenario.md     # Optional: Scenario-specific instructions
    ```

    ## metadata.yaml

    metadata.yaml contains the skill's metadata, including its name, description, and list of tools. The name and description are available in the primary agent's context always.

    ## SKILL.md

    When activating a skill, the agent will first make a tool call `read_skill_file(skill_name="kubernetes_skill", file_path="SKILL.md")`.
    SKILL.md is the main file that contains the core domain knowledge and instructions that gets injected into the agent's context when a skill is activated.
    The content of this file is returned by the tool call to add domain-specific knowledge to the agent's context.
    The content of SKILL.md may reference additional files for detailed information for specific topics.
    These additional files can be read as needed based on the task at hand.

    ## Other files

    Additional markdown files may be included to provide additional scenario-specific instructions, examples, or reference documentation. These files are not
    loaded by default, but can be read by the agent as needed using the `read_skill_file` tool. These additional files will be referenced in the SKILL.md file.
    </skills_information>
    """;

    private const string AgentFrameworkInformation = """
    <agent_framework_information>
    ## Agents Framework Overview

    The Agents Framework is a multi-agent system where each agent can hand off to other agents for specialized tasks based on the conversation context.

    ## Handoffs

    Each agent may have handoffs to other agents for specialized tasks. Handoffs are achieved by calling a handoff tool, generally named `transfer_to_<agent_name>` or `HandOffBack`.
    Handoffs can be multi-level, meaning an agent can hand off to another agent, which in turn can hand off to yet another agent, and so on.
    </agent_framework_information>
    """;

    private const string AgentConversionSkillDescriptionPrompt = $"""
    {SkillsInformation}

    <skill_description_instructions>
    You are an expert in AI agent frameworks and skill systems. Your task is to generate a detailed description of a skill based on the provided agent definition.
    The skill description should explain the purpose, capabilities, and usage of the skill in a way that is clear and informative.
    The description should be brief, concise, and focused. It should be a maximum of 5 sentences long.
    </skill_description_instructions>

    <output_format>
    Provide only the skill description as your output. Do not include any explanations or additional text.
    </output_format>
    """;

    private const string AgentConversionSkillMdPrompt = $"""
    <role>
    You are an expert in converting AI agent definitions into modular skill files. You will be provided with the system prompt and relevant details from an AI agent.
    Your task is to convert and organize this information into a skill file format.
    </role>

    {AgentFrameworkInformation}

    {SkillsInformation}

    <conversion_instructions>
    Your task is to convert the provided agent information into a skill file named SKILL.md.
    Guidelines:
    1. The agent's system prompt should become the main content of SKILL.md. Focus on extracting domain knowledge, instructions, best practices, and examples.
       The content should be worded as instructions. Remove any personality elements, such as "You are an expert on X...".
    2. Remove any references to "handoffs", "agents", "sub-agents", or multi-agent coordination. Skills should be self-contained and directly usable by the primary agent.
       Ignore any mentions of "meta_agent".
    3. The agent may have a list of handoffs to other agents. These sub-agent definitions will become other markdown files in the skill directory.
       You should use the handoff descriptions to create sections in SKILL.md that reference these files for further details. The file that replaces each handoff will be named "<agent_name>.md"
       where <agent_name> is the name of the handoff agent, minus the "_agent" suffix if present. For example, a handoff to "kubernetes_agent" becomes "kubernetes.md".
       Create one section for every handoff you convert. Include information from the handoff description to explain when and how to read the additional file.
    4. The agent may also have a list of handoffs to other agents that have been converted into top-level skills.
       You should create sections in SKILL.md that reference these top-level skills for further details. The name of the skill will be the agent name minus the "_agent" suffix if present.
       For example, a handoff to "azure_cli_command_executor_agent" becomes the "azure_cli_command_executor" skill.
       Create one section for every top-level skill handoff. Include information from the handoff description to explain when and how to use the top-level skill.
    5. Organize the content into clear sections, such as:
       - Overview/Description
       - Capabilities
       - Instructions/Best Practices
       - Examples
       - Additional Resources
       You do not need to include all these sections, only those relevant to the content.
    </conversion_instructions>

    <output_format>
    Provide only the content of SKILL.md as your output. Do not include any explanations or additional text.
    Provide your response in raw markdown using proper Github-flavored syntax. Different sections should use proper markdown headers, with different levels as necessary (#, ##, ###).
    Format file names in your output as markdown links. For example, use `[kubernetes.md](kubernetes.md)` to reference the file `kubernetes.md`. Assume all files are in the same directory.
    </output_format>
    """;

    private const string AgentConversionHandoffMdPrompt = $"""
    <role>
    You are an expert in converting AI agent definitions into modular skill files. You will be provided with the details skill file that has already been generated for an AI agent.
    The original agent was previously able to 'hand off' to other AI agents for specialized tasks, and you will need to create separate markdown files for these handoffs.
    Your task is to analyze the skill file content and the provided handoff definition, and create a separate detailed markdown file for a handoff that was defined in the original agent.
    The original markdown file content you are provided with will mention the file you are tasked with generating.
    </role>

    {AgentFrameworkInformation}

    {SkillsInformation}

    <conversion_instructions>
    Your task is to convert the provided handoff information into a separate markdown file named "<agent_name>.md" where <agent_name> is the name of the handoff agent, minus the "_agent" suffix if present.
    For example, a handoff to "kubernetes_agent" becomes "kubernetes.md".
    Guidelines:
    1. Focus on extracting domain knowledge, instructions, best practices, and examples relevant to the handoff agent.
       The content should be worded as instructions. Remove any personality elements, such as "You are an expert on X...".
    2. Remove any references to "handoffs", "agents", "sub-agents", or multi-agent coordination. Skills should be self-contained and directly usable by the primary agent.
       Ignore any mentions of "meta_agent".
    3. The handoff agent may have a list of further handoffs to other agents. These sub-agent definitions will become other markdown files in the skill directory.
       You should use the handoff descriptions to create sections in SKILL.md that reference these files for further details. The file that replaces each handoff will be named "<agent_name>.md"
       where <agent_name> is the name of the handoff agent, minus the "_agent" suffix if present. For example, a handoff to "kubernetes_agent" becomes "kubernetes.md".
       Create one section for every handoff you convert. Include information from the handoff description to explain when and how to read the additional file.
    4. The handoff agent may also have a list of further handoffs to other agents that have been converted into top-level skills.
       You should create sections in SKILL.md that reference these top-level skills for further details. The name of the skill will be the agent name minus the "_agent" suffix if present.
       For example, a handoff to "azure_cli_command_executor_agent" becomes the "azure_cli_command_executor" skill.
       Create one section for every top-level skill handoff. Include information from the handoff description to explain when and how to use the top-level skill.
    5. Organize the content into clear sections, such as:
       - Overview/Description
       - Capabilities
       - Instructions/Best Practices
       - Examples
       You do not need to include all these sections, only those relevant to the content.
    </conversion_instructions>

    <output_format>
    Provide only the content of the handoff markdown file as your output. Do not include any explanations or additional text.
    Provide your response in raw markdown using proper Github-flavored syntax. Different sections should use proper markdown headers, with different levels as necessary (#, ##, ###).
    Format file names in your output as markdown links. For example, use `[kubernetes.md](kubernetes.md)` to reference the file `kubernetes.md`. Assume all files are in the same directory.
    </output_format>
    """;
}
