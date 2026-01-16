# IcM Enriched Tools Extension

Extend existing `ICMPlugin` with two new tools that leverage the IcM Copilot Data Gateway API.

## Tools

### GetIncidentDetails
Get incident context from AI-enriched API, falling back to OData if unavailable.

```csharp
Task<string> GetIncidentDetails(string incidentId, bool includeAlertDetails = false)
```

**Tool Description:** "Get incident details from IcM, using AI-enriched data when available. Set includeAlertDetails=false for a quick overview, or true for full context including the alerting Kusto query and results."

- Try AI-enriched endpoint first
- If fails/empty → fall back to existing `GetIncidentAsync`
- Optionally append alert details (Kusto query, TSG, results)
- Returns raw data, no transformation

### GetIncidentAlertDetails
Get the alerting discussion entry containing Kusto query and results.

```csharp
Task<string> GetIncidentAlertDetails(string incidentId)
```

**Tool Description:** "Get the alerting entry that created the incident, including Kusto query and results."
|------|--------|
| `Agent.Core/Models/EnrichedIncidentContext.cs` | NEW - Response model |
| `Agent.Core/Services/ICMAPIClient.cs` | Add `GetEnrichedIncidentContextAsync` + interface + null impl |
| `Agent.Plugins/Interface/IICMPlugin.cs` | Add method signatures |
| `Agent.Plugins/Implementation/ICMPlugin.cs` | Implement tools + IncidentDetailsResponse class |

---

## Implementation

### Models (Agent.Core/Models/)

**EnrichedIncidentContext.cs**
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Core.Models;

public class EnrichedIncidentContext
{
    [JsonPropertyName("IncidentId")]
    public string? IncidentId { get; set; }

    [JsonPropertyName("RawResponse")]
    public JsonElement? RawResponse { get; set; }

    [JsonIgnore]
    public bool HasContent => RawResponse?.TryGetProperty("rawqna", out _) == true;
}
```

**IncidentDetailsResponse.cs**
```csharp
namespace Agent.Core.Models;

public class IncidentDetailsResponse
{
    public string DataSource { get; set; } = string.Empty;  // "AIEnriched" or "OData"
    public object? IncidentData { get; set; }
    public DescriptionEntry? AlertDetails { get; set; }
}
```

### ICMAPIClient (Agent.Core/Services/)

**Interface addition:**
```csharp
Task<EnrichedIncidentContext?> GetEnrichedIncidentContextAsync(string incidentId);
```

**Implementation:**
```csharp
private readonly IIcmHttpClient _icmHttpClient;

public async Task<EnrichedIncidentContext?> GetEnrichedIncidentContextAsync(string incidentId)
{
    try
    {
        var apiPath = $"/api2/cert/icmcopilot/datagateway/enrichdata/{incidentId}?version=1&SELECT=rawqna,outagetimeline";
        var response = await _icmHttpClient.MakeICMRequestAsync<EnrichedIncidentContext>(
            HttpMethod.Get, apiPath, nameof(GetEnrichedIncidentContextAsync));

        return response?.HasContent == true ? response : null;
    }
    catch (Exception ex)
    {
        _logger.LogInternalWarning($"Enriched API failed for {incidentId}: {ex.Message}");
        return null;
    }
}
```

**Null implementation:**
```csharp
public Task<EnrichedIncidentContext?> GetEnrichedIncidentContextAsync(string incidentId)
    => Task.FromResult<EnrichedIncidentContext?>(null);
```

### ICMPlugin (Agent.Plugins/Implementation/)

```csharp
public async Task<string> GetIncidentDetails(string incidentId, bool includeAlertDetails = false)
{
    // Start alert fetch early if needed
    var alertTask = includeAlertDetails
        ? GetAlertingDiscussionEntry(incidentId)
        : Task.FromResult<DescriptionEntry?>(null);

    var enriched = await _icmApiClient.GetEnrichedIncidentContextAsync(incidentId);

    string dataSource;
    object? incidentData;

    if (enriched?.HasContent == true)
    {
        dataSource = "AIEnriched";
        incidentData = enriched;
    }
    else
    {
        var incident = await _icmApiClient.GetIncidentAsync(incidentId);
        if (incident == null)
            return $"Incident {incidentId} not found or you don't have access to it.";

        dataSource = "OData";
        incidentData = incident;
    }

    var alertEntry = await alertTask;

    var response = new IncidentDetailsResponse
    {
        DataSource = dataSource,
        IncidentData = incidentData,
        AlertDetails = alertEntry
    };

    return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
}

public async Task<string> GetIncidentAlertDetails(string incidentId)
{
    var entry = await GetAlertingDiscussionEntry(incidentId);
    if (entry == null)
        return "No alert details found. The incident may not have been created by a monitor.";

    return JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true });
}
```

---

## API Reference

**Endpoint:**
```
GET https://prod.microsofticm.com/api2/cert/icmcopilot/datagateway/enrichdata/{id}?version=1&SELECT=rawqna,outagetimeline
```

**Auth:** Client certificate (existing `IIcmHttpClient` handles this)

---

## Output Examples

**AI-Enriched:**
```json
{
  "DataSource": "AIEnriched",
  "IncidentData": {
    "IncidentId": "716803284",
    "RawResponse": { "rawqna": [...], "outagetimeline": "..." }
  },
  "AlertDetails": null
}
```

**OData Fallback:**
```json
{
  "DataSource": "OData",
  "IncidentData": { "Id": 716803284, "Title": "...", "Severity": 2, ... },
  "AlertDetails": null
}
```

**AlertDetails content** (when included):
```json
{
  "SubmittedBy": "azurealerting.trafficmanager.net",
  "SubmitDate": "2026-01-06T23:16:19Z",
  "Text": "- Primary Query and Result (executed at 2026-01-06 23:15:34Z)\n// [SREAgent] Data Plane Resource Stuck...",
  "RenderType": "Html"
}
```

---

## Checklist

- [ ] Create `EnrichedIncidentContext.cs` model
- [ ] Create `IncidentDetailsResponse.cs` model
- [ ] Add `IIcmHttpClient` dependency to `ICMAPIClient` constructor
- [ ] Add `GetEnrichedIncidentContextAsync` to interface + implementation + null impl
- [ ] Add tool methods to `IICMPlugin` interface
- [ ] Implement `GetIncidentDetails` and `GetIncidentAlertDetails` in `ICMPlugin`
