// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Data.DataModels;
using Agent.Web.ApiResources;
using Agent.Web.Json;

namespace Agent.Web.Views.v2;

/// <summary>
/// View model for code repository API endpoints.
/// </summary>
public class CodeRepoView
{
    /// <summary>
    /// Repository URL (will be normalized during validation).
    /// </summary>
    public Settable<string> Url { get; set; }

    /// <summary>
    /// Repository type (GitHub or AzureDevOps). Auto-detected if not provided.
    /// </summary>
    public Settable<string> Type { get; set; }

    /// <summary>
    /// Scanning status of the repository (read-only, not persisted).
    /// </summary>
    public ScanStatus? ScanStatus { get; set; }

    /// <summary>
    /// Name of the authentication connector used for this repository (read-only, not persisted).
    /// </summary>
    public string? AuthConnectorName { get; set; }

    /// <summary>
    /// Creates an API response envelope from a code repository document model.
    /// </summary>
    /// <param name="repoDoc">The repository document model.</param>
    /// <param name="authConnectorName">The name of the authentication connector (optional).</param>
    /// <returns>API response envelope.</returns>
    public static ApiResponseEnvelope<CodeRepoView> CreateApiResponseEnvelope(
        CodeRepoDocumentModel repoDoc,
        string? authConnectorName = null)
    {
        var repoView = new CodeRepoView
        {
            Url = repoDoc.Spec.Url,
            Type = repoDoc.Spec.Type.ToString(),
            ScanStatus = Agent.Core.Models.ScanStatus.NotScanned, // Placeholder until scan logic is implemented
            AuthConnectorName = authConnectorName
        };

        var apiResponse = new ApiResponseEnvelope<CodeRepoView>
        {
            Name = repoDoc.Name,
            Type = repoDoc.DocumentType,
            Tags = repoDoc.Metadata.Tags,
            Properties = repoView,
        };

        return apiResponse;
    }

    /// <summary>
    /// Creates a code repository document model from an API request envelope.
    /// </summary>
    /// <param name="envelope">The API request envelope.</param>
    /// <param name="metadata">Optional existing metadata to preserve.</param>
    /// <param name="baseModel">Optional base model for updates.</param>
    /// <returns>Code repository document model.</returns>
    public static CodeRepoDocumentModel CreateModel(
        ApiRequestEnvelope<CodeRepoView> envelope,
        ResourceMetadata? metadata = null,
        CodeRepoDocumentModel? baseModel = null)
    {
        var result = baseModel ?? new CodeRepoDocumentModel(
            new ResourceMetadata
            {
                CreatedAt = DateTime.UtcNow,
            },
            new CodeRepoSpec
            {
                Url = string.Empty,
                Type = RepoType.AzureDevOps // Default, will be auto-detected during validation
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
        envelope.Tags.ApplyTo(tags => result.Metadata.Tags = tags);
        envelope.Properties.ApplyTo(properties =>
        {
            if (properties == null)
            {
                return;
            }

            properties.Url.ApplyTo(value =>
            {
                if (value != null)
                {
                    result.Spec.Url = value;
                }
            });

            properties.Type.ApplyTo(value =>
            {
                if (value != null && Enum.TryParse<RepoType>(value, ignoreCase: true, out var repoType))
                {
                    result.Spec.Type = repoType;
                }
            });
        });

        return result;
    }
}
