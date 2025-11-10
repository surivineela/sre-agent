// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Skills;

namespace Agent.Framework;

public interface IExtensibilityLoader
{
    Task<List<YamlCommonToolsDescriptor>> LoadExtendedCommonToolsListsAsync(CancellationToken cancellationToken = default);

    Task<List<YamlPromptDescriptor>> LoadExtendedCommonPromptsAsync(CancellationToken cancellationToken = default);

    Task<List<YamlToolDefinitionBase>> LoadExtendedToolsAsync(CancellationToken cancellationToken = default);

    Task<List<YamlAgentDescriptor>> LoadExtendedAgentsAsync(CancellationToken cancellationToken = default);

    Task<List<SkillSpec>> LoadExtendedSkillsAsync(CancellationToken cancellationToken = default);
}
