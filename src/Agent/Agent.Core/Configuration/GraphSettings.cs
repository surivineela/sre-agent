using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration
{
    public class GraphSettings
    {
        [Required]
        public string AccountName { get; set; } = string.Empty;

        [Required]
        public string ApiKey { get; set; } = string.Empty;

        [Required]
        public string Database { get; set; } = string.Empty;

        [Required]
        public string Collection { get; set; } = string.Empty;
    }
}
