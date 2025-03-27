using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Configuration
{
    public class ObserverClientSettings
    {
        public bool IsEnabled { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string CertificateSubjectName { get; set; } = string.Empty;
        public string UserAuthClientId { get; set; }
        public bool UserAuth { get; set; }
    }
}
