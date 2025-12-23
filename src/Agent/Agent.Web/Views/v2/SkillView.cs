// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Framework.Skills;
using Agent.Web.ApiResources;
using Agent.Web.Json;

namespace Agent.Web.Views.v2;

public class SkillView
{
    public Settable<string> Description { get; set; }

    public Settable<List<string>> Tools { get; set; }

    public Settable<string> SkillContent { get; set; }

    public Settable<List<SkillSubFileView>> AdditionalFiles { get; set; }

    public static ApiResponseEnvelope<SkillView> CreateApiResponseEnvelope(SkillDocumentModel skillDoc)
    {
        var skill = skillDoc.ToRuntimeModel();
        var skillView = new SkillView
        {
            Description = skill.Description,
            Tools = skill.Tools,
            SkillContent = skill.SkillMdContent,
            AdditionalFiles = skill.AdditionalFiles?.Select(f => new SkillSubFileView
            {
                FilePath = f.FilePath,
                Content = f.Content
            }).ToList()
        };

        ApiResponseEnvelope<SkillView> apiResponse = new()
        {
            Name = skill.Name,
            Type = skillDoc.DocumentType,
            Tags = skillDoc.Metadata.Tags,
            Properties = skillView,
        };

        return apiResponse;
    }

    public static SkillDocumentModel CreateModel(ApiRequestEnvelope<SkillView> envelope, ResourceMetadata? metadata = null, SkillDocumentModel? baseModel = null)
    {
        var result = baseModel ?? new SkillDocumentModel(
            Metadata: metadata ?? new ResourceMetadata(),
            Spec: new SkillSpec()
        );

        if (metadata != null)
        {
            result = result with
            {
                Metadata = metadata,
            };
        }

        result.Metadata.UpdatedAt = DateTime.UtcNow;

        envelope.Name.ApplyTo(name => result.Metadata.Name = name!);
        envelope.Tags.ApplyTo(tag => result.Metadata.Tags = tag);
        envelope.Properties.ApplyTo(properties =>
        {
            if (properties == null)
            {
                return;
            }

            properties.Description.ApplyTo(value => result.Spec.Description = value ?? string.Empty);
            properties.Tools.ApplyTo(value => result.Spec.Tools = value ?? []);
            properties.SkillContent.ApplyTo(value => result.Spec.SkillMdContent = value ?? string.Empty);
            properties.AdditionalFiles.ApplyTo(value =>
            {
                if (value == null)
                {
                    result.Spec.AdditionalFiles = [];
                    return;
                }

                result.Spec.AdditionalFiles = [.. value.Select(f => new SkillSubFile
                {
                    FilePath = f.FilePath!,
                    Content = f.Content!
                })];
            });
        });

        return result;
    }
}

public class SkillSubFileView
{
    public Settable<string> FilePath { get; set; }

    public Settable<string> Content { get; set; }
}
