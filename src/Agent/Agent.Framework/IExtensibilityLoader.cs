namespace Agent.Framework.Interfaces;

public interface IExtensibilityLoader :IAsyncInitializer    
{
    Task<List<YamlCommonToolsDescriptor>> LoadExtendedCommonToolsListsAsync(CancellationToken cancellationToken = default);

    Task<List<YamlPromptDescriptor>> LoadExtendedCommonPromptsAsync(CancellationToken cancellationToken = default);

    Task<List<YamlToolDefinitionBase>> LoadExtendedToolsAsync(CancellationToken cancellationToken = default);
    Task<List<YamlPluginConfig>> LoadExtendedPluginConfigsAsync(CancellationToken cancellationToken = default);

    Task<List<YamlAgentDescriptor>> LoadExtendedAgentsAsync(CancellationToken cancellationToken = default);
}
