// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Web.ApiResources;
using Agent.Web.Json;

namespace Agent.Web.Views.v2;

public class PluginConfigView
{
    public Settable<IDictionary<string, object>> Config { get; set; }

    public static ApiResponseEnvelope<PluginConfigView> CreateApiResponseEnvelope(PlugInConfigDocumentModel pluginDoc)
    {
        var plugin = pluginDoc.Spec;
        var pluginView = new PluginConfigView
        {
            Config = new Settable<IDictionary<string, object>>(plugin.Config)
        };

        ApiResponseEnvelope<PluginConfigView> apiResponse = new()
        {
            Name = plugin.Name,
            Type = pluginDoc.DocumentType,
            Tags = pluginDoc.Metadata.Tags,
            Properties = pluginView,
        };

        return apiResponse;
    }

    public static PlugInConfigDocumentModel CreateModel(ApiRequestEnvelope<PluginConfigView> envelope, ResourceMetadata? metadata = null, PlugInConfigDocumentModel? baseModel = null)
    {
        PluginConfigSpec spec;

        if (baseModel != null)
        {
            spec = baseModel.Spec;
        }
        else
        {
            spec = new PluginConfigSpec();
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

            properties.Config.ApplyTo(value => spec.Config = value ?? new Dictionary<string, object>());
        });

        return new PlugInConfigDocumentModel(resultMetadata, spec);
    }
}
