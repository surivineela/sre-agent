using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
        private readonly ArmHelper _armHelper;

        public ArmPlugin(ILogger<ArmPlugin> logger, ArmHelper armHelper)
        {
            _logger = logger;
            _armHelper = armHelper;
        }

        public async Task<string> SetMinimumTlsVersion(
            string appResourceId,
            string minimumTlsVersion)
        {
            var status = (await _armHelper.GetTlsSettings([appResourceId])).SingleOrDefault();
            bool success = false;
            string reason = string.Empty;

            if (status != null)
            {
                var response = await _armHelper.UpdateMinimumTlsVersion(status, minimumTlsVersion);
                success = response.Item1;
                reason = response.Item2;
            }
            else
            {
                success = false;
                reason = $"Resource {appResourceId} not found.";
            }

            var message = success switch
            {
                true => $"Resource {appResourceId} updated with minimum TLS version set to {minimumTlsVersion} at {DateTime.UtcNow:o}",
                false => $"Failed to update resource {appResourceId} at {DateTime.UtcNow:o}. Reason: {reason}",
            };


            _logger?.LogInformation(message);
            return message;
        }
        public async Task<List<TlsStatus>> GetTlsSettings(List<string> resourceIds)
        {
            return await _armHelper.GetTlsSettings(resourceIds);
        }


        public async Task<bool> RestartWebApp(
            string appResourceId)
        {
            return await _armHelper.RestartWebAppAsync(appResourceId);
        }

        public async Task<bool> CheckIfResourceExists(string appResourceId)
        {
            return await _armHelper.CheckIfResourceExistsAsync(appResourceId);
        }

        public async Task<bool> RestartContainerApp(string appResourceId, string revisionName)
        {
            return await _armHelper.RestartContainerAppAsync(appResourceId, revisionName);
        }
    }
}
