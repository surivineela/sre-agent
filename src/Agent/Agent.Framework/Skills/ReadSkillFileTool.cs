// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Framework.Skills;

/// <summary>
/// Tool for reading skill files to load domain-specific knowledge
/// </summary>
public class ReadSkillFileTool<TContext>(
    ISkillRegistry skillRegistry,
    Agent<TContext>? agent = null) : AIFunction where TContext : class
{
    public const string SkillNameParam = "skill_name";
    public const string FilePathParam = "file_path";

    #region AITool overrides

    public override string Name { get; } = ToolName;

    public override string Description => $"""
    <skills_instructions>
    <dynamic_toolset_notice>
    **Dynamic toolset**: Your available tools may be incomplete at the start of a request. Loading a skill can provide missing domain instructions and may add new tools.

    **When you suspect missing capability** (e.g., you cannot perform a requested action with current tools):
    1. Identify the most relevant skill from <available_skills>
    2. Read that skill's core instructions first: `read_skill_file(skill_name="...", file_path="SKILL.md")`
    3. Re-evaluate the request using the newly loaded instructions and any newly available tools

    Only after loading the most relevant skill(s) should you conclude an operation is not possible.
    </dynamic_toolset_notice>

    When users ask you to perform tasks, check if any of the available skills below can help complete the task more effectively.
    Skills help you gain domain-specific expertise and knowledge, and may also add additional tools for specialized operations.
    You MUST load and utilize the relevant skills to inform your actions and decisions. Do not attempt to answer domain-specific questions without first loading the appropriate skill(s).

    Skills contain detailed instructions, examples, best practices, and tools for specific domains.

    How to use skills effectively:
    1. **Start with SKILL.md**: ALWAYS begin by reading the main SKILL.md file from a skill. This file is guaranteed to exist and contains the primary skill instructions.
       Example: read_skill_file(skill_name="kubernetes_skill", file_path="SKILL.md")
       Result: (The content of SKILL.md)

    2. **Read referenced files**: The content of SKILL.md may reference additional files for detailed information for specific topics. Read these files as needed based on the task at hand.
       Example: If SKILL.md within `kubernetes_skill` mentions "troubleshooting.md", read it with:
       read_skill_file(skill_name="kubernetes_skill", file_path="troubleshooting.md")

    3. **Load skills progressively**: Only read what you need, when you need it
       - Start with SKILL.md to get the core expertise
       - Load detailed files that are referenced only as the task requires

    Tools provided by skills:
    - Skills may provide additional tools for specialized operations. After loading a skill for the first time with read_skill_file(skill_name="skill_name", file_path="SKILL.md"), new tools may be added to your tool set.
    - If a skill provides tools, the tool names will be listed along with the skill's metadata in the <available_skills> section below.
    - Use these tools as per the skill instructions to perform domain-specific tasks effectively.
    - Always refer back to the skill instructions when using these tools to ensure best practices are followed.
    - <example>
        User request: "Create pdf file with CPU metrics chart for resource X"
        Assistant: reasoning: (does not have any tools for generating pdf documents, but sees available skill which mentions pdf file generation, called 'document_generation')
        Assistant: read_skill_file(skill_name="document_generation", file_path="SKILL.md")
        Assistant: reasoning: (now has new tools 'ExecutePythonCode' and 'GeneratePdfReport' available)
        Assistant: <uses new tools per skill instructions to generate the pdf with charts>
      </example>

    When to use skills:
    - You need domain-specific expertise (e.g., "How do I diagnose PostgreSQL slow queries?")
    - The task requires specialized knowledge beyond general capabilities
    - You want to follow best practices for a specific technology or platform
    - You need examples of how to solve domain-specific problems

    **IMPORTANT**:
    - Only use skills listed in <available_skills> below
    - Do not attempt to read files that are not part of the skill set
    - Do not read metadata.yaml (this information is already provided in <available_skills>)
    - Do not read skill files more than once per task to avoid redundancy
    - Follow the instructions and best practices outlined in the skill files closely. If the skill outlines a specific workflow, adhere to it strictly.

    <blocked_handling>
    When you are blocked:
    - **Blocked by missing capability/tools**: Load the most relevant skill's `SKILL.md` immediately (do not stall by repeatedly saying you will proceed).
    - **Blocked by missing required user parameter**: Ask one focused question for the missing detail, then continue.
    - **Still blocked after skill load**: Explain what is missing (capability/permissions/tool) and offer the closest feasible alternative.
    </blocked_handling>
    </skills_instructions>

    <active_skills_guidelines>
    - You may continue to load skill files as needed throughout the conversation to gain additional expertise.
    - The first time you read 'SKILL.md' from a skill, that skill is now considered "active" and may provide additional tools.
    - You can have multiple active skills at the same time, but there is a limit to how many you can maintain concurrently (default is 5).
    - This limit is maintained by the system, and you will be informed of your currently active skills with a message like:
        "<system-reminder>These are your currently active skills: [skill1, skill2, skill3].</system-reminder>"
    - Your oldest active skills will be unloaded automatically when you exceed the limit.
    - Your active skills may also be reset if the conversation context is cleared or reset. In that case you will see a message like:
        "<system_notice> Your active skills have been cleared. You must re-evaluate which skills are currently needed (if any), and re-activate them by using the 'read_skill_file' tool.</system_notice>"
    - If a skill is active, you will have access to any tools it provides. If a skill is not active (not listed in the active skills reminder), you will not have access to its tools even if you have read its SKILL.md before. If you need to use this skill again, you must re-activate it by reading its SKILL.md file again.
    </active_skills_guidelines>

    <example_decision_flows>
    <note>
    These are examples of how to decide when to load skills based on user requests. Always analyze the user's specific request to determine the appropriate skills to load.
    Your specific available skills may differ; use this as a general guide.
    </note>
    <example_context>
    <sample_skills>
    - metrics_and_chart_visualization: Skill for analyzing and visualizing metrics with charts
    - scheduled_task: Skill for setting up recurring monitoring tasks
    - aks_general: Skill for general AKS operations
    - executing_code: Skill for executing code snippets
    </sample_skills>
    <decisions>
    - User: "Show CPU metrics for X" → Assistant: Load `metrics_and_chart_visualization` skill → Execute analysis
    - User: "Can you keep monitoring my resource foo?" → Assistant: Load `scheduled_task` skill for recurring monitoring setup
    - User: "AKS: list deployments" → Assistant: Load `aks_general` skill → Execute kubectl commands per skill guidance
    - User: "Generate a report with charts" → Assistant: Load `executing_code` skill → Use code execution and PDF generation tools as per skill instructions
    </decisions>
    </example_context>
    </example_decision_flows>

    <skill_file_structure>
    Each skill directory follows this pattern:
    ```
    Skills/
    └── <skill_name>/
        ├── SKILL.md              (Core domain expertise - read this first)
        ├── [additional].md       (Optional supplementary files)
        └── metadata.yaml         (Skill metadata - not for direct reading)
    ```
    </skill_file_structure>

    <available_skills>
    {skillRegistry.GetSkillsMetadataForPrompt(includeSystemSkills: agent?.AddSystemSkills ?? true)}
    </available_skills>
    """;

    #endregion

    #region AIFunction overrides

    public override JsonElement JsonSchema { get; } = InputJsonSchema;

    public override MethodInfo? UnderlyingMethod => typeof(ReadSkillFileTool<TContext>).GetMethod(nameof(ReadSkillFile));

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetValue(SkillNameParam, out var skillNameObj) || skillNameObj is null)
        {
            return new("Error: skill_name parameter is required");
        }

        if (!arguments.TryGetValue(FilePathParam, out var filePathObj) || filePathObj is null)
        {
            return new("Error: file_path parameter is required");
        }

        var skillName = skillNameObj.ToString() ?? string.Empty;
        var filePath = filePathObj.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(skillName) || string.IsNullOrEmpty(filePath))
        {
            return new("Error: skill_name and file_path cannot be empty");
        }

        var result = skillRegistry.ReadSkillFile(skillName, filePath, includeSystemSkills: agent?.AddSystemSkills ?? true);
        return new(result);
    }

    #endregion

    #region Statics

    public const string ToolName = "read_skill_file";
    public const string CommonPromptName = "skills";

    private static readonly JsonElement InputJsonSchema = AIJsonUtilities.CreateFunctionJsonSchema(
        method: typeof(ReadSkillFileTool<TContext>).GetMethod(nameof(ReadSkillFile))!);

    #endregion

    #region Schema Method

    [AgentTool(ToolMode.Auto, DisableOutputTruncation = true)]
    public static void ReadSkillFile(
        [Description("The name of the skill to read from (e.g., 'kubernetes_skill', 'postgresql_skill')")]
        [MinLength(1)]
        string skill_name,

        [Description("The relative path to the file within the skill directory. Always start with 'SKILL.md' to load core expertise.")]
        [MinLength(1)]
        string file_path)
    { }

    #endregion
}
