using Azure.ResourceManager.AppService;
using Agent.Core.Helpers;

/// <summary>
/// Simple cache for Kudu information to prevent unnecessary calls to the ARM helper.
/// </summary>
public sealed class KuduManager
{
    private string _kuduHostName;
    private string _os;
    private string _resourceId;
    private bool _is32Bit;
    private ArmHelper _armHelper;

    private KuduManager(string resourceId, ArmHelper armHelper)
    {
        _resourceId = resourceId;
        _armHelper = armHelper;
    }

    public string OS => _os;
    public string KuduHostName => _kuduHostName;
    public string ResourceId => _resourceId;
    public bool Is32Bit => _is32Bit;

    public static async Task<KuduManager> Initialize(string resourceId, ArmHelper armHelper)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));

            KuduManager manager = new KuduManager(resourceId, armHelper);
            WebSiteResource site = await armHelper.GetWebSiteResourceAsync(resourceId);
            manager._kuduHostName = site?.Data?.EnabledHostNames?.FirstOrDefault(h => h.Contains(".scm."));
            manager._os = site.Data.Kind.Contains("linux", StringComparison.OrdinalIgnoreCase) ? "Linux" : "Windows";
            WebSiteConfigResource config = await site.GetWebSiteConfig().GetAsync();
            manager._is32Bit = config?.Data.Use32BitWorkerProcess ?? false;
            Console.WriteLine($"[KuduManager] Initialized KuduManager for {resourceId}");
            return manager;
        }

        catch (Exception ex)
        {
            Console.Error.WriteLine($"[KuduManager] Initialization failed: {ex.Message}");
            throw new InvalidOperationException("Failed to initialize KuduManager.", ex);
        }
    }

    public async Task<string> ExecuteCommandAsync(string command, string workingDirectory)
        => await _armHelper.ExecuteKuduCommandAsync(_kuduHostName, command, workingDirectory);
}
