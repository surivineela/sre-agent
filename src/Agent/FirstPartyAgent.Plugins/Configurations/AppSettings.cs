// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace FirstPartyAgent.Configuration;

public class TaskStorageSettings
{
    public string FilePath { get; set; } = string.Empty;
}

public class SREAgentSettings
{
    [Required]
    public TaskStorageSettings? TaskStorage { get; set; }
}

public class IcmSettings
{
    [Required]
    public string ServiceId { get; set; } = string.Empty;
    [Required]
    public string Endpoint { get; set; } = string.Empty;
    public string CertificateSubjectName { get; set; } = string.Empty;
    public string CertificateFilePath { get; set; } = string.Empty;
    public string UserToken { get; set; } = string.Empty;
    public string PostIncidentDiscussionUrl { get; set; } = string.Empty;
}