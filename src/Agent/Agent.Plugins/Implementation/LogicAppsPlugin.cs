using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Agent.Plugins.Services.Interfaces;
using Kusto.Cloud.Platform.Utils;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;

namespace Agent.Plugins.Implementation
{
    public class LogicAppsPlugin : ILogicAppsPlugin
    {
        private readonly ArmHelper _armHelper;
        private readonly IGraphService _graphService;

        public LogicAppsPlugin(
            ArmHelper armHelper,
            IGraphService _graphService)
        {
            this._armHelper = armHelper;
            this._graphService = _graphService;
        }

        public async Task<IReadOnlyList<Workflow>> ListWorkflowsAsync(string logicAppResourceId)
        {

            var workflows = new List<Workflow>();
            try
            {
                string logicAppResourceIdKey = logicAppResourceId.ToLower().Replace("/", "_");
                string query = $@"g.V()
                    .has('id', containing('{logicAppResourceIdKey}'))
                    .has('isDeleted', false)
                    .hasLabel('microsoft.web/sites')
                    .has('kind', containing('workflowapp'))
                    .outE()
                    .inV()
                        .hasLabel('microsoft.web/sites/workflows')
                        .has('isDeleted', false)
                        .project('id', 'name', 'type', 'properties')
                            .by(id())
                            .by(coalesce(values('resourceName'), constant('')))
                            .by(label())
                            .by(valueMap())";

                var result = await _graphService.QueryAsync(query);

                if (result == null || !result.Any())
                {
                    return workflows;
                }

                foreach (var workflow in result)
                {
                    var properties = workflow["properties"];
                    var id = workflow["id"].ToString();
                    var resourceId = id.Replace("_", "/");
                    var name = workflow["name"]?.ToString();

                    var workflowDescriptor = new Workflow(
                        Id: resourceId,
                        Name: name
                    );

                    workflows.Add(workflowDescriptor);
                }
            }
            catch (Exception)
            {
                return Array.Empty<Workflow>();
            }

            return workflows;
        }

        public async Task<IReadOnlyList<ManagedConnector>> GetManagedConnectorsByWorkflow(string subscriptionId, string resourceGroupName, string logicAppName, string workflowName)
        {
            var connectors = new Dictionary<string, ManagedConnector>();

            try
            {
                var id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{logicAppName}/workflows/{workflowName}";
                //_logger.LogInternalInformation("Querying subscriptions from graph database");
                string query = $@"g.V()
                .has('id', '{id.ToLower().Replace("/", "_")}')
                .has('isDeleted', false)
                .outE('USES')
                .inV()
                    .has('isDeleted', false)
                    .hasLabel('microsoft.web/connections')
                    .project('id', 'name', 'type', 'properties')
                        .by(id())
                        .by(coalesce(values('resourceName'), constant('')))
                        .by(label())
                        .by(valueMap())";

                var result = await _graphService.QueryAsync(query);
                if (result == null || !result.Any())
                {
                    return Array.Empty<ManagedConnector>();
                }

                foreach (var connector in result)
                {
                    var properties = connector["properties"];
                    var connectionNode = new ConnectionNode(properties);
                    var connectorName = connectionNode.ConnectorName;
                    if (connectorName != null)
                    {
                        connectors.TryAdd(connectorName, new ManagedConnector($"managedApis/{connectorName}", connectorName));
                    }
                }
            }
            catch (Exception)
            {
                return Array.Empty<ManagedConnector>();
            }

            return connectors.Values.ToArray();
        }


        public Task<ServiceProviderConnector?> LookupServiceProviderConnectorEquivalent(string managedConnectorId)
        {
            var lookup = new Dictionary<string, ServiceProviderConnector?>()
            {
                {
                    "managedApis/sftpwithssh",
                    new ServiceProviderConnector("serviceProviders/sftp", "sftp")
                }
            };

            return Task.FromResult(lookup.GetOrDefault(managedConnectorId, null));
        }
    }
}
