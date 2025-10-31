// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Web.Json;
using Agent.Web.ApiResources;

namespace Agent.Web.Views.v2;

public class CommonToolListView
{
    public Settable<List<string>> CommonToolsList { get; set; }

    public static ApiResponseEnvelope<CommonToolListView> CreateApiResponseEnvelope(CommonToolsListDocumentModel toolListDoc)
    {
        var toolList = toolListDoc.Spec;
        var toolListView = new CommonToolListView
        {
            CommonToolsList = toolList.CommonToolsList
        };

        ApiResponseEnvelope<CommonToolListView> apiResponse = new()
        {
            Name = toolList.Name,
            Type = toolListDoc.DocumentType,
            Tags = toolListDoc.Metadata.Tags,
            Properties = toolListView,
        };

        return apiResponse;
    }

    public static CommonToolsListDocumentModel CreateModel(ApiRequestEnvelope<CommonToolListView> envelope, ResourceMetadata? metadata = null, CommonToolsListDocumentModel? baseModel = null)
    {
        CommonToolListSpec spec;

        if (baseModel != null)
        {
            spec = baseModel.Spec;
        }
        else
        {
            spec = new CommonToolListSpec();
        }

        var resultMetadata = metadata ?? baseModel?.Metadata ?? new ResourceMetadata
        {
            CreatedAt = DateTime.UtcNow,
            Version = "v2",
        };

        resultMetadata.UpdatedAt = DateTime.UtcNow;

        envelope.Name.ApplyTo(name => spec.Name = name!);
        envelope.Owner.ApplyTo(owner => resultMetadata.Owner = owner);
        envelope.Tags.ApplyTo(tag => resultMetadata.Tags = tag);
        envelope.Properties.ApplyTo(properties =>
        {
            if (properties == null)
            {
                return;
            }

            properties.CommonToolsList.ApplyTo(value => spec.CommonToolsList = value ?? new List<string>());
        });

        return new CommonToolsListDocumentModel(resultMetadata, spec);
    }
}
