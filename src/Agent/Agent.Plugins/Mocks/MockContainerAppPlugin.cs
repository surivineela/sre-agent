// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Azure.ResourceManager.Network;

namespace Agent.Plugins.Mocks
{
    public class MockContainerAppPlugin(MockNSGRulePlugin nsgRulePlugin) : IContainerAppPlugin
    {
        private readonly Dictionary<string, ContainerAppDescriptor> _containerApps = [];
        private readonly Dictionary<string, IList<string>> _containerAppNsgs = [];
        private readonly Dictionary<string, IList<RevisionInfo>> _containerAppRevisions = [];
        private readonly Dictionary<string, double> _cpuMetrics = [];
        private readonly Dictionary<string, double> _memoryMetrics = [];
        private readonly Dictionary<string, double> _requestMetrics = [];
        private readonly Dictionary<string, string> _containerImageReferences = [];
        private readonly Dictionary<string, string> _previousImageReferences = [];
        private readonly Dictionary<string, bool> _registryAccessibility = [];

        public void ConfigureContainerApp(ContainerAppDescriptor containerAppDescriptor, params IEnumerable<RevisionInfo> revisionInfo)
        {
            _containerApps[containerAppDescriptor.ResourceId] = containerAppDescriptor;
            _containerAppRevisions[containerAppDescriptor.ResourceId] = [.. revisionInfo];
            _cpuMetrics[containerAppDescriptor.ResourceId] = 1.0; // default cpu metric (percentage)
            _memoryMetrics[containerAppDescriptor.ResourceId] = 1.0; // default memory metric (bytes)
            _requestMetrics[containerAppDescriptor.ResourceId] = 1.0; // default request metric (count)
        }

        public void ConfigureDefaultApplication(string name, string resourceId)
        {
            var defaultApp = new ContainerAppDescriptor(
                ResourceId: resourceId,
                Name: name,
                Location: "centralus",
                WorkloadProfile: "Consumption",
                State: "Succeeded",
                ResourceGroup: "mockResourceGroup",
                EnvironmentId: "mockEnvironmentId",
                Containers: [
                    new Container(
                        Name: "mockContainerName",
                        Image: "mcr.microsoft.com/k8se/quickstart:latest",
                        Cpu: "0.5",
                        Memory: "1Gi")
                ],
                InitContainers: [],
                Configurations: new ContainerAppConfigurations(
                    RevisionMode: "single",
                    Ingress: new IngressConfiguration(
                        TargetPort: 80,
                        IsExternal: true,
                        Transport: "auto",
                        Hostnames: ["mockHostname"],
                        Traffic: [
                            new TrafficConfiguration(
                                RevisionName: "mockRevisionName",
                                Weight: 100,
                                Label: "",
                                LatestRevision: true)
                        ]),
                    Registries: []
                )
            );

            var revision = new RevisionInfo(
                RevisionName: "latest",
                IsActive: true,
                TrafficWeight: 100,
                CreatedOn: DateTime.UtcNow.ToString(),
                LastActiveOn: null,
                Fqdn: "myapp.azurecontainerapps.io",
                Template: null,
                Replicas: 1,
                Labels: null,
                ProvisioningError: null,
                HealthState: "Healthy",
                ProvisioningState: "Provisioned",
                RunningState: "Running"
            );

            ConfigureContainerApp(defaultApp, revision);
        }

        public void ConfigureSecurityRules(string resourceId, string containerAppResourceId)
        {

            if (_containerAppNsgs.TryGetValue(containerAppResourceId, out var value))
            {
                value.Add(resourceId);
            }
            else
            {
                _containerAppNsgs[containerAppResourceId] = [resourceId];
            }
        }

        public void ConfigureContainerAppCpu(string resourceId, double cpuPercentage)
        {
            _cpuMetrics[resourceId] = cpuPercentage;
        }

        public void ConfigureContainerAppMemory(string resourceId, double memoryBytes)
        {
            _memoryMetrics[resourceId] = memoryBytes;
        }

        public void ConfigureContainerAppRequestCount(string resourceId, double requestCount)
        {
            _requestMetrics[resourceId] = requestCount;
        }

        public async Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetAllNSGRulesForContainerAppAsync(string resourceId)
        {
            if (_containerAppNsgs.TryGetValue(resourceId, out var nsgs))
            {
                var result = new Dictionary<string, IReadOnlyList<SecurityRuleData>>();
                foreach (var nsg in nsgs)
                {
                    var rules = await nsgRulePlugin.GetNSGRulesAsync(nsg);

                    if (rules.TryGetValue("SecurityRules", out var securityRules))
                    {
                        result[nsg] = securityRules;
                    }
                }

                return result;
            }

            throw new ArgumentException($"Resource {resourceId} not found");
        }

        public Task<IReadOnlyList<CpuUsageTimeSeriesData>> GetContainerAppCpuMetrics(string resourceId)
        {
            var now = DateTime.UtcNow;
            var cpuMetrics = new List<CpuUsageTimeSeriesData>();

            for (DateTime i = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(30)); i < now; i = i.AddMinutes(1))
            {
                cpuMetrics.Add(new CpuUsageTimeSeriesData(i, _cpuMetrics[resourceId]));
            }

            return Task.FromResult<IReadOnlyList<CpuUsageTimeSeriesData>>(cpuMetrics.AsReadOnly());
        }

        public Task<ContainerAppDescriptor> GetContainerAppInfoAsync(string resourceId)
        {
            if (_containerApps.TryGetValue(resourceId, out var containerAppDescriptor))
            {
                return Task.FromResult(containerAppDescriptor);
            }

            throw new ArgumentException($"Resource {resourceId} not found");
        }

        public Task<IReadOnlyList<MemoryUsageTimeSeriesData>> GetContainerAppMemoryMetrics(string resourceId)
        {
            var now = DateTime.UtcNow;
            var memoryMetrics = new List<MemoryUsageTimeSeriesData>();

            for (DateTime i = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(30)); i < now; i = i.AddMinutes(1))
            {
                memoryMetrics.Add(new MemoryUsageTimeSeriesData(i, _memoryMetrics[resourceId]));
            }

            return Task.FromResult<IReadOnlyList<MemoryUsageTimeSeriesData>>(memoryMetrics.AsReadOnly());
        }

        public Task<IReadOnlyList<RequestCountTimeSeriesData>> GetContainerAppRequestMetrics(string resourceId)
        {
            var now = DateTime.UtcNow;
            var requestMetrics = new List<RequestCountTimeSeriesData>();

            for (DateTime i = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(30)); i < now; i = i.AddMinutes(1))
            {
                requestMetrics.Add(new RequestCountTimeSeriesData(i, _requestMetrics[resourceId]));
            }

            return Task.FromResult<IReadOnlyList<RequestCountTimeSeriesData>>(requestMetrics.AsReadOnly());
        }

        public Task<RevisionInfo?> GetLatestRevisionAsync(string resourceId)
        {
            if (_containerAppRevisions.TryGetValue(resourceId, out var revisions) && revisions.Count > 0)
            {
                return Task.FromResult<RevisionInfo?>(revisions.Last());
            }

            throw new ArgumentException($"Resource {resourceId} not found");
        }

        public Task<IReadOnlyList<ContainerAppDescriptor>> ListContainerAppsAsync(Guid subscriptionId)
        {
            var containerApps = _containerApps.Values.ToList();
            return Task.FromResult<IReadOnlyList<ContainerAppDescriptor>>(containerApps);
        }

        public Task<IReadOnlyList<RevisionInfo>> ListContainerAppRevisionsAsync(string resourceId)
        {
            if (_containerAppRevisions.TryGetValue(resourceId, out var revisions))
            {
                return Task.FromResult<IReadOnlyList<RevisionInfo>>(revisions.AsReadOnly());
            }

            throw new ArgumentException($"Resource {resourceId} not found");
        }

        public Task<string> RestartContainerApp(string appResourceId, string revisionName)
        {
            return Task.FromResult("RestartSuceeded");
        }

        public Task<bool> ScaleContainerApp(string resourceId, string desiredMemory, int minReplicas, int maxReplicas)
        {
            // TODO: simulate scaling logic with in-memory state

            return Task.FromResult(true);
        }

        public Task<string> GetContainerAppLogsAsync(string resourceId, string? revisionName)
        {
            return Task.FromResult("Logs retrieved successfully.");
        }

        public Task<bool> UpdateTargetPort(string resourceId, int targetPort)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<string> ListAvailableScalers()
        {
            throw new NotImplementedException();
        }

        public Task<string> GetScalerDetails(string scalerName)
        {
            throw new NotImplementedException();
        }

        public Task<RollbackResult> RollbackToLastKnownWorkingRevision(string resourceId)
        {
            if (!_containerApps.ContainsKey(resourceId))
            {
                return Task.FromResult(RollbackResult.Failure($"Container app with resourceId {resourceId} not found"));
            }

            if (!_previousImageReferences.TryGetValue(resourceId, out var previousImage) || string.IsNullOrEmpty(previousImage))
            {
                return Task.FromResult(RollbackResult.Failure("No previous image reference found to roll back to"));
            }

            // Set the current image to the previous one
            _containerImageReferences[resourceId] = previousImage;

            // Create mock details dictionary
            var details = new Dictionary<string, string>
            {
                { "AppName", _containerApps[resourceId].Name },
                { "PreviousImage", _containerImageReferences[resourceId] },
                { "RolledBackToImage", previousImage },
                { "MockRevision", "mock-revision-1" }
            };
            
            return Task.FromResult(RollbackResult.Success("mock-revision-1", previousImage, details));
        }

        public Task<ImageUpdateResult> UpdateContainerImage(string resourceId, string newImageReference)
        {
            if (_containerApps.TryGetValue(resourceId, out var containerApp))
            {
                string currentImage = null;
                
                if (_containerImageReferences.TryGetValue(resourceId, out var existingImage))
                {
                    currentImage = existingImage;
                    _previousImageReferences[resourceId] = currentImage;
                }
                else if (containerApp.Containers != null && containerApp.Containers.Any())
                {
                    currentImage = containerApp.Containers.First().Image;
                    _previousImageReferences[resourceId] = currentImage;
                }
                
                _containerImageReferences[resourceId] = newImageReference;
                
                var details = new Dictionary<string, string>
                {
                    { "AppName", containerApp.Name },
                    { "ResourceGroup", containerApp.ResourceGroup },
                    { "Location", containerApp.Location },
                    { "MockUpdate", "true" }
                };
                
                return Task.FromResult(ImageUpdateResult.Success(currentImage, newImageReference, details));
            }
            
            return Task.FromResult(ImageUpdateResult.Failure($"Container app with resource ID {resourceId} not found"));
        }

        public Task<string> GetImageReferenceFromResourceId(string resourceId)
        {
            if (_containerImageReferences.TryGetValue(resourceId, out var imageReference))
            {
                return Task.FromResult(imageReference);
            }
            
            if (_containerApps.TryGetValue(resourceId, out var containerApp) && 
                containerApp.Containers != null && 
                containerApp.Containers.Any())
            {
                return Task.FromResult(containerApp.Containers.First().Image);
            }
            
            return Task.FromResult<string>(null);
        }

        public Task<bool> VerifyExternalRegistryAsync(string resourceId, string imageReference)
        {
            bool isAccessible = _registryAccessibility.TryGetValue(resourceId, out var accessible) && accessible;

            // For testing purposes, consider ACR (azure container registry) always accessible since we are only verifyting non-ACR registries
            if (imageReference?.Contains("azurecr.io") == true)
            {
                isAccessible = true;
            }
            
            if (!isAccessible)
            {
                return Task.FromResult(false);
            }
            
            return Task.FromResult(true);
        }

        public Task<string> GetContainerMemoryAnalysisForDotnet(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsDotnetBased(string resourceId)
        {
            throw new NotImplementedException();
        }

        // public Task<string> RollbackToLastRevision(string resourceId)
        // {
        //     throw new NotImplementedException();
        // }
        public Task<ContainerAppHealthValidationResult> ValidateContainerAppHealth(string resourceId)
        {
            if (_containerApps.ContainsKey(resourceId))
            {
                return Task.FromResult(new ContainerAppHealthValidationResult
                {
                    IsHealthy = true,
                    Messages = ["Container app is healthy."]
                });
            }

            return Task.FromResult(new ContainerAppHealthValidationResult
            {
                IsHealthy = false,
                Messages = [$"Container app with resource ID {resourceId} not found."]
            });
        }
    }
}

