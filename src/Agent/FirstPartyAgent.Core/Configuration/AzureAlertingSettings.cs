// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Configuration
{
    public class AzureAlertingSettings
    {
        public string Endpoint { get; set; } = "https://azurealertingfunctions.azurewebsites.net/";
        public string CertificateSubjectName { get; set; } = string.Empty;
        public string UserToken { get; set; } = string.Empty;
    }
}

