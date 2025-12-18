// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Mcp;

#region Common Response Types

public record McpErrorResponse(string Error);

public record McpSuccessResponse(bool Success, string Message);

public record McpSuccessWithPathResponse(bool Success, string Message, string PersistencePath);

#endregion

#region Documentation Responses

public record DocumentationResponse(string Topic, string Documentation);

#endregion

#region Agent Building Responses

public record AgentInfo(
    string Name,
    string? Description,
    string AgentType,
    string Status);

public record StartAgentBuildResponse(
    bool Success,
    string Message,
    AgentInfo Agent,
    List<string> NextSteps);

public record ConfigureAgentResponse(
    bool Success,
    string Message,
    AgentContextDto CurrentConfiguration);

public record AgentContextDto(
    string Name,
    string? Description,
    string? SystemPrompt,
    string AgentType,
    string Status,
    List<string> Tools,
    List<string> Handoffs,
    List<string> Connectors,
    List<string> CommonPrompts,
    List<string> Notes,
    double? Temperature,
    int? MaxReflectionCount,
    bool? VanillaMode,
    bool? DisableDocumentRetrieval,
    string? LlmModelName,
    TriggerDto? Trigger,
    ScheduledTaskDto? ScheduledTask,
    string CreatedAt,
    string UpdatedAt);

public record TriggerDto(
    string Type,
    string? ImpactedService,
    List<int> Priorities,
    string? TitleContains,
    string? AlertId,
    string? OwningTeamId,
    int MaxAttempts);

public record ScheduledTaskDto(
    string? CronExpression,
    string? AgentPrompt,
    string? NotificationChannel,
    string? StartTime,
    string? EndTime,
    int? MaxExecutions);

public record ConfigureTriggerResponse(
    bool Success,
    string Message,
    object TriggerConfiguration);

public record GenerateYamlResponse(
    string AgentYaml,
    string FilePath,
    string? ScheduledTaskYaml = null,
    string? ScheduledTaskFilePath = null);

public record GenerateCommandsResponse(string Commands);

public record AgentListItem(
    string Name,
    string? Description,
    string AgentType,
    string Status,
    int ToolsCount,
    bool HasTrigger,
    string UpdatedAt);

public record ListAgentsResponse(int Count, List<AgentListItem> Agents);

public record AddNoteResponse(bool Success, string Message, int NotesCount);

public record SessionSummaryResponse(
    int AgentsInProgress,
    List<string> AgentNames,
    int SubagentsInProgress,
    List<string> SubagentNames,
    int KnowledgeEntries,
    List<string> RecentActions,
    Dictionary<string, string> Preferences,
    string PersistencePath,
    bool PersistenceEnabled);

public record ValidationResponse(
    bool Valid,
    List<string> Issues,
    List<string> Warnings,
    string Status);

#endregion

#region Subagent Building Responses

public record SubagentInfo(
    string Name,
    string ClassName,
    string? Description,
    string Status);

public record StartSubagentBuildResponse(
    bool Success,
    string Message,
    SubagentInfo Subagent,
    List<string> NextSteps);

public record ToolMethodInfo(
    string MethodName,
    string KernelFunctionName,
    string Description,
    string ReturnType,
    int ParametersCount);

public record AddToolResponse(
    bool Success,
    string Message,
    ToolMethodInfo Tool);

public record AddDependencyResponse(bool Success, string Message);

public record ConfigureDelegationResponse(bool Success, string Message);

public record GenerateCodeResponse(
    string Code,
    string FilePath,
    string? Namespace,
    string ClassName);

public record SubagentListItem(
    string Name,
    string ClassName,
    string? Description,
    string Status,
    int ToolsCount,
    int DependenciesCount,
    string UpdatedAt);

public record ListSubagentsResponse(int Count, List<SubagentListItem> Subagents);

public record SubagentContextDto(
    string Name,
    string ClassName,
    string? Namespace,
    string? Description,
    string? SystemPrompt,
    string Status,
    List<ToolMethodDto> Tools,
    List<DependencyDto> Dependencies,
    List<string> DelegatesTo,
    List<string> Notes,
    string CreatedAt,
    string UpdatedAt);

public record ToolMethodDto(
    string MethodName,
    string KernelFunctionName,
    string Description,
    string ReturnType,
    List<ParameterDto> Parameters);

public record ParameterDto(
    string Name,
    string Type,
    string? Description);

public record DependencyDto(
    string InterfaceType,
    string FieldName,
    string? Description);

public record PlatformSubagentExample(
    string Name,
    string Description,
    string Pattern);

public record PlatformSubagentsReferenceResponse(
    string Documentation,
    List<PlatformSubagentExample> Examples);

#endregion

#region Knowledge Base Responses

public record KnowledgeEntryInfo(
    string Id,
    string Category,
    string Title,
    List<string> Tags);

public record LearnTopicResponse(
    bool Success,
    string Message,
    string Category,
    string Title,
    string? UploadedFileName);

public record KnowledgeSearchResult(
    string Id,
    string Category,
    string Title,
    string Content,
    List<string> Tags,
    string CreatedAt);

public record SearchKnowledgeResponse(
    int Count,
    List<KnowledgeSearchResult> Results);

public record ListKnowledgeResponse(
    int Count,
    List<KnowledgeSearchResult> Entries,
    List<string> Categories);

#endregion

#region Deep Dive & Options Responses

public record DeepDiveStep(
    int Step,
    string Title,
    string? Description = null,
    string? Code = null,
    string? Yaml = null,
    string? Cli = null,
    string? Diagram = null);

public record DeepDiveExample(
    string Title,
    string Description,
    List<DeepDiveStep>? Steps = null,
    string? Yaml = null,
    string? Cli = null,
    string? ToolsYaml = null,
    string? AgentYaml = null,
    string? IncidentHandlerCli = null,
    string? ScheduledTaskYaml = null,
    string? ManualCli = null);

public record OptionDoc(
    string Name,
    string Type,
    string Default,
    string Description);

public record AllOptionsResponse(
    string ConfigType,
    string? YamlKind = null,
    List<OptionDoc>? Options = null,
    List<OptionDoc>? CommonOptions = null,
    List<OptionDoc>? ParameterOptions = null,
    List<string>? ToolTypes = null,
    string? AvailablePackages = null,
    string? CliCommand = null,
    object? CronExamples = null,
    string? BaseClass = null,
    List<OptionDoc>? RequiredOverrides = null,
    List<OptionDoc>? OptionalOverrides = null,
    List<OptionDoc>? ToolAttributes = null,
    string? DiRegistration = null,
    List<object>? Types = null,
    string? Description = null,
    List<object>? CommonConnectors = null);

public record AllOptionsReferenceResponse(
    string Title,
    string Description,
    AllOptionsResponse AgentOptions,
    AllOptionsResponse ToolOptions,
    AllOptionsResponse TriggerOptions,
    AllOptionsResponse ScheduledTaskOptions);

#endregion

#region Prompt Engineering Responses

public record PromptAnalysisResponse(
    string OriginalPrompt,
    List<string> Strengths,
    List<string> Weaknesses,
    List<string> Suggestions,
    string? ImprovedPrompt,
    List<PromptPattern> ApplicablePatterns);

public record PromptPattern(
    string Name,
    string Description,
    string Example,
    string WhenToUse);

public record PromptTemplateResponse(
    string TemplateName,
    string Template,
    List<string> Placeholders,
    string UsageExample,
    List<string> BestPractices);

public record E2EWorkflowResponse(
    string WorkflowName,
    string Description,
    List<WorkflowStep> Steps,
    string MermaidDiagram,
    List<string> ValidationChecklist,
    Dictionary<string, string> GeneratedFiles);

public record WorkflowStep(
    int StepNumber,
    string Title,
    string Description,
    string? YamlSnippet,
    string? CliCommand,
    string? CodeSnippet,
    List<string> Tips);

public record PromptTestResult(
    string TestName,
    string Prompt,
    string ExpectedBehavior,
    List<string> TestScenarios,
    string MockResponse,
    List<string> EdgeCases);

public record WorkspaceFileResponse(
    bool Success,
    string FilePath,
    string Content,
    string FileType,
    List<string> NextSteps);

#endregion

