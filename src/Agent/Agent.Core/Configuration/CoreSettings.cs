using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration
{
    public class CoreSettings
    {
        [Required]
        public AzureSettings Azure { get; set; } = new();

        [Required]
        public ExternalSettings External { get; set; } = new();
    }
}
