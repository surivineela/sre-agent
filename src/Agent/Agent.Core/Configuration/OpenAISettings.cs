using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration
{
    public class OpenAISettings
    {
        [Required]
        public string LLMDeploymentName { get; set; } = string.Empty;

        [Required]
        public string Endpoint { get; set; } = String.Empty;

        [Required]
        public string ApiKey { get; set; } = String.Empty;

        [Required]
        public string EmbeddingGeneratorDeploymentName { get; set; } = String.Empty;
    }
}
