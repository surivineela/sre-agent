using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Configuration
{
    public class AzureAlertingSettings
    {
        public string Endpoint { get; set; } = "https://azurealertingfunctions.azurewebsites.net/";
        public string CertificateSubjectName { get; set; } = string.Empty;
        public string UserToken { get; set; } = string.Empty;
    }
}
