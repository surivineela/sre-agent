// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Web.ApiResources;
using Agent.Web.Json;

namespace Agent.Web.Views.v2;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KustoConnectorView), ConnectorDocumentModel.KustoConnectorType)]
public class ConnectorView
{
    [JsonIgnore] // this is a must because type is already be serialized due to polymorphic deserialization
    public Settable<string> Type { get; set; }

    public Settable<string> Description { get; set; }

    public Settable<ConnectorAuthSettings> Auth { get; set; }

    public Settable<bool> Enabled { get; set; }

    public static ApiResponseEnvelope<ConnectorView> CreateApiResponseEnvelope(ConnectorDocumentModel connectorDoc)
    {
        ConnectorView connectorView;
        switch (connectorDoc)
        {
            case KustoConnectorDocumentModel kustoConnectorDoc:
                connectorView = KustoConnectorView.CreateApiResponseEnvelope(kustoConnectorDoc).Properties!;
                break;
            default:
                throw new NotSupportedException($"Unsupported connector type: {connectorDoc.Type}");
        }

        return new ApiResponseEnvelope<ConnectorView>
        {
            Name = connectorDoc.Name,
            Type = connectorDoc.DocumentType,
            Tags = connectorDoc.Metadata.Tags,
            Properties = connectorView,
        };
    }

    public static ConnectorDocumentModel CreateModel(ApiRequestEnvelope<ConnectorView> envelope, ResourceMetadata? metadata = null, ConnectorDocumentModel? baseModel = null)
    {
        var result = baseModel ?? new ConnectorDocumentModel(
            new ResourceMetadata
            {
                CreatedAt = DateTime.UtcNow,
            },
            new ConnectorSpec()
            {
                Type = ConnectorDocumentModel.KustoConnectorType, // This is a must because KustoConnectorView.Type is always not set due to polymorphic deserialization
            }
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

            properties.Type.ApplyTo(value => result.Spec.Type = value!);
            properties.Description.ApplyTo(value => result.Spec.Description = value!);
            properties.Auth.ApplyTo(value => result.Spec.Auth = value!);
            properties.Enabled.ApplyTo(value => result.Spec.Enabled = value);
        });

        return result;
    }
}

public class KustoConnectorView : ConnectorView
{
    public Settable<string> ClusterUrl { get; set; }

    public Settable<string> Database { get; set; }

    public Settable<string> ClusterHint { get; set; }

    public Settable<List<KustoRegionalGroupSettings>> RegionalClusterGroups { get; set; }

    public static ApiResponseEnvelope<KustoConnectorView> CreateApiResponseEnvelope(KustoConnectorDocumentModel connectorDoc)
    {
        var connector = connectorDoc.Spec;
        var connectorView = new KustoConnectorView();

        connectorView.Type = connector.Type;
        connectorView.Description = connector.Description;
        connectorView.Auth = connector.Auth;
        connectorView.Enabled = connector.Enabled;
        connectorView.ClusterUrl = connector.ClusterUrl;
        connectorView.Database = connector.Database;
        connectorView.ClusterHint = connector.ClusterHint;
        connectorView.RegionalClusterGroups = connector.RegionalClusterGroups;

        ApiResponseEnvelope<KustoConnectorView> apiResponse = new()
        {
            Name = connectorDoc.Name,
            Type = connectorDoc.DocumentType,
            Tags = connectorDoc.Metadata.Tags,
            Properties = connectorView,
        };

        return apiResponse;
    }

    public static KustoConnectorDocumentModel CreateModel(ApiRequestEnvelope<KustoConnectorView> envelope, ResourceMetadata? metadata = null, KustoConnectorDocumentModel? baseModel = null)
    {
        var result = baseModel ?? new KustoConnectorDocumentModel(
            new ResourceMetadata
            {
                CreatedAt = DateTime.UtcNow,
            },
            new KustoConnectorSpec()
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

            properties.Type.ApplyTo(value => result.Spec.Type = value!);
            properties.Description.ApplyTo(value => result.Spec.Description = value!);
            properties.Auth.ApplyTo(value => result.Spec.Auth = value!);
            properties.Enabled.ApplyTo(value => result.Spec.Enabled = value);
            properties.ClusterUrl.ApplyTo(value => result.Spec.ClusterUrl = value!);
            properties.Database.ApplyTo(value => result.Spec.Database = value!);
            properties.ClusterHint.ApplyTo(value => result.Spec.ClusterHint = value);
            properties.RegionalClusterGroups.ApplyTo(value => result.Spec.RegionalClusterGroups = value!);
        });

        return result;
    }
}
