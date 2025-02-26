using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
