using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration
{
    public class CosmosDBSettings
    {
        [Required]
        public GraphSettings Graph { get; set; } = new();
    }
}
