using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Logging;
using Agent.Plugins.Definitions;
using Azure.Core;
using Agent.Plugins.Implementation.DiagnosticsPlugin.ComputeResourceDiagnosticStrategies;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.ContainerService;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation.DiagnosticsPlugin;

public sealed class DiagnosticsPlugin : IDiagnosticsPlugin
{
    private readonly IAuthenticationService _authService;
    private readonly IArmClientFactory _armClientFactory;
    private readonly ILogger<DiagnosticsPlugin> _logger; 
    private readonly IEnumerable<IComputeResourceDiagnosticStrategy> _computeDiagnosticStrategies;
    private readonly ArmHelper _armHelper;

    public DiagnosticsPlugin(IAuthenticationService authenticationService,
                             IArmClientFactory armClientFactory,
                             ArmHelper armHelper,
                             ILogger<DiagnosticsPlugin> logger,
                             IKubePlugin kubePlugin)
    {
        _authService = authenticationService;
        _armClientFactory = armClientFactory;

        // Register Compute Diagnostic Strategies.
        _computeDiagnosticStrategies = new List<IComputeResourceDiagnosticStrategy>
        {
            new KubernetesDiagnosticStrategy(logger, kubePlugin),
            new AppServiceDiagnosticStrategy(logger, armHelper),
            new ContainerAppDiagnosticStrategy(logger, armHelper, armClientFactory)
            // TODO: Add one for Function Apps.
        };

        _armHelper = armHelper;
        _logger = logger;
    }

    public async Task<string> GetAnalysisAsync(string resourceId, AnalysisType analysisType, string additionalProperties)
    {
        // Precondition Checks.
        if (string.IsNullOrEmpty(resourceId))
        {
            throw new ArgumentNullException(nameof(resourceId), "Resource ID cannot be null or empty.");
        }

        if (analysisType == AnalysisType.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(analysisType), "Analysis type cannot be Unknown.");
        }

        // Step 1. Get the Compute Resource Info.
        ComputeResourceInfo computeResourceInfo = await GetComputeResourceInfoAsync(resourceId, additionalProperties);

        // Step 2. Based on the Compute Info -> Dispatch to the right analysis type.
        IComputeResourceDiagnosticStrategy diagnosticStrategy = _computeDiagnosticStrategies.FirstOrDefault(strategy => strategy.CanHandle(computeResourceInfo));

        // Step 3. Return the result.
        if (diagnosticStrategy != null)
        {
            return await diagnosticStrategy.PerformAnalysisAsync(resourceId, computeResourceInfo, analysisType, additionalProperties);
        }

        else
        {
            string error = $"No diagnostic strategy found for resource type {computeResourceInfo.ResourceType}.";
            _logger.LogInternalError(error);
            throw new ArgumentException(error);
        }
    }

    public Task<string> GetCPUAnalysisAsync(string resourceId, string additionalProperties)
       => GetAnalysisAsync(resourceId, AnalysisType.Cpu, additionalProperties);

    public Task<string> GetMemoryAnalysisAsync(string resourceId, string additionalProperties)
        => GetAnalysisAsync(resourceId, AnalysisType.Memory, additionalProperties);

    /// <summary>
    /// Gets compute type, OS, architecture, and runtime information for a given resource ID.
    /// </summary>
    internal async Task<ComputeResourceInfo> GetComputeResourceInfoAsync(string resourceId, string additionalProperties)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var resourceIdentifier = new ResourceIdentifier(resourceId);
        string type = resourceIdentifier.ResourceType.ToString().ToLowerInvariant();

        // Container App
        if (type.Contains("microsoft.app/containerapps"))
        {
            try
            {
                var containerAppResource = armClient.GetContainerAppResource(resourceIdentifier);
                var containerApp = await containerAppResource.GetAsync();

                // Get OS type - Container Apps run on Linux unless explicitly specified
                var osType = OSType.Linux;
                var architecture = Architecture.x64; // Default to x64 for Container Apps

                // TODO: Determine Arch.
                // Commented out logic for potentially determining architecture from SKU or image tag.
                // Try to determine architecture from image tag first
                //var imageRef = containerApp.Value.Data.Template?.Containers?.FirstOrDefault()?.Image ?? "";
                //if (!string.IsNullOrEmpty(imageRef))
                //{
                //    if (imageRef.Contains("arm64", StringComparison.OrdinalIgnoreCase) ||
                //        imageRef.Contains("aarch64", StringComparison.OrdinalIgnoreCase))
                //    {
                //        architecture = Architecture.ARM64;
                //    }
                //    else if (imageRef.Contains("arm/v7", StringComparison.OrdinalIgnoreCase) ||
                //             imageRef.Contains("armhf", StringComparison.OrdinalIgnoreCase))
                //    {
                //        architecture = Architecture.x86;
                //    }
                //}

                // Detect language stack by examining the container
                var languageStack = await DetectLanguageStackAsync(resourceId);

                return new ComputeResourceInfo(
                    ResourceType: ComputeResourceType.ContainerApp,
                    OsType: osType,
                    Architecture: architecture,
                    LanguageStack: languageStack,
                    Is32Bit: architecture == Architecture.x86
                );
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error determining Container App details for {resourceId}");
            }
        }

        // App Service
        else if (type.Contains("microsoft.web/sites"))
        {
            try
            {
                var webSiteResource = armClient.GetWebSiteResource(resourceIdentifier);
                var site = await webSiteResource.GetAsync();

                // Get app service site config to determine properties
                var kuduManager = await KuduManager.Initialize(resourceId, _armHelper);
                // Determine OS type
                var osType = kuduManager.OS.Contains("linux", StringComparison.OrdinalIgnoreCase) ? OSType.Linux : OSType.Windows;

                // Determine architecture - Now using stack settings and SKU info
                var architecture = kuduManager.Is32Bit ? Architecture.x86 : Architecture.x64; // Default assumption

                // TODO: Improve this eventually.
                //if (kind.Contains("arm64") || site.Value.Data.SiteConfig?.?.TryGetValue("IsArm64", out var isArm64) == true && isArm64 == "true")
                //{
                //    architecture = Architecture.ARM64;
                //}

                // Determine language stack - Use site config information

                // Check for .NET presence
                // var languageStack = LanguageStack.Unknown;
                //if (siteConfig.Value.Data.NetFrameworkVersion?.Contains("v4.") == true ||
                //    siteConfig.Value.Data.NetFrameworkVersion?.Contains("v5.") == true ||
                //    siteConfig.Value.Data.NetFrameworkVersion?.Contains("v6.") == true ||
                //    siteConfig.Value.Data.NetFrameworkVersion?.Contains("v7.") == true ||
                //    siteConfig.Value.Data.LinuxFxVersion?.Contains("DOTNET", StringComparison.OrdinalIgnoreCase) == true ||
                //    siteConfig.Value.Data.LinuxFxVersion?.Contains(".NET", StringComparison.OrdinalIgnoreCase) == true)
                LanguageStack languageStack = LanguageStack.Dotnet;

                return new ComputeResourceInfo(
                    ResourceType: ComputeResourceType.AppService,
                    OsType: osType,
                    Architecture: architecture,
                    LanguageStack: languageStack,
                    Is32Bit: kuduManager.Is32Bit 
                );
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error determining App Service details for {resourceId}");
            }
        }

        // AKS
        else if (type.Contains("microsoft.containerservice/managedclusters"))
        {
            try
            {
                var aksResource = armClient.GetContainerServiceManagedClusterResource(resourceIdentifier);
                var aks = await aksResource.GetAsync();
                var agentPool = aks.Value.Data.AgentPoolProfiles?.FirstOrDefault();

                // Determine OS type from agent pool
                var osType = OSType.Linux; // Default for AKS
                if (agentPool?.OSType?.ToString()?.Contains("Windows", StringComparison.OrdinalIgnoreCase) == true)
                {
                    osType = OSType.Windows;
                }

                // Determine architecture from VM size
                var architecture = Architecture.x64; // Default for AKS
                //var vmSize = agentPool?.VmSize?.ToLowerInvariant() ?? "";

                //if (vmSize.Contains("_a") || // Standard_A series may be x86
                //    vmSize.Contains("basic_a"))
                //{
                //    architecture = Architecture.x86;
                //}
                //else if (vmSize.Contains("_arm") || // Explicit ARM notation
                //         vmSize.StartsWith("standard_d") && vmSize.EndsWith("ps_v5")) // Dpsv5 are ARM
                //{
                //    architecture = Architecture.ARM64;

                return new ComputeResourceInfo(
                    ResourceType: ComputeResourceType.KubernetesService,
                    OsType: osType,
                    Architecture: architecture,
                    LanguageStack: LanguageStack.Unknown, // AKS can run any language
                    Is32Bit: architecture == Architecture.x86
                );
            }

            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error determining AKS details for {resourceId}");
            }
        }

        // Default fallback.
        _logger.LogInternalWarning($"Could not determine resource details for {resourceId} of type {type}");
        return new ComputeResourceInfo(
            ResourceType: ComputeResourceType.Unknown,
            OsType: OSType.Unknown,
            Architecture: Architecture.Unknown,
            LanguageStack: LanguageStack.Unknown,
            Is32Bit: false
        );
    }

    private async Task<LanguageStack> DetectLanguageStackAsync(string resourceId)
    {
        // TODO: Add more.
        return LanguageStack.Dotnet; // For now, assume only .NET is used. Change this accordingly later.
    }
}
