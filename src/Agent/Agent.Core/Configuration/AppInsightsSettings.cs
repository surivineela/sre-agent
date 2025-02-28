using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration
{
    public class AppInsightsSettings
    {
        [Required]
        public string ConnectionString { get; set; } = string.Empty;
    }
}
