using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models
{
    public class KustoCluster
    {
        [Required]
        public string Region { get; set; }
        [Required]
        public string ClusterUri { get; set; }
        [Required]
        public string Database { get; set; }
    }
}
