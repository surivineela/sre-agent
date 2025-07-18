// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{

    public class BaseIcmWorkflowSettings
    {
        [Required]
        public string WorkflowsEndpoint { get; set; } = string.Empty;
        public string CertificateSubjectName { get; set; } = string.Empty;
        public string CertificateFilePath { get; set; } = string.Empty;
        public string UserToken { get; set; } = string.Empty;
        public bool UseFunctionApp { get; set; } = false;
        public string FunctionAppEndpoint { get; set; } = string.Empty;
        public string FunctionAppKey { get; set; } = string.Empty;
        public bool ReadOnly { get; set; } = false;
    }

    public class ICMWorkflowSettings
    {
        [Required]
        public string WorkflowsEndpoint { get; set; } = string.Empty;
        public string CertificateSubjectName { get; set; } = string.Empty;
        public string CertificateFilePath { get; set; } = string.Empty;
        public string CertificateKeyVaultUri { get; set; } = string.Empty;
        public string ManagedIdentityClientId { get; set; } = string.Empty;
        public string CertificateKeyVaultSecretName { get; set; } = string.Empty;
        public string UserToken { get; set; } = string.Empty;
        public bool UseFunctionApp { get; set; } = false;
        public string FunctionAppEndpoint { get; set; } = string.Empty;
        public string FunctionAppKey { get; set; } = string.Empty;
        public string GetIncidentWorkflowName { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string AddIncidentAttachmentWorkflowName { get; set; } = string.Empty;
        public string GetIncidentDiscussionEntriesWorkflowName { get; set; } = string.Empty;
        public string TransferIncidentWorkflowName { get; set; } = string.Empty;
        public string UpdateIncidentWorkflowName { get; set; } = string.Empty;
        public string MitigateIncidentWorkflowName { get; set; } = string.Empty;
        public string ResolveIncidentWorkflowName { get; set; } = string.Empty;
        public string PostIncidentDiscussionWorkflowName { get; set; } = string.Empty;
        public string DowngradeSev2WorkflowName { get; set; } = string.Empty;
        public string MarkSubscriptionFirstPartyWorkflowName { get; set; } = string.Empty;
        public string GetSubscriptionWorkflowName { get; set; } = string.Empty;
        public string SubscriptionDetailWorkflowName { get; set; } = string.Empty;
        public string SubscriptionUsageWorkflowName { get; set; } = string.Empty;
        public string RestartWebAppWorkflowName { get; set; } = string.Empty;
        public string RebootWorkerWorkflowName { get; set; } = string.Empty;
        public string IncidentLookupWorkflowName { get; set; } = string.Empty;
        public string RedisTenantId { get; set; } = string.Empty;
        public string RedisDeploymentDetailsWorkflowName { get; set; } = string.Empty;
        public string RedisDeploymentHistoryWorkflowName { get; set; } = string.Empty;
        public string AddIncidentTagWorkflowName { get; set; } = string.Empty;
        public string AddIncidentKeywordsWorkflowName { get; set; } = string.Empty;
        public string ApplensPluginWorkflowName { get; set; } = string.Empty;
        public string HumanInterventionServiceName { get; set; } = string.Empty;
        public string HumanInterventionTeamName { get; set; } = string.Empty;
        public bool ReadOnly { get; set; } = false;
        public bool ProcessImages { get; set; } = true;
        public bool ICMBacktestingModeEnabled { get; set; } = false;
        public bool Enabled { get; set; } = false;
    }
}

