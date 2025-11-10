// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


namespace Agent.Framework.Skills;

/// <summary>
/// An empty skill registry implementation
/// </summary>
public class EmptySkillRegistry : ISkillRegistry
{
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public string GetSkillsMetadataForPrompt(bool includeSystemSkills)
    {
        return string.Empty;
    }

    public string ReadSkillFile(string skillName, string filePath, bool includeSystemSkills)
    {
        return string.Empty;
    }

    public ISkill? GetSkillByName(string name, bool includeSystemSkills)
    {
        return null;
    }
}
