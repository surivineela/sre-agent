// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework.Skills;

/// <summary>
/// Represents a skill that provides domain-specific knowledge and expertise to an agent.
/// Skills are loaded progressively based on agent needs.
/// </summary>
public interface ISkill
{
    /// <summary>
    /// Unique name of the skill (e.g., "kubernetes_skill")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of the skill's domain expertise
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Directory path where skill files are located
    /// </summary>
    string DirectoryPath { get; }

    /// <summary>
    /// Optional list of tools that should be available when this skill is active.
    /// Used during migration phase; eventually skills won't specify tools.
    /// </summary>
    List<string> Tools { get; }
}
