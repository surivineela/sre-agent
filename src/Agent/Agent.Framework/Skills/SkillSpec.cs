// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Framework.Skills;

public class SkillSpec : YamlSkillDescriptor
{
    [YamlMember(Alias = "skill_md_content")]
    public string SkillMdContent { get; set; } = string.Empty;

    [YamlMember(Alias = "additional_files")]
    public List<SkillSubFile> AdditionalFiles { get; set; } = [];

    [YamlIgnore]
    public string DirectoryPath { get; set; } = string.Empty;
}

public class SkillSubFile
{
    /// <summary>
    /// The path to the file within the skill directory, including the file name.
    /// </summary>
    [YamlMember(Alias = "file_path")]
    public required string FilePath { get; set; }

    /// <summary>
    /// The content of the file.
    /// </summary>
    [YamlMember(Alias = "content")]
    public required string Content { get; set; }
}
