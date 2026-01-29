// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

/// <summary>
/// Utility class for incident indexing configuration document operations.
/// </summary>
public static class IncidentIndexingConfigurationUtilities
{
    private const string DocumentIdPrefix = "incident-indexing-configuration-";
    private const string DocumentTypePrefix = "IncidentIndexingConfiguration";

    /// <summary>
    /// Gets the document ID for a given provider type.
    /// Format: incident-indexing-configuration-{provider}
    /// </summary>
    public static string GetDocumentId(IncidentManagementType providerType)
    {
        return $"{DocumentIdPrefix}{providerType.ToString().ToLowerInvariant()}";
    }

    /// <summary>
    /// Gets the document type name for a given provider type.
    /// Format: IncidentIndexingConfiguration{Provider}
    /// </summary>
    public static string GetDocumentTypeName(IncidentManagementType providerType)
    {
        return $"{DocumentTypePrefix}{providerType}";
    }

    /// <summary>
    /// Extracts the provider type from a document ID.
    /// </summary>
    public static IncidentManagementType? GetProviderTypeFromDocumentId(string documentId)
    {
        if (string.IsNullOrEmpty(documentId) || !documentId.StartsWith(DocumentIdPrefix))
        {
            return null;
        }

        var providerString = documentId.Substring(DocumentIdPrefix.Length);
        if (Enum.TryParse<IncidentManagementType>(providerString, ignoreCase: true, out var providerType))
        {
            return providerType;
        }

        return null;
    }
}
