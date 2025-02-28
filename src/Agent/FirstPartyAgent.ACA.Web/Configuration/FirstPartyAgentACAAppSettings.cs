using FirstPartyAgent.Core.Configuration;

namespace FirstPartyAgent.ACA.Web.Configuration
{
    /// <summary>
    /// Leaving this up to you, but I recommend refactoring the ACA-specific settings if there is currently only one and it is just to override a local storage path which only works for linux.
    /// </summary>
    public class FirstPartyAgentACAAppSettings : FirstPartyAgentAppSettings
    {
        public ACASettings ACASettings { get; set; } = new();
    }
}
