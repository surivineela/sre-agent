using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Configuration
{
    public class ICMSettings
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
}
