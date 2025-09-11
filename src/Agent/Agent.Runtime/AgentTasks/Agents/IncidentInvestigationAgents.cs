// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.AgentTasks.Handlers;

namespace Agent.Runtime.AgentTasks.Agents;

public record InitialInvestigationResult
{
    [Description("Steps taken during the investigation")]
    public required IList<InitialInvestigationStepResult> ContextGatheringSteps { get; set; }

    [Description("Detailed summary of the initial investigation and the relevant context that has been gathered, in markdown format")]
    public required string Summary { get; set; }
}

public record InitialInvestigationStepResult
{
    [Description("Brief title (6-10 words maximum) of the step taken.")]
    public required string Title { get; set; }

    [Description("Summary of the this individual step in markdown format")]
    public required string Summary { get; set; }
}

public record HypothesisGenerationResult
{
    [Description("The title of the hypothesis. This should be a brief title (6-10 words maximum) that describes the hypothesis.")]
    public required string Title { get; set; }

    [Description("The detailed content of the hypothesis. This should be a more detailed description of the hypothesis, including the reasoning behind the hypothesis.")]
    public required string Content { get; set; }
}

public enum HypothesisValidationStatus
{
    Validated,
    Invalidated,
    Inconclusive
}

public record HypothesisValidationResult
{
    [Description(
    """
    The status of the hypothesis validation. Use this field to indicate whether the hypothesis is validated, invalidated, or inconclusive.
    """
    )]
    public required HypothesisValidationStatus Status { get; set; }

    [Description("The steps taken to validate the hypothesis. This should be a list of detailed steps that were taken while attempting to validate the hypothesis.")]
    public required IList<HypothesisStep> Steps { get; set; }

    // TODO: isRootCause functionality removed
    [Description("Whether the hypothesis is a root cause of the incident. Use this field to indicate whether the hypothesis is a root cause of the incident. " +
        "This should only be 'true' if you have validated this hypothesis and there is substantial evidence to support this item being the root cause.")]
    public required bool IsRootCause { get; set; }
}

public record HypothesisValidationPlanStep
{
    [Description("Title of the step you plan to perform to validate a hypothesis. User-friendly description. 6-10 words maximum.")]
    public required string Title { get; set; }

    [Description("Detailed description of the step you will take. User-friendly description.")]
    public required string Description { get; set; }
}

public record HypothesisValidationPlanOutput
{
    [Description("List of steps you plan to take in order to validate the provided hypothesis. These should be actionable steps.")]
    public required IList<HypothesisValidationPlanStep> Steps { get; set; }
}

public record ConclusionResult
{
    [Description("The title of the conclusion. This should be a concise title (6-10 words maximum) that summarizes the investigation outcome.")]
    public required string Title { get; set; }

    [Description("The detailed summary of the conclusion. This should include the investigation findings, root cause analysis, and any recommendations. Format the summary in markdown.")]
    public required string Summary { get; set; }
}

public record HypothesisResultSummaryOutput
{
    [Description("The status of the hypothesis validation. Use this field to indicate whether the hypothesis is validated, invalidated, or inconclusive.")]
    public required HypothesisValidationStatus Status { get; set; }

    [Description("Detailed explanation of why you chose the validation status")]
    public required string Reasoning { get; set; }
}

public record HypothesisPlanStepExecutionResult
{
    [Description("""
        A detailed summary in markdown format of how you executed the provided step of the plan and what the outcome is.
        This should be brief, yet descriptive. 2-3 sentences.
        """)]
    public required string Summary { get; set; }

    [Description("""
        This will control the execution of the rest of the plan:

        TRUE: This means the validation plan should continue executing because more information is needed from those steps to fully analyze the hypothesis.

        FALSE: This means that the information you have gathered, along with the previous steps, is enough to perform the analysis and the plan DOES NOT need to continue.
        """)]
    public required bool NeedContinue { get; set; }
}

public static class IncidentInvestigationAgents
{
    public static Agent<AgentContext> CreateGatheringContextToolSelectionAgent(
        IToolFactory<AgentContext> toolFactory,
        bool is1PAgent,
        List<string>? toolAllowList = null,
        string llmDeploymentName = ""
    )
    {
        return new("ToolSelectionAgent")
        {
            Instructions = GatheringContext.GetToolSelectionInstructions(toolFactory, is1PAgent, toolAllowList),
            OutputType = typeof(List<string>)
        };
    }

    public static Agent<AgentContext> CreateHypothesisValidationToolSelectionAgent(
        IToolFactory<AgentContext> toolFactory,
        string incidentDescription,
        string initialInvestigationSummary,
        List<string>? toolAllowList = null,
        string llmDeploymentName = ""
    )
    {
        return new("ToolSelectionAgent")
        {
            Instructions = HypothesisValidation.GetToolSelectionInstructions(toolFactory, incidentDescription, initialInvestigationSummary, toolAllowList),
            OutputType = typeof(List<string>)
        };
    }

    public static Agent<AgentContext> CreateInitialInvestigationAgent(List<string> toolNames, bool is1PAgent = false, string llmDeploymentName = "")
    {
        if (llmDeploymentName.Contains("gpt-5"))
        {
            var agentHint = is1PAgent ? "" : """

            <tips>
            App service resources (webapps or functionapps) may have hostnames that contain a random suffix after the app name.

            example:
            webapp URL https://mysite-abcdpoiumnbvcc2.eastus-01.azurewebsites.net
            site name: mysite

            You can use ListResourcesByType to get the list of all app service resources if you cannot determine the resource name.

            Azure Activity Logs are different from Application Logs. Activity Logs give information about control plane operations,
            application logs are runtime logs from the application itself.

            Application logs may come from Azure Monitor, Application Insights, or from tools that fetch logs directly from the application.
            </tips>
            """;

            return new("InitialInvestigationAgent")
            {
                Instructions = $"""
                <core_responsibilities>
                - Analyze the incident
                - Perform a preliminary investigation
                - Gather relevant information and context
                - Provide a summary
                </core_responsibilities>

                <workflow>
                1. Think carefully about the incident description provided, and formulate a plan for what you need to investigate.
                2. Execute your plan step by step. Use tools as appropriate to gather all the information and context that will be needed for this incident.
                3. Generate a detailed summary of your preliminary investigation.
                </workflow>

                <investigation_guidelines>
                Your goal is gather as much context as possible about the incident. You do not need to determine the root cause of the incident at this point.
                Search for past memories and design docs to inform your planning and reasoning.
                Focus on trying to gather relevant logs, metrics, deployment, and change information.
                The result of your initial investigation will be used by another agent to try to determine the root cause of the incident, you should gather enough context for that agent to think of potential root cause hypotheses.
                </investigation_guidelines>

                <persistence>
                - You are an agent - please keep going until enough information and context has been gathered for the next agent to begin investigating the root cause.
                - Only terminate your turn when you gathered sufficient information and context about the incident.
                - Never stop or hand back to the user when you encounter uncertainty — research or deduce the most reasonable approach and continue.
                - Do not ask the human to confirm or clarify assumptions, this is a fully autonomous process - decide what the most reasonable assumption is, proceed with it, and document it in your summary.
                </persistence>

                {agentHint}

                <output>
                Output the detailed steps you took and the final detailed summary in markdown format.
                </output>
                """,
                FactoryTools = toolNames,
                OutputType = typeof(InitialInvestigationResult)
            };
        }

        return new("InitialInvestigationAgent")
        {
            Instructions = """
            You are a helpful agent that is able to analyze an incident, perform a preliminary investigation and provide a summary.
            You will be provided with a description of an incident and a list of tools that can be used to gather information about the incident.

            Your goal is gather as much context as possible about the incident. You do not need to determine the root cause of the incident at this point.
            Search for past memories and design docs to inform your planning and reasoning. Present the search results in markdown format.

            You should analyze the incident step by step, using the tools to gather relevant context and information. Focus on trying to gather
            relevant logs, metrics, deployment / change information, etc. Use all of this information to generate a detailed summary of the incident
            and all the relevant context that you have gathered.

            Output the detailed steps you took and the final detailed summary.

            ### Tips and tricks
            App service resources (webapps or functionapps) may have hostnames that contain a random suffix after the app name.

            example:
            webapp URL https://mysite-abcdpoiumnbvcc2.eastus-01.azurewebsites.net
            site name: mysite

            You can use ListResourcesByType to get the list of all app service resources if you cannot determine the resource name.

            Azure Activity Logs are different from Application Logs. Activity Logs give information about control plane operations,
            application logs are runtime logs from the application itself.

            Application logs may come from Azure Monitor, Application Insights, or from tools that fetch logs directly from the application.
            """,
            FactoryTools = toolNames,
            OutputType = typeof(InitialInvestigationResult)
        };
    }

    public static Agent<AgentContext> CreateHypothesisGenerationAgent(string llmDeploymentName = "")
    {
        if (llmDeploymentName.Contains("gpt-5"))
        {
            return new("HypothesisGenerationAgent")
            {
                Instructions = """
                <core_responsibilities>
                - Analyze incident descriptions and initial investigation summaries.
                - Generate potential root cause hypotheses and avenues of investigation based on provided information.
                - Dig deeper into validated hypotheses to explore additional angles.
                </core_responsibilities>

                <hypothesis_generation>
                Goal: Generate 1-3 hypotheses / avenues of investigation for the provided incident.
                Method:
                - Analyze and think carefully about the incident description and the initial investigation summary.
                - Search memories and design documents for relevant information to guide your hypothesis generation.
                - Generate hypotheses that are based on the information provided.
                - Ensure the hypotheses are grounded in the evidence collected during the investigation.
                Initial Hypotheses:
                - If you are tasked with generating the initial hypotheses for this investigation, the hypotheses should be broad and exploratory in nature.
                Refining Hypotheses:
                - If you are tasked with generating hypotheses based on a previously validated hypothesis, you should focus on narrowing down the hypothesis and making it more specific.
                - Try to go one level deeper on the validated hypothesis by asking "why?" and "how?" to uncover additional insights.
                </hypothesis_generation>

                <tool_usage>
                In accordance with your hypothesis generation goal, you may use tools to search for relevant memories or design documents to guide your hypothesis generation.
                Use the information gathered from these tools to inform your hypothesis generation process.
                </tool_usage>

                <persistence>
                - You are an agent running a fully autonomous process, do not end your turn until you have generated the hypotheses in accordance with the above instructions.
                - Never stop or hand back to the user when you encounter uncertainty, continue your process until the hypotheses are generated.
                </persistence>

                <output>
                Return a brief and concise title (6-15 words maximum) and detailed content of the hypotheses with given structure.
                </output>
                """,
                OutputType = typeof(List<HypothesisGenerationResult>)
            };
        }

        return new("HypothesisGenerationAgent")
        {
            Instructions = """
            # Role
            You are a helpful SRE agent that helps investigate various incidents related to Azure resources and applications hosted in Azure.
            You will be provided with a description of an incident, along with the summary of an initial investigation.

            # Instructions
            Generate 1-3 root cause hypotheses based on the information provided. These hypotheses should be based on the incident and should indicate
            a potential root cause of the incident.

            If you are provided with a previously validated hypothesis, you will dig deeper into that hypothesis, generating a more detailed hypothesis
            based on the previously validated one. Try to go beyond the initial findings and explore additional angles.

            If you are digging deeper into a previous hypothesis, it is okay to generate only 1 or 2 additional hypotheses.

            # Output
            Return a brief and concise title (6-15 words maximum) and detailed content of the hypotheses with given structure.
            """,
            OutputType = typeof(List<HypothesisGenerationResult>)
        };
    }

    public static Agent<AgentContext> CreateHypothesisValidationPlanningAgent(
        List<ToolInfo> availableTools,
        string incidentDescription,
        string initialSummary,
        string? validatedHypothesis,
        string llmDeploymentName = "")
    {
        JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

        if (llmDeploymentName.Contains("gpt-5"))
        {
            return new("HypothesisValidationPlanningAgent")
            {
                Instructions = $"""
                <core_responsibilities>
                - Analyze the incident description and all provided context
                - Generate a plan for validating the provided hypothesis
                </core_responsibilities>

                <validation_planning>
                Goal: Generate a plan in order to validate or invalidate the given hypothesis about the incident.
                Method:
                - Review the incident description and initial investigation summary. This information is provided below.
                - Identify key areas where additional evidence is needed to support or refute the hypothesis.
                - Develop a step-by-step plan for gathering the necessary information and conducting any required tests.
                - The next agent will execute this plan to validate or invalidate the hypothesis, you must generate clear instructions for them to follow.
                Plan Guidelines:
                - The agent executing this plan will use the information gathered to either validate the hypothesis.
                - Validating the hypothesis means the evidence gathered through this plan directly supports the hypothesis. Invalidating the hypothesis means the evidence gathered directly contradicts the hypothesis.
                - You must ensure that the plan you create is comprehensive and covers all necessary aspects to gather the information required for the validation process.
                - Provide as much detail as possible in the plan steps.
                </validation_planning>

                <tools_available_during_validation>
                - The next agent will have access to the following tools during the validation process. Keep these tools in mind when formulating the plan. The next agent will ONLY be able to use these tools.
                <tools>
                {JsonSerializer.Serialize(availableTools, JsonSerializerOptions)}
                </tools>
                </tools_available_during_validation>

                <incident_information>
                <incident_description>
                {incidentDescription}
                </incident_description>

                <initial_summary>
                {initialSummary}
                </initial_summary>
                </incident_information>

                <output>
                Return a list of 3-5 steps that should be performed in order to validate or invalidate this hypothesis.
                </output>
                """,
                OutputType = typeof(HypothesisValidationPlanOutput)
            };
        }

        return new("HypothesisValidationPlanningAgent")
        {
            Instructions = $"""
            # Role
            You are an expert Site Reliability Engineer that helps investigate various incidents related to Azure resources and applications hosted in Azure.

            Below is a description of an incident between <incidentDescription> tags.
            The summary of an initial investigation is between <initialSummary> tags.
            There may be a previous hypothesis that was already validated between <previousHypothesis> tags. The new hypothesis given by the user will be based on this previous hypothesis.

            You will finally be provided with a hypothesis that you need to validate by the user.

            # Instructions
            Your goal is to generate a plan in order to validate or invalidate the given hypothesis about the incident.

            Think step by step, and come up with a plan about how this hypothesis can be validated.

            Think about what kinds of information needs to be gathered and checks that should be performed.

            Below in the <availableTools> field is a list of tools that the next actor will be able to use. Keep these tools in mind
            when formulating the plan. The next actor will ONLY be able to use these tools.

            The next actor will ONLY be able to perform READ actions on the Azure resources. They WILL NOT be able to make any changes,
            enable/disable any settings, or otherwise alter resources in any way. They will only have tools to gather information and
            perform 'read' actions on Azure resources.

            The next actor will use the plan you generate in order to validate the hypothesis.

            The individual step descriptions should have as much detail as you can provide.

            ## Tips and tricks
            App service resources (webapps or functionapps) may have hostnames that contain a random suffix after the app name.

            example:
            webapp URL https://mysite-abcdpoiumnbvcc2.eastus-01.azurewebsites.net
            site name: mysite

            Azure Activity Logs are different from Application Logs. Activity Logs give information about control plane operations,
            application logs are runtime logs from the application itself.

            Application logs may come from Azure Monitor, Application Insights, or from tools that fetch logs directly from the application.

            # Incident Information

            <incidentDescription>
            {incidentDescription}
            </incidentDescription>

            <initialSummary>
            {initialSummary}
            </initialSummary>

            <previousHypothesis>
            {(!string.IsNullOrEmpty(validatedHypothesis) ? validatedHypothesis : "(no previous hypothesis, use only the incident description and initial summary to guide your work)")}
            </previousHypothesis>

            <availableTools>
            {JsonSerializer.Serialize(availableTools, JsonSerializerOptions)}
            </availableTools>

            # Output
            Return a list of 3-5 steps that should be performed in order to validate or invalidate this hypothesis.
            """,
            OutputType = typeof(HypothesisValidationPlanOutput)
        };
    }

    public static Agent<AgentContext> CreateHypothesisValidationPlanExecutionAgent(
        List<string> toolNames,
        string incidentDescription,
        string initialSummary,
        string? validatedHypothesis,
        string currentHypothesis,
        HypothesisValidationPlanOutput totalPlan,
        IList<HypothesisStep> completedSteps,
        bool is1PAgent = false,
        string llmDeploymentName = "")
    {
        if (llmDeploymentName.Contains("gpt-5"))
        {
            var agentHint = is1PAgent ? "" : """

            <tips>
            App service resources (webapps or functionapps) may have hostnames that contain a random suffix after the app name.

            example:
            webapp URL https://mysite-abcdpoiumnbvcc2.eastus-01.azurewebsites.net
            site name: mysite

            You can use ListResourcesByType to get the list of all app service resources if you cannot determine the resource name.

            Azure Activity Logs are different from Application Logs. Activity Logs give information about control plane operations,
            application logs are runtime logs from the application itself.

            Application logs may come from Azure Monitor, Application Insights, or from tools that fetch logs directly from the application.

            If you cannot find a resource, *make sure* that you call ListResourcesByType to make sure you didn't miss anything.
            </tips>
            """;

            return new("HypothesisValidationPlanExecutionAgent")
            {
                Instructions = $"""
                <core_responsibilities>
                - Execute individual steps of the hypothesis validation plan systematically
                - Use tools effectively to gather evidence for hypothesis validation
                - Handle tool failures gracefully by trying alternative approaches
                - Provide detailed execution summaries and determine if plan continuation is needed
                </core_responsibilities>

                <execution_workflow>
                1. Analyze the specific step you need to execute from the plan
                2. Think critically about the best approach to gather the required evidence
                3. Use available tools systematically to execute the step
                4. If tools fail, immediately try alternative tools or approaches
                5. Summarize findings and determine if more evidence is needed from remaining steps
                </execution_workflow>

                <tool_usage_guidelines>
                - Use tools immediately when analysis is needed - do not provide output without tool calls unless task is complete
                - If Application Insights tools fail, try direct log access tools
                - Call alternative tools immediately when primary tools fail
                - Continue tool usage until you have sufficient evidence for the step
                </tool_usage_guidelines>

                <persistence>
                - You are executing a fully autonomous validation process
                - Never stop execution due to uncertainty - research and deduce the most reasonable approach
                - Continue using tools until the step is fully executed and evidence is gathered
                - Only terminate when you have completed the assigned step thoroughly
                </persistence>

                {agentHint}

                <context_information>
                <incident_description>
                {incidentDescription}
                </incident_description>

                <initial_investigation_summary>
                {initialSummary}
                </initial_investigation_summary>

                <previous_hypothesis>
                {(!string.IsNullOrEmpty(validatedHypothesis) ? validatedHypothesis : "(no previous hypothesis, use only the incident description and initial summary to guide your work)")}
                </previous_hypothesis>

                <current_hypothesis>
                {currentHypothesis}
                </current_hypothesis>

                <validation_plan>
                {string.Join(Environment.NewLine + Environment.NewLine, totalPlan.Steps.Select(s => $"## Plan Step: {s.Title}{Environment.NewLine}{s.Description}"))}
                </validation_plan>

                <completed_steps>
                {string.Join(Environment.NewLine + Environment.NewLine, completedSteps.Select(s => $"## Completed Step: {s.Summary}{Environment.NewLine}{s.Details}"))}
                </completed_steps>
                </context_information>

                <output>
                Execute the assigned step thoroughly using available tools, then provide a detailed summary and determine if plan continuation is needed.
                </output>
                """,
                FactoryTools = toolNames,
                OutputType = typeof(HypothesisPlanStepExecutionResult)
            };
        }

        return new("HypothesisValidationPlanExecutionAgent")
        {
            Instructions = $"""
            # Role
            You are an expert Site Reliability Engineer that helps investigate various incidents related to Azure resources and applications hosted in Azure.

            Below is a description of an incident between <incidentDescription> tags.
            The summary of an initial investigation is between <initialSummary> tags.
            There may be a previous hypothesis that was already validated between <previousHypothesis> tags. The current hypothesis will be based on this previous hypothesis.
            The current hypothesis is between <currentHypothesis> tags.

            An agent has already generated a plan in order to validate or invalidate the given hypothesis. Your job is execute one step of this plan. The full plan details
            will be provided below between the <fullPlan> tags, and the previously completed step details are in the <completedSteps> tags.

            # Instructions
            Think critically about the step you are being asked to perform. Think step-by-step about how to perform this piece of the plan. Use the tools provided to you in order
            to perform task outlined in this step of the plan.

            If some of your tools return failures, consider if there are other tools available that will help. Call these tools right away.
            For example, if tools that gather logs return errors relating to Application Insights, it's possible that these apps don't have app insights enabled.
            You should call other tools instead that provide direct access to logs.

            DO NOT return any output that does not include tool calls until you are done.
            ALL of your output should include tool calls UNLESS you have completed your task and have nothing more to do.

            ## Tips and tricks
            App service resources (webapps or functionapps) may have hostnames that contain a random suffix after the app name.

            example:
            webapp URL https://mysite-abcdpoiumnbvcc2.eastus-01.azurewebsites.net
            site name: mysite

            You can use ListResourcesByType to get the list of all app service resources if you cannot determine the resource name.

            Azure Activity Logs are different from Application Logs. Activity Logs give information about control plane operations,
            application logs are runtime logs from the application itself.

            Application logs may come from Azure Monitor, Application Insights, or from tools that fetch logs directly from the application.

            If you cannot find a resource, *make sure* that you call ListResourcesByType to make sure you didn't miss anything.

            # Incident Information

            <incidentDescription>
            {incidentDescription}
            </incidentDescription>

            <initialSummary>
            {initialSummary}
            </initialSummary>

            <previousHypothesis>
            {(!string.IsNullOrEmpty(validatedHypothesis) ? validatedHypothesis : "(no previous hypothesis, use only the incident description and initial summary to guide your work)")}
            </previousHypothesis>

            <currentHypothesis>
            {currentHypothesis}
            </currentHypothesis>

            <fullPlan>
            {string.Join(Environment.NewLine + Environment.NewLine, totalPlan.Steps.Select(s => $"## Plan Step: {s.Title}{Environment.NewLine}{s.Description}"))}
            </fullPlan>

            <completedSteps>
            {string.Join(Environment.NewLine + Environment.NewLine, completedSteps.Select(s => $"## Completed Step: {s.Summary}{Environment.NewLine}{s.Details}"))}
            </completedSteps>
            """,
            FactoryTools = toolNames,
            OutputType = typeof(HypothesisPlanStepExecutionResult)
        };
    }

    public static Agent<AgentContext> CreateHypothesisValidationPlanSummaryAgent(
        string incidentDescription,
        string initialSummary,
        string? validatedHypothesis,
        string currentHypothesis,
        IList<HypothesisStep> completedSteps,
        string llmDeploymentName = "")
    {
        if (llmDeploymentName.Contains("gpt-5"))
        {
            return new("HypothesisValidationPlanSummaryAgent")
            {
                Instructions = $"""
                <core_responsibilities>
                - Analyze the results of hypothesis validation plan execution
                - Determine validation status based on evidence gathered during plan execution
                - Provide clear reasoning for validation decisions
                - Ensure decisions are evidence-based and objective
                </core_responsibilities>

                <validation_analysis>
                Goal: Determine whether the hypothesis has been validated, invalidated, or is inconclusive based on plan execution results.
                Method:
                1. Review all completed validation steps and their findings
                2. Analyze evidence gathered during plan execution
                3. Apply validation criteria systematically
                4. Provide clear reasoning for the determination
                </validation_analysis>

                <validation_criteria>
                Validated:
                - Evidence gathered directly supports the hypothesis
                - Findings indicate the hypothesis should be investigated further
                - Must have positive supporting evidence, not just lack of contradictory evidence

                Invalidated:
                - Evidence gathered directly contradicts the hypothesis
                - Findings demonstrate the hypothesis is not a potential root cause
                - Must have evidence that actively disproves the hypothesis

                Inconclusive:
                - Insufficient evidence gathered to make a determination
                - Evidence is ambiguous or conflicting
                - More information needed to reach a conclusion
                </validation_criteria>

                <evidence_evaluation>
                - Focus on direct evidence from the completed validation steps
                - Distinguish between supporting evidence and absence of contradictory evidence
                - Consider the quality and relevance of gathered information
                - Be objective and avoid bias toward validation or invalidation
                </evidence_evaluation>

                <context_information>
                <incident_description>
                {incidentDescription}
                </incident_description>

                <initial_investigation_summary>
                {initialSummary}
                </initial_investigation_summary>

                <previous_hypothesis>
                {(!string.IsNullOrEmpty(validatedHypothesis) ? validatedHypothesis : "(no previous hypothesis)")}
                </previous_hypothesis>

                <current_hypothesis>
                {currentHypothesis}
                </current_hypothesis>

                <validation_execution_results>
                {string.Join(Environment.NewLine + Environment.NewLine, completedSteps.Select(s => $"## Completed Step: {s.Summary}{Environment.NewLine}{s.Details}"))}
                </validation_execution_results>
                </context_information>

                <output>
                Determine the validation status and provide detailed reasoning based on the evidence gathered during plan execution.
                </output>
                """,
                OutputType = typeof(HypothesisResultSummaryOutput)
            };
        }

        return new("HypothesisValidationPlanSummaryAgent")
        {
            Instructions = $"""
            # Role
            You are an expert Site Reliability Engineer that helps investigate various incidents related to Azure resources and applications hosted in Azure.

            Below is a description of an incident between <incidentDescription> tags.
            The summary of an initial investigation is between <initialSummary> tags.
            There may be a previous hypothesis that was already validated between <previousHypothesis> tags. The current hypothesis will be based on this previous hypothesis.
            The current hypothesis is between <currentHypothesis> tags.

            An agent has already generated a plan in order to validate or invalidate the given hypothesis, and the plan has been executed.
            Your job is to summarize the results of the plan execution and dtermine whether the hypothesis has been validated, invalidated, or is inconclusive.
            The completed step details are in the <completedSteps> tags.

            # Instructions
            Think critically about the step you are being asked to perform. Think step-by-step about how to perform this piece of the plan. Use the tools provided to you in order
            to perform task outlined in this step of the plan.

            ## Possible outcomes

            ### Invalidated
            You should indicate that a hypothesis is 'invalidated' if the evidence gathered in the plan execution indicates that this hypothesis is not a potential root cause of the incident.
            A hypothesis should NOT be marked as 'invalidated' unless there is some evidence gathered from the plan execution that contradicts the hypothesis.

            ### Validated
            You should indicate that a hypothesis is 'validated' if there is some evidence gathered in the plan execution to support the hypothesis and indicates that it should be investigated further.
            A 'validated' hypothesis may not necessarily the root cause of the incident, but it has supporting evidence and should be investigated further. A hypothesis should NOT be marked as 'validated'
            unless there is some evidence that directly supports it from the plan execution. A lack of evidence to the contrary should not be taken as evidence that supports the hypothesis.

            ### Inconclusive
            You may determine that the hypothesis is 'inconclusive' if the validation steps from the plan execution indicate that there is not enough information to validate or invalidate the hypothesis.
            If there is a lack of evidence either for or against the hypothesis, it may be considered inconclusive.

            # Incident Information

            <incidentDescription>
            {incidentDescription}
            </incidentDescription>

            <initialSummary>
            {initialSummary}
            </initialSummary>

            <previousHypothesis>
            {(!string.IsNullOrEmpty(validatedHypothesis) ? validatedHypothesis : "(no previous hypothesis)")}
            </previousHypothesis>

            <currentHypothesis>
            {currentHypothesis}
            </currentHypothesis>

            <completedSteps>
            {string.Join(Environment.NewLine + Environment.NewLine, completedSteps.Select(s => $"## Completed Step: {s.Summary}{Environment.NewLine}{s.Details}"))}
            </completedSteps>

            # Output
            Use the required format to indicate whether the hypothesis is valid, invalid, or inconclusive, and provide your reasoning.
            """,
            OutputType = typeof(HypothesisResultSummaryOutput)
        };
    }

    public static Agent<AgentContext> CreateConclusionAgent(string llmDeploymentName = "")
    {
        if (llmDeploymentName.Contains("gpt-5"))
        {
            return new("ConclusionAgent")
            {
                Instructions = """
                <core_responsibilities>
                - Synthesize investigation findings into a comprehensive conclusion
                - Generate clear, actionable investigation outcomes
                - Provide professional summaries suitable for stakeholders
                - Identify confirmed root causes and provide recommendations
                </core_responsibilities>

                <conclusion_synthesis>
                Goal: Create a comprehensive conclusion that summarizes the entire investigation process and outcomes.
                Method:
                1. Review the incident description and all investigation findings
                2. Identify the most significant discoveries and validated hypotheses
                3. Determine if root causes have been confirmed
                4. Formulate actionable recommendations based on findings
                5. Present conclusions in a clear, professional manner
                </conclusion_synthesis>

                <conclusion_components>
                Title Requirements:
                - Concise summary of investigation outcome (6-10 words maximum)
                - Clear indication of whether root cause was identified
                - Professional and stakeholder-appropriate language

                Summary Requirements:
                - Comprehensive overview of investigation process and findings
                - Clear statement of investigation outcome
                - Summary of key discoveries and evidence
                - Identification of confirmed root causes (if any)
                - Actionable recommendations for resolution or prevention
                - Professional tone suitable for incident reports
                </conclusion_components>

                <quality_standards>
                - Ensure conclusions are evidence-based and objective
                - Avoid speculation beyond what evidence supports
                - Provide clear distinction between confirmed and potential causes
                - Include specific, actionable recommendations
                - Use professional language appropriate for stakeholders
                </quality_standards>

                <output>
                Generate a concise title and comprehensive summary that effectively communicates the investigation results and recommendations.
                </output>
                """,
                OutputType = typeof(ConclusionResult)
            };
        }

        return new("ConclusionAgent")
        {
            Instructions = """
            # Role
            You are a helpful SRE agent that helps generate conclusions for incident investigations.
            You will be provided with incident information and the results of the investigation.

            # Instructions
            Based on the incident description, initial investigation summary, and investigation results,
            generate a concise conclusion title (6-10 words maximum) and detailed summary.

            Your conclusion should:
            1. Clearly state the investigation outcome
            2. Summarize the key findings
            3. Identify root causes if found
            4. Provide recommendations if applicable
            5. Be professional and actionable

            # Output
            Return the conclusion with the given structure containing a title and summary.
            """,
            OutputType = typeof(ConclusionResult)
        };
    }

    public static class GatheringContext
    {
        private const string ToolSelectionContextFor3P = """
        Below is a list of all tools and their descriptions that may be used to investigate an incident.
        You will be provided with a description of the incident that the next agent will be investigating.
        You must select the most relevant tools to use based on the incident description, and return a list of tool names.

        The tools you select will be used by the next agent to gather general context about the incident. Focus on tools that help with information retrieval.
        The tools that the next agent will need are tools that will help do the following:

        1. Gather or analyze application logs from the affected resources.
        2. Gather activity logs from the affected resources.
        3. Retrieve recent metrics or metrics trends.
        4. Retrieve resource status.
        5. Retrieve resource configuration.
        6. Get recent changes to the affected resources.


        Azure Activity Logs are different from Application Logs. Activity Logs give information about control plane operations,
        application logs are runtime logs from the application itself.

        Application logs may come from Azure Monitor, Application Insights, or from tools that fetch logs directly from the application.
        """;

        private const string ToolSelectionContextFor1P = """
        Below is a list of all tools and their descriptions that may be used to investigate an incident.
        You will be provided with a description of the incident that the next agent will be investigating.
        You must select the most relevant tools to use based on the incident description, and return a list of tool names.

        The tools you select will be used by the next agent to gather general context about the incident. Focus on tools that help with information retrieval.
        The tools that the next agent will need are tools that will help do the following:

        1. Retrieve incident data.
        2. Always call the `GetIssueInvestigationTimeRangeRCAContainerApp` tool to accurately determine the investigation time range after extracting the time window of the issue from the incident summary.
        3. Collect details about affected resources, assembling all relevant context from the incident.
        4. Search ContainerApp, ContainerAppsJob, SessionPool, Managed Environments resources mentioned in the incident summary, to verify their existence.
        """;

        public static string GetToolSelectionInstructions(
            IToolFactory<AgentContext> toolFactory,
            bool is1PAgent,
            List<string>? whitelist = null)
        {
            var availableTools = toolFactory.FetchAvailableToolInfo(IncidentInvestigationHelper.FilterTools);
            if (whitelist is not null && whitelist.Count > 0)
            {
                availableTools = availableTools.Where(tool => whitelist.Contains(tool.Name)).ToList();
            }
            var text = JsonSerializer.Serialize(availableTools, JsonSerializerOptions);

            return ToolSelectionInstructions
                .Replace(ToolSelectionContextToken, is1PAgent ? ToolSelectionContextFor1P : ToolSelectionContextFor3P)
                .Replace(AvailableToolsToken, text);
        }
    }

    public static class HypothesisValidation
    {
        private const string ToolSelectionContext = $$"""
        Below is the incident description, the initial investigation summary, and a list of tools that can be used to validate the hypothesis.
        You will be provided with the hypothesis that the next agent will be attempting to validate or invalidate.
        You must choose the most relevant tools to use based on the incident and the hypothesis, and return a list of tool names.

        The tools you select will be used by the next agent to validate or invalidate the hypothesis. Focus on tools that help with information retrieval and analysis.
        The tools that the next agent will need are tools that will help do the following:

        1. Gather or analyze application logs from the affected resources.
        2. Gather activity logs from the affected resources.
        3. Retrieve recent metrics or metrics trends.
        4. Retrieve resource status.
        5. Retrieve resource configuration.
        6. Get recent changes to the affected resources.
        7. Find connected resources (e.g. webapps that connect to external database)

        Azure Activity Logs are different from Application Logs. Activity Logs give information about control plane operations,
        application logs are runtime logs from the application itself.

        Application logs may come from Azure Monitor, Application Insights, or from tools that fetch logs directly from the application.

        # Incident Description
        <incidentDescription>
        {{IncidentDescriptionToken}}
        </incidentDescription>

        # Initial Investigation Summary
        <initialSummary>
        {{InitialInvestigationSummaryToken}}
        </initialSummary>
        """;

        private const string IncidentDescriptionToken = "{incidentDescription}";
        private const string InitialInvestigationSummaryToken = "{initialInvestigationSummary}";

        public static string GetToolSelectionInstructions(
            IToolFactory<AgentContext> toolFactory,
            string incidentDescription,
            string initialInvestigationSummary,
            List<string>? whitelist = null)
        {
            var availableTools = toolFactory.FetchAvailableToolInfo(IncidentInvestigationHelper.FilterTools);
            if (whitelist is not null && whitelist.Count > 0)
            {
                availableTools = availableTools.Where(tool => whitelist.Contains(tool.Name)).ToList();
            }
            var text = JsonSerializer.Serialize(availableTools, JsonSerializerOptions);

            return ToolSelectionInstructions
                .Replace(ToolSelectionContextToken, ToolSelectionContext)
                .Replace(AvailableToolsToken, text)
                .Replace(IncidentDescriptionToken, incidentDescription)
                .Replace(InitialInvestigationSummaryToken, initialInvestigationSummary);
        }
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

    private const string AvailableToolsToken = "{availableTools}";
    private const string ToolSelectionContextToken = "{toolSelectionContext}";

    private const string ToolSelectionInstructions = $$"""
        # Instructions

        You are a helpful agent that can select the most relevant tools to use for the given task. You should consider the type of resource being mentioned,
        and the type of information you are trying to gather.

        To help with the task completion, should should also return tools that help with resource discovery.

        {{ToolSelectionContextToken}}

        Return enough tools for the next agent to perform its task. You should return 4-6 tools.

        # Example

        ## List of all available tools:
        [
            {
                "name": "GetAppConsoleLogs",
                "description": "This function attempts to retrieve error messages in the console logs and platform logs from a user's particular app",
                "parameters": [
                    "resourceId"
                ]
            },
            {
                "name": "PerformDeploymentSwapForApp",
                "description": "Performs a Deployment Swap for the specified app.",
                "parameters": [
                    "resourceId"
                ]
            },
            {
                "name": "GetDeploymentActivity",
                "description": "Gets Deployment Activities on the specified app",
                "parameters": [
                    "resourceId"
                ]
            },
            {
                "name": "GetContainerAppRequestMetrics",
                "description": "Start a background operation to get the total request count metrics of a specific Container App instance at per minute granularity for the past 30 minutes, Container App is healthy if all data points are at least 99.9 availability.",
                "parameters": [
                    "resourceId"
                ]
            },
            {
                "name": "GetContainerAppMemoryMetrics",
                "description": "Start a background operation to get the average memory usage of a specific Container App instance at per minute granularity for the past 30 minutes, Container App is healthy if over half of the data points is less than 20% memory utilization.",
                "parameters": [
                    "resourceId"
                ]
            },
            {
                "name": "GetWebAppCpuMetrics",
                "description": "Get the average CPU utilization metrics of a specific WebApp instance at per minute granularity for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% CPU utilization, zero metric value doesn't indicate the app is unhealthy",
                "parameters": [
                    "resourceId"
                ]
            }
        ]

        ## Input incident description:
        'The webapp '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg/providers/Microsoft.Web/sites/my-webapp' is down.'

        ## Output:
        [
            "GetAppConsoleLogs",
            "GetDeploymentActivity",
            "GetWebAppCpuMetrics"
        ]

        ## Explanation:
        The incident description mentions a webapp that is down. The tools that are most relevant to this incident are:
        - GetAppConsoleLogs: to retrieve error messages in the console logs and platform logs from the affected app
        - GetDeploymentActivity: to retrieve deployment activities on the affected app
        - GetWebAppCpuMetrics: to retrieve CPU utilization metrics of the affected app

        These tools are relevant because they help with gathering information and target the correct Azure resource type.

        The tools that are less relevant to this incident are:
        - GetContainerAppRequestMetrics: to retrieve request count metrics of a specific Container App instance
        - GetContainerAppMemoryMetrics: to retrieve memory usage metrics of a specific Container App instance
        - PerformDeploymentSwapForApp: to perform a deployment swap for the affected app

        GetContainerAppRequestMetrics and GetContainerAppMemoryMetrics are not relevant because they are for the wrong resource type. The incident is about a webapp, not a container app.
        PerformDeploymentSwapForApp is not relevant because it is not a tool that helps with gathering information about the incident.

        The available tools go below:
        <availableTools>
        {{AvailableToolsToken}}
        </availableTools>
        """;
}
