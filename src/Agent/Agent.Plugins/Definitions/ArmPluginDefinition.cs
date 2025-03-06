using System.ComponentModel;

namespace Agent.Plugins
{
    public class ArmPluginDefinition
    {
        private readonly IArmPlugin _armPlugin;

        public ArmPluginDefinition(IArmPlugin armPlugin)
        {
            _armPlugin = armPlugin;
        }

        [Description("Sets the minimum TLS version on a site resource")]
        public async Task<string> SetMinimumTlsVersion(
            [Description("The resource ID of the app.")]
            string appResourceId,
            [Description("The minimum TLS version to set, e.g. 1.2")]
            string minimumTlsVersion
            )
        {
            return await _armPlugin.SetMinimumTlsVersion(appResourceId, minimumTlsVersion);
        }

        [Description("Restart an AppService app")]
        public async Task<string> RestartWebApp(
            [Description("The resource ID of the AppService app.")]
            string appResourceId)
        {
            return await _armPlugin.RestartWebApp(appResourceId)
                ? "Restart succeeded"
                : "Restart failed";
        }
    }
}
