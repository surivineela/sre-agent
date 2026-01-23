// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Microsoft.AzureAd.Icm.Types;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services.IncidentTriggerDetection;

/// <summary>
/// Detects incident trigger events by analyzing state transitions and discussion entries.
/// </summary>
public class IncidentEventDetector : IIncidentEventDetector
{
    private static readonly TimeSpan NewIncidentThreshold = TimeSpan.FromMinutes(5);
    private const string AgentMentionPattern = "@sreagent";
    private const string AgentSystemUserAlias = "Azure SRE Agent";

    private readonly ILogger<IncidentEventDetector> _logger;

    public IncidentEventDetector(ILogger<IncidentEventDetector> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IncidentEventDetectionResult DetectOccurredEvents(
        IncidentStateSnapshot currentState,
        IncidentStateSnapshot? previousState,
        List<DescriptionEntry> discussionEntries,
        HashSet<string>? processedEntryIds = null,
        List<string>? onCallAliases = null)
    {
        var events = new HashSet<IcmIncidentTriggerEvent>();
        var entriesWithMention = new List<DiscussionEntryInfo>();
        var now = DateTime.UtcNow;

        // 1. Detect IncidentCreatedOrTransferred
        if (DetectCreatedOrTransferred(currentState, previousState, discussionEntries, now))
        {
            events.Add(IcmIncidentTriggerEvent.IncidentCreatedOrTransferred);
            _logger.LogInternalInformation("[EventDetector] Detected IncidentCreatedOrTransferred for {IncidentId}", currentState.IncidentId);
        }

        // 2. Detect IncidentReactivated (Mitigated/Resolved -> Active)
        if (DetectReactivated(currentState, previousState))
        {
            events.Add(IcmIncidentTriggerEvent.IncidentReactivated);
            _logger.LogInternalInformation("[EventDetector] Detected IncidentReactivated for {IncidentId}", currentState.IncidentId);
        }

        // 3. Detect IncidentMitigated (-> Mitigated)
        if (DetectMitigated(currentState, previousState))
        {
            events.Add(IcmIncidentTriggerEvent.IncidentMitigated);
            _logger.LogInternalInformation("[EventDetector] Detected IncidentMitigated for {IncidentId}", currentState.IncidentId);
        }

        // 3.5. Detect IncidentResolved (-> Resolved)
        if (DetectResolved(currentState, previousState))
        {
            events.Add(IcmIncidentTriggerEvent.IncidentResolved);
            _logger.LogInternalInformation("[EventDetector] Detected IncidentResolved for {IncidentId}", currentState.IncidentId);
        }

        // 4. Detect DiscussionEntry (@sreagent + STRICT on-call validation)
        // STRICT: If on-call list not provided or empty, DiscussionEntry will NOT trigger
        var mentionEntries = DetectDiscussionEntries(discussionEntries, processedEntryIds, onCallAliases);
        if (mentionEntries.Count > 0)
        {
            events.Add(IcmIncidentTriggerEvent.DiscussionEntry);
            entriesWithMention.AddRange(mentionEntries);
            _logger.LogInternalInformation("[EventDetector] Detected DiscussionEntry for {IncidentId}, {Count} new mentions",
                currentState.IncidentId, mentionEntries.Count);
        }

        return new IncidentEventDetectionResult(events, entriesWithMention);
    }

    private bool DetectCreatedOrTransferred(
        IncidentStateSnapshot current,
        IncidentStateSnapshot? previous,
        List<DescriptionEntry> entries,
        DateTime now)
    {
        // New incident that was created recently (within threshold)
        // We only trigger for truly new incidents, not old ones we've never processed
        if (previous == null && current.CreatedDate > now.Subtract(NewIncidentThreshold))
        {
            return true;
        }

        // Transfer detected via discussion entry (same logic as existing isIncidentNeedToHandle)
        var hasTransfer = entries.Any(e =>
            e.Date > now.Subtract(NewIncidentThreshold) &&
            !string.IsNullOrEmpty(e.Text) &&
            e.Text.StartsWith("<div>Transferred from", StringComparison.OrdinalIgnoreCase));
        return hasTransfer;
    }

    private bool DetectReactivated(IncidentStateSnapshot current, IncidentStateSnapshot? previous)
    {
        if (previous == null) return false;

        var wasInactive = previous.State.Equals("Mitigated", StringComparison.OrdinalIgnoreCase) ||
                          previous.State.Equals("Resolved", StringComparison.OrdinalIgnoreCase);
        var isNowActive = current.State.Equals("Active", StringComparison.OrdinalIgnoreCase);

        return wasInactive && isNowActive;
    }

    private bool DetectMitigated(IncidentStateSnapshot current, IncidentStateSnapshot? previous)
    {
        var isNowMitigated = current.State.Equals("Mitigated", StringComparison.OrdinalIgnoreCase);

        // New incident already mitigated
        if (previous == null)
            return isNowMitigated;

        var wasNotMitigated = !previous.State.Equals("Mitigated", StringComparison.OrdinalIgnoreCase);
        return wasNotMitigated && isNowMitigated;
    }

    private bool DetectResolved(IncidentStateSnapshot current, IncidentStateSnapshot? previous)
    {
        var isNowResolved = current.State.Equals("Resolved", StringComparison.OrdinalIgnoreCase);

        // New incident already resolved - unlikely but handle it
        if (previous == null)
            return isNowResolved;

        var wasNotResolved = !previous.State.Equals("Resolved", StringComparison.OrdinalIgnoreCase);
        return wasNotResolved && isNowResolved;
    }

    private List<DiscussionEntryInfo> DetectDiscussionEntries(
        List<DescriptionEntry> entries,
        HashSet<string>? processedEntryIds,
        List<string>? onCallAliases)
    {
        var result = new List<DiscussionEntryInfo>();

        // STRICT: If on-call aliases not provided or empty, do NOT detect any discussion entries
        if (onCallAliases == null || onCallAliases.Count == 0)
        {
            _logger.LogInternalInformation("[EventDetector] No on-call aliases provided, skipping DiscussionEntry detection (STRICT mode)");
            return result;
        }

        foreach (var entry in entries)
        {
            // Skip if already processed
            var entryId = entry.DescriptionEntryId.ToString();
            if (processedEntryIds?.Contains(entryId) == true)
            {
                continue;
            }

            // Skip system-generated entries (from agent itself)
            if (IsSystemGeneratedEntry(entry))
            {
                continue;
            }

            // Check for @sreagent mention
            if (string.IsNullOrEmpty(entry.Text) ||
                !entry.Text.Contains(AgentMentionPattern, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // STRICT: Validate that ChangedBy is one of the on-call aliases
            var changedBy = entry.ChangedBy ?? string.Empty;
            var isOnCall = onCallAliases.Any(alias =>
                alias.Equals(changedBy, StringComparison.OrdinalIgnoreCase));

            if (!isOnCall)
            {
                _logger.LogInternalInformation(
                    "[EventDetector] Skipping discussion entry - ChangedBy '{ChangedBy}' is not in on-call list",
                    changedBy);
                continue;
            }

            result.Add(new DiscussionEntryInfo(
                entryId,
                changedBy,
                entry.Date,
                entry.Text
            ));
        }

        return result;
    }

    private bool IsSystemGeneratedEntry(DescriptionEntry entry)
    {
        // Skip entries from the agent itself
        if (string.Equals(entry.ChangedBy, AgentSystemUserAlias, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Skip entries where ChangedBy looks like a GUID (system-generated)
        if (Guid.TryParse(entry.ChangedBy, out _))
        {
            return true;
        }

        return false;
    }
}
