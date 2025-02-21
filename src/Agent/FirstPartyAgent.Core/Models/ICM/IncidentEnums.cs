using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
