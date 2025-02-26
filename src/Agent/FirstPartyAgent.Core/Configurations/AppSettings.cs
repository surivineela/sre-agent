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

public class ICMWorkflowSettings
{
    [Required]
    public string WorkflowsEndpoint { get; set; } = string.Empty;
    public string CertificateSubjectName { get; set; } = string.Empty;
    public string UserToken { get; set; } = string.Empty;
    public bool UseFunctionApp { get; set; } = false;
    public string FunctionAppEndpoint { get; set; } = string.Empty;
    public string FunctionAppKey { get; set; } = string.Empty;
    public string GetIncidentWorkflowName { get; set; } = string.Empty;
    public string GetIncidentDiscussionEntriesWorkflowName { get; set; } = string.Empty;
    public string TransferIncidentWorkflowName { get; set; } = string.Empty;
    public string UpdateIncidentWorkflowName { get; set; } = string.Empty;
    public string MitigateIncidentWorkflowName { get; set; } = string.Empty;
    public string ResolveIncidentWorkflowName { get; set; } = string.Empty;
    public string PostIncidentDiscussionWorkflowName { get; set; } = string.Empty;
    public string DowngradeSev2WorkflowName { get; set; } = string.Empty;
    public string MarkSubscriptionFirstPartyWorkflowName { get; set; } = string.Empty;
    public string GetSubscriptionWorkflowName { get; set; } = string.Empty;
    public string AddIncidentTagWorkflowName { get; set; } = string.Empty;
    public string ApplensPluginWorkflowName { get; set; } = string.Empty;
    public string HumanInterventionServiceName { get; set; } = string.Empty;
    public string HumanInterventionTeamName { get; set; } = string.Empty;
    public bool ReadOnly { get; set; } = false;
}

public class ICMAPISettings
{
    [Required]
    public string APIEndpoint { get; set; } = string.Empty;
    public string CertificateSubjectName { get; set; } = string.Empty;
    public string UserToken { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; } = false;
}