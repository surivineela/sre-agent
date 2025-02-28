using System.ComponentModel.DataAnnotations;

namespace FirstPartyAgent.ACA.Web.Configuration
{
    public class TaskStorageSettings
    {
        public string FilePath { get; set; } = string.Empty;
    }

    public class SREAgentSettings
    {
        public TaskStorageSettings TaskStorage { get; set; }
    }
}
