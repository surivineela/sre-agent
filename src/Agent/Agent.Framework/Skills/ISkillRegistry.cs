// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework.Skills;

/// <summary>
/// Registry for managing agent skills - loading, discovering, and reading skill files.
/// </summary>
public interface ISkillRegistry : IAsyncInitializer
{

    /// <summary>
    /// Gets formatted skill metadata for injection into agent system prompt
    /// </summary>
    /// <param name="includeSystemSkills">Whether to include system skills in the listing</param>
    /// <returns>Formatted text listing all available skills</returns>
    string GetSkillsMetadataForPrompt(bool includeSystemSkills);

    /// <summary>
    /// Reads a file from a skill directory (exposed as a tool to the agent)
    /// </summary>
    /// <param name="skillName">Name of the skill</param>
    /// <param name="filePath">Relative path to the file within the skill directory</param>
    /// <param name="includeSystemSkills">Whether to include system skills in the search</param>
    /// <returns>File contents or error message</returns>
    string ReadSkillFile(string skillName, string filePath, bool includeSystemSkills);

    /// <summary>
    /// Gets a skill by its name
    /// </summary>
    /// <param name="name">Name of the skill</param>
    /// <param name="includeSystemSkills">Whether to include system skills in the search</param>
    ISkill? GetSkillByName(string name, bool includeSystemSkills);
}
