// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Configuration
{
    public class ObserverClientSettings
    {
        public bool IsEnabled { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string CertificateSubjectName { get; set; } = string.Empty;
        public string UserAuthClientId { get; set; } = string.Empty;
        public bool UserAuth { get; set; }
    }
}

