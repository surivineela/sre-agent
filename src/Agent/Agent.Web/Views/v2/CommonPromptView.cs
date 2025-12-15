// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Web.ApiResources;
using Agent.Web.Json;

namespace Agent.Web.Views.v2;

public class CommonPromptView
{
    public Settable<string> Prompt { get; set; }

    public static ApiResponseEnvelope<CommonPromptView> CreateApiResponseEnvelope(CommonPromptDocumentModel promptDoc)
    {
        var prompt = promptDoc.Spec;
        var promptView = new CommonPromptView
        {
            Prompt = prompt.Prompt
        };

        ApiResponseEnvelope<CommonPromptView> apiResponse = new()
        {
            Name = promptDoc.Name,
            Type = promptDoc.DocumentType,
            Tags = promptDoc.Metadata.Tags,
            Properties = promptView,
        };

        return apiResponse;
    }

    public static CommonPromptDocumentModel CreateModel(ApiRequestEnvelope<CommonPromptView> envelope, ResourceMetadata? metadata = null, CommonPromptDocumentModel? baseModel = null)
    {
        CommonPromptSpec spec;

        if (baseModel != null)
        {
            spec = baseModel.Spec;
        }
        else
        {
            spec = new CommonPromptSpec();
        }

        var resultMetadata = metadata ?? baseModel?.Metadata ?? new ResourceMetadata
        {
            CreatedAt = DateTime.UtcNow,
        };

        resultMetadata.UpdatedAt = DateTime.UtcNow;

        envelope.Name.ApplyTo(name => resultMetadata.Name = name!);
        envelope.Tags.ApplyTo(tag => resultMetadata.Tags = tag);
        envelope.Properties.ApplyTo(properties =>
        {
            if (properties == null)
            {
                return;
            }

            properties.Prompt.ApplyTo(value => spec.Prompt = value ?? string.Empty);
        });

        return new CommonPromptDocumentModel(resultMetadata, spec);
    }
}
