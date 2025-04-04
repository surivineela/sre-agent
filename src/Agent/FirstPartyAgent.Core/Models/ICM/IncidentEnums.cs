// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Models
{
    public enum IncidentStatus
    {
        Active,
        Mitigated,
        Resolved,
        Correlating,
        Holding
    }

    public enum IncidentEnvironment
    {
        DOGFOOD,
        INT,
        PPE,
        PROD,
        STAGING,
        TEST
    }

    public enum IncidentType
    {
        CustomerReported,
        LiveSite,
        Deployment
    }
}

