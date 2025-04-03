// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Models;

namespace FirstPartyAgent.ACA.Web.Models
{
    public class ProcessQuotaIncidentRequest
    {
        public string? IncidentId { get; set; }

        public string? Title { get; set; }

        public string? Summary { get; set; }

        public IList<ConversationEntry>? Discussions { get; set; }
    }
}

