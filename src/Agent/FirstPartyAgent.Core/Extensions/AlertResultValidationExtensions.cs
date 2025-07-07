using static FirstPartyAgent.Core.Plugins.ControlPlanePlugin;

namespace FirstPartyAgent.Core.Extensions;
public static class AlertResultValidationExtensions
{
    public static List<string> GetValidationErrors<T>(this IEnumerable<T> items)
        where T : IValidatableAlertResult
    {
        return items
            .Select((item, idx) => (idx, error: item.Validate()))
            .Where(x => !string.IsNullOrWhiteSpace(x.error))
            .Select(x => $"[{x.idx}]: {x.error}")
            .ToList();
    }
}
