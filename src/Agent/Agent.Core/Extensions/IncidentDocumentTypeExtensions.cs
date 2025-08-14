using Agent.Core.Enums;

namespace Agent.Core.Extensions;

/// <summary>
/// Extension methods for IncidentDocumentType enum
/// </summary>
public static class IncidentDocumentTypeExtensions
{
    /// <summary>
    /// Converts a string document type to IncidentDocumentType enum
    /// </summary>
    /// <param name="documentType">The string representation of the document type</param>
    /// <returns>The corresponding IncidentDocumentType enum value</returns>
    /// <exception cref="NotSupportedException">Thrown when the document type is not supported</exception>
    public static IncidentDocumentType ToIncidentDocumentType(this string documentType)
    {
        return documentType switch
        {
            "ServiceNowIncident" => IncidentDocumentType.ServiceNowIncident,
            "PagerDutyIncident" => IncidentDocumentType.PagerDutyIncident,
            "IcmIncident" => IncidentDocumentType.IcmIncident,
            "AzureMonitorIncident" => IncidentDocumentType.AzureMonitorIncident,
            _ => throw new NotSupportedException($"Incident document type '{documentType}' is not supported.")
        };
    }

    /// <summary>
    /// Converts IncidentDocumentType enum to its string representation
    /// </summary>
    /// <param name="documentType">The IncidentDocumentType enum value</param>
    /// <returns>The string representation of the document type</returns>
    /// <exception cref="NotSupportedException">Thrown when the document type is not supported</exception>
    public static string ToDocumentTypeString(this IncidentDocumentType documentType)
    {
        return documentType switch
        {
            IncidentDocumentType.ServiceNowIncident => "ServiceNowIncident",
            IncidentDocumentType.PagerDutyIncident => "PagerDutyIncident",
            IncidentDocumentType.IcmIncident => "IcmIncident",
            IncidentDocumentType.AzureMonitorIncident => "AzureMonitorIncident",
            _ => throw new NotSupportedException($"Incident document type '{documentType}' is not supported.")
        };
    }

    /// <summary>
    /// Maps IncidentDocumentType to the corresponding platform name for incident handler tools
    /// </summary>
    /// <param name="documentType">The IncidentDocumentType enum value</param>
    /// <returns>The platform name used for incident handler tools</returns>
    /// <exception cref="NotSupportedException">Thrown when the document type is not supported</exception>
    public static string ToPlatformName(this IncidentDocumentType documentType)
    {
        return documentType switch
        {
            IncidentDocumentType.ServiceNowIncident => Constants.IncidentPlatforms.ServiceNow,
            IncidentDocumentType.PagerDutyIncident => Constants.IncidentPlatforms.PagerDuty,
            IncidentDocumentType.IcmIncident => Constants.IncidentPlatforms.ICM,
            IncidentDocumentType.AzureMonitorIncident => Constants.IncidentPlatforms.AzureMonitor,
            _ => throw new NotSupportedException($"Incident document type '{documentType}' is not supported for platform mapping.")
        };
    }
}
