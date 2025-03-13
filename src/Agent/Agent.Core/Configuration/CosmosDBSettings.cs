using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class CosmosDBSettings
    {
        [Required]
        public GraphSettings Graph { get; set; } = new();

        [Required]
        public DocsSettings Docs { get; set; } = new();
    }
}
