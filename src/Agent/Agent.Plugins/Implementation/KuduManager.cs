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
        _os = string.Empty;
        _kuduHostName = string.Empty;
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
            var site = await armHelper.GetWebSiteResourceAsync(resourceId);

            manager._kuduHostName = site?.Data?.EnabledHostNames?
                .Where(h => !string.IsNullOrEmpty(h))
                .FirstOrDefault(h => h.Contains(".scm.")) ?? string.Empty;

            manager._os = site?.Data?.Kind != null && site.Data.Kind.Contains("linux", StringComparison.OrdinalIgnoreCase)
                ? "Linux"
                : "Windows";

            var config = site?.GetWebSiteConfig() is var configOperation && configOperation != null
                ? await configOperation.GetAsync()
                : null;

            manager._is32Bit = config?.Value.Data.Use32BitWorkerProcess ?? false;
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
