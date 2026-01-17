// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;

namespace Agent.Framework.Skills;

/// <summary>
/// Implementation of ISkill representing a domain-specific knowledge package
/// </summary>
public class Skill : ISkill
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;

    public List<string> Tools { get; init; } = [];

    public bool FirstPartyOnly { get; init; } = false;

    public static Skill FromDescriptor(YamlSkillDescriptor descriptor, string directoryPath)
    {
        return new Skill
        {
            Name = descriptor.Name,
            Description = descriptor.Description,
            DirectoryPath = directoryPath,
            Tools = descriptor.Tools ?? [],
            FirstPartyOnly = descriptor.FirstPartyOnly
        };
    }
}

public class SkillList : ConcurrentQueue<ISkill>
{
    public SkillList() : base() { }

    public SkillList(IEnumerable<ISkill> skills) : base(skills) { }

    public IEnumerable<string> AllToolNames => this.SelectMany(skill => skill.Tools).Distinct();

    public ISkill? GetSkillByName(string name)
    {
        return this.FirstOrDefault(skill => skill.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
