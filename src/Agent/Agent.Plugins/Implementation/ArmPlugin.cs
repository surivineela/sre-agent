using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    public class ArmPlugin : IArmPlugin
    {
        private readonly ILogger<ArmPlugin> _logger;

        public ArmPlugin(ILogger<ArmPlugin> logger)
        {
            _logger = logger;
        }

        public async Task<string> SetMinimumTlsVersion(
            string appResourceId,
            string minimumTlsVersion
)
        {
            var status = (await ArmHelper.GetTlsSettings([appResourceId])).SingleOrDefault();
            bool success = false;

            if (status != null)
            {
                success = await ArmHelper.UpdateMinimumTlsVersion(status, minimumTlsVersion);
            }

            var message = success switch
            {
                true => $"Resource {appResourceId} updated with minimum TLS version set to {minimumTlsVersion} at {DateTime.UtcNow:o}",
                false => $"Failed to update resource {appResourceId} at {DateTime.UtcNow:o}",
            };


            _logger?.LogInformation(message);
            return message;
        }
    }
}
