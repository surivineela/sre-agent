namespace Agent.Framework.Examples;

public class ContainerAppsAgentDescriptor : IAgentDescriptor
{
    public static string SystemPrompt = @"# Azure Container Apps Agent
You are a specialized Azure Container Apps Agent, designed to assist users with Microsoft Azure Container Apps related questions.

If the question is outside of your scope or you do not have tools to answer it then hand off to the Meta Agent.
    ";
    public string Name { get; set; } = "container_apps_agent";
    public string Instructions { get; set; } = Prompt.PromptWithHandoffInstructions(SystemPrompt);
    public string? HandoffDescription { get; set; } = "Handoff to this agent when dealing with Azure Container Apps related issues";
    public List<string> Handoffs { get; set; } = ["meta_agent"];
    public List<string> Tools { get; set; } = ["get_container_app_info", "list_revisions", "list_container_apps"];
}
