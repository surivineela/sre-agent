// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Models.Api.v1;
using Agent.Framework;

namespace Agent.Runtime.AgentTasks.Agents;

public record InitialInvestigationResult
{
    [Description("Steps taken during the investigation")]
    public required IList<InitialInvestigationStep> ContextGatheringSteps { get; set; }

    [Description("Detailed summary of the initial investigation and the relevant context that has been gathered, in markdown format")]
    public required string Summary { get; set; }
}

public record InitialInvestigationStepResult
{
    [Description("Brief title of the step taken.")]
    public required string Title { get; set; }

    [Description("Summary of the this individual step in markdown format")]
    public required string Summary { get; set; }
}

public record HypothesisGenerationResult
{
    [Description("The title of the hypothesis. This should be a brief title that describes the hypothesis.")]
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
    [Description("The status of the hypothesis validation. Use this field to indicate whether the hypothesis is validated, invalidated, or inconclusive.")]
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
    [Description("Title of the step you plan to perform to validate a hypothesis. User-friendly description.")]
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
    [Description("The title of the conclusion. This should be a concise title that summarizes the investigation outcome.")]
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

public class IncidentInvestigationAgents
{
    public static Agent<AgentContext> CreateToolSelectionAgent(string instructions)
    {
        return new("ToolSelectionAgent")
        {
            Instructions = instructions,
            MaxReflectionCount = 0,
            CustomReflectionNote = """
                Are the tools you selected relevant to the incident?
                Are the tools you selected enough to gather general context about the incident?
                Are the tools you selected for the right resource type?
                """,
            OutputType = typeof(List<string>)
        };
    }

    public static Agent<AgentContext> CreateInitialInvestigationAgent(List<string> toolNames)
    {
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
            MaxReflectionCount = 0,
            FactoryTools = toolNames,
            OutputType = typeof(InitialInvestigationResult)
        };
    }

    public static Agent<AgentContext> CreateHypothesisGenerationAgent()
    {
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
                based on the previously validated one.

                If you are digging deeper into a previous hypothesis, it is okay to generate only 1 or 2 additional hypotheses.

                # Output
                Return a brief title and detailed content of the hypotheses with given structure.
                """,
            MaxReflectionCount = 0,
            CustomReflectionNote = """
                Are the hypotheses you generated relevant to the incident?
                Are the hypotheses you generated based on the incident description and the initial investigation summary?
                """,
            OutputType = typeof(List<HypothesisGenerationResult>)
        };
    }

    public static Agent<AgentContext> CreateHypothesisValidationPlanningAgent(
        List<ToolInfo> availableTools,
        string incidentDescription,
        string initialSummary,
        string? validatedHypothesis)
    {
        JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

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
        IList<HypothesisStep> completedSteps)
    {
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
        IList<HypothesisStep> completedSteps)
    {
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

            ### Invalid
            You should indicate that a hypothesis is 'invalid' if the evidence gathered in the plan execution indicates that this hypothesis is not a potential root cause of the incident.

            ### Valid
            You should indicate that a hypothesis is 'valid' if there is some evidence gathered in the plan execution to support the hypothesis and indicates that it should be investigated further.
            A 'valid' hypothesis may not necessarily the root cause of the incident, but it has supporting evidence and should be investigated further.

            ### Inconclusive
            You may determine that the hypothesis is 'inconclusive' only if the validation steps from the plan execution indicate that there is not enough information to validate or invalidate the hypothesis

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

    public static Agent<AgentContext> CreateHypothesisValidationAgent(
        List<string> toolNames,
        string incidentDescription,
        string initialSummary,
        string? validatedHypothesis)
    {
        return new("HypothesisValidationAgent")
        {
            // TODO: provide example in the system prompt
            Instructions = $"""
                # Role
                You are a helpful SRE agent that helps investigate various incidents related to Azure resources and applications hosted in Azure.

                Below is a description of an incident between <incidentDescription> tags.
                The summary of an initial investigation is between <initialSummary> tags.
                There may be a previous hypothesis that was already validated between <previousHypothesis> tags.

                You will finally be provided with a hypothesis that you need to validate by the user.

                # Instructions
                Your goal is to validate or invalidate the hypothesis by using the tools provided to you. You must use the tools to gather information about the incident
                and the hypothesis. Think step by step, and use the tools provided to gather evidence for or against the hypothesis.

                ## Possible outcomes

                ### Invalid
                You should indicate that a hypothesis is 'invalid' if you find evidence through your tool calls that this hypothesis is incorrect.

                ### Valid
                You should indicate that a hypothesis is 'valid' if there is some evidence to support the hypothesis and it should be investigated further. A 'valid' hypothesis
                is not necessarily the root cause of the incident, but it is a valid hypothesis that should be investigated further.

                ### Inconclusive
                You may determine that the hypothesis is 'inconclusive' only if you cannot determine if the hypothesis is valid or invalid. If you find that you need more information to validate the hypothesis,
                you should ALWAYS use the tools provided to gather more information. If you do not have the necessary tools to gather the information you need, then you can indicate that the hypothesis is inconclusive.

                # Tools
                You will be provided with a list of tools that can be used to validate the hypothesis. You MUST use these tools to gather information about the incident
                and the hypothesis. You MUST NOT rely solely on the incident description and initial summary to validate the hypothesis.

                # Output
                Return the validation result with given structure.

                You MUST plan extensively before each function call, and reflect extensively on the outcomes of the previous function calls.
                Your thinking should be thorough and so it's fine if it's very long. You must think step by step before and after each action you decide to take.

                ### Tips and tricks
                App service resources (webapps or functionapps) may have hostnames that contain a random suffix after the app name.

                example:
                webapp URL https://mysite-abcdpoiumnbvcc2.eastus-01.azurewebsites.net
                site name: mysite

                You can use ListResourcesByType to get the list of all app service resources if you cannot determine the resource name.

                Azure Activity Logs are different from Application Logs. Activity Logs give information about control plane operations,
                application logs are runtime logs from the application itself.

                Application logs may come from Azure Monitor, Application Insights, or from tools that fetch logs directly from the application.

                If you find that you need more information to validate the hypothesis, you should ALWAYS use the tools provided to gather more information.
                If you do not have the necessary tools or are unable to gather the information you need, then you can indicate that the hypothesis is inconclusive.

                MAKE NO ASSUMPTIONS.

                ### Incident Information

                <incidentDescription>
                {incidentDescription}
                </incidentDescription>

                <initialSummary>
                {initialSummary}
                </initialSummary>

                <previousHypothesis>
                {(!string.IsNullOrEmpty(validatedHypothesis) ? validatedHypothesis : "(no previous hypothesis, use only the incident description and initial summary to guide your work)")}
                </previousHypothesis>
                """,
            MaxReflectionCount = 1,
            CustomReflectionNote = """
                If a hypothesis was indicated to be inconclusive, was there a detailed explanation of why it is inconclusive?
                If the result was 'inconclusive', was it because there was not enough information to determine if the hypothesis was valid or invalid? If so, are the available tools sufficient to gather more information?
                The actor should always use the tools provided to attempt to gather information it needs.

                If a hypothesis was indicated to be valid or invalid, was there a detailed explanation of why it is valid or invalid?
                If a hypothesis was indicated to be valid or invalid, was there sufficient evidence to support the conclusion?
                """,
            FactoryTools = toolNames,
            OutputType = typeof(HypothesisValidationResult) // TODO: should we do multiple turns with the agent where each turn = 1 step, rather than a single turn where agent returns a list of steps?
        };
    }

    public static Agent<AgentContext> CreateHypothesisValidationCheckAgent()
    {
        return new("HypothesisValidationCheckAgent")
        {
            // TODO: provide example in the system prompt
            Instructions = """
                # Role
                You are a helpful SRE agent that helps investigate various incidents related to Azure resources and applications hosted in Azure.
                You will be provided with a description of an incident, along with the summary of an initial investigation.
                You will also be provided with a hypothesis that has been validated.

                # Instructions
                Your goal is to determine if the investigation can be stopped here or need to dig deeper to find the root cause, based on the given information.

                # Output
                Return true or false to indicate if the investigation can be stopped now.
                """,
            MaxReflectionCount = 0,
            OutputType = typeof(bool)
        };
    }

    public static Agent<AgentContext> CreateConclusionAgent()
    {
        return new("ConclusionAgent")
        {
            Instructions = """
                # Role
                You are a helpful SRE agent that helps generate conclusions for incident investigations.
                You will be provided with incident information and the results of the investigation.

                # Instructions
                Based on the incident description, initial investigation summary, and investigation results,
                generate a concise conclusion title and detailed summary.

                Your conclusion should:
                1. Clearly state the investigation outcome
                2. Summarize the key findings
                3. Identify root causes if found
                4. Provide recommendations if applicable
                5. Be professional and actionable

                # Output
                Return the conclusion with the given structure containing a title and summary.
                """,
            MaxReflectionCount = 0,
            CustomReflectionNote = """
                Does the conclusion title accurately reflect the investigation outcome?
                Does the summary provide a clear and comprehensive overview of the findings?
                Are the key points from the investigation properly highlighted?
                """,
            OutputType = typeof(ConclusionResult)
        };
    }
}
