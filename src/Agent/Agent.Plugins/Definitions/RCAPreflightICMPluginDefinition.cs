using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using Agent.Core;
using Agent.Core.Attributes;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Markdig; // ensure IncidentProcessingContext is available
using Agent.Plugins.Helpers;

namespace Agent.Plugins.Definitions;

// Exposes a single tool to post RCA preflight summary to ICM and tag the incident.
[AgentToolPlugin(IsFirstPartyOnly = true, Category = ToolCategories.IncidentManagement)]
public class RCAPreflightICMPluginDefinition
{
    private readonly IICMPlugin _icmPlugin;
    private readonly ILogger<RCAPreflightICMPluginDefinition> _logger;

    public RCAPreflightICMPluginDefinition(IICMPlugin icmPlugin, ILogger<RCAPreflightICMPluginDefinition> logger)
    {
        _icmPlugin = icmPlugin;
        _logger = logger;
    }

    [WriteAction(runInReadOnlyMode: false,
        readOnlyMessage: "Would have posted RCA summary to ICM and added tag. Operation simulated successfully.")]
    [Description("Post RCA preflight summary to ICM discussion and add a tag")]
    public async Task<string> PostIcmRcaSummary(
        [Description("ICM incident ID")] string incidentId,
        [Description("ICM tag to add, e.g., ScaleCtrlRCAProcessed or BlobTrigRCAProcessed")] string tag,
        [Description("RCA preflight summary in Markdown or plain text")] string summary)
    {
        if (string.IsNullOrWhiteSpace(incidentId))
        {
            return "IncidentId is required.";
        }

        // Allow environment override for local testing
        bool allowNonScannerPost = false;
        try
        {
            var env = System.Environment.GetEnvironmentVariable("SREAGENT_ALLOW_NON_SCANNER_RCA_POST");
            allowNonScannerPost = !string.IsNullOrWhiteSpace(env) && (env.Equals("1", System.StringComparison.OrdinalIgnoreCase) || env.Equals("true", System.StringComparison.OrdinalIgnoreCase) || env.Equals("yes", System.StringComparison.OrdinalIgnoreCase));
        }
        catch { }

        // Only allow posting when the workflow is scanner-originated (ambient flag set by IcmScanner) unless overridden
        if (!IncidentProcessingContext.IsScannerOrigin && !allowNonScannerPost)
        {
            _logger.LogInternalInformation("[RCAPreflightICMPluginDefinition] Skipping ICM post: non-scanner origin workflow for incident {IncidentId}", incidentId);
            return "Skipped: non-scanner-origin workflow.";
        }

        try
        {
            // Pre-check: fetch incident and skip if already completed for owning team
            var incident = await _icmPlugin.GetIncidentInfo(incidentId);
            var owningTeamId = incident?.OwningTeam ?? string.Empty;
            var teamCompletedTag = !string.IsNullOrWhiteSpace(owningTeamId) ? $"{owningTeamId}:Completed" : null;
            var existingTags = incident?.Tags ?? Array.Empty<string>();

            if (!string.IsNullOrWhiteSpace(teamCompletedTag) && existingTags.Any(t => string.Equals(t, teamCompletedTag, System.StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInternalInformation("[RCAPreflightICMPluginDefinition] Skipping summary post: already completed for team {OwningTeamId} on incident {IncidentId}", owningTeamId, incidentId);
                return $"Skipped: already marked completed for team {owningTeamId}.";
            }

            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseGenericAttributes()
                .Build();

            // Remove emojis and normalize markdown before converting to HTML to avoid garbling in ICM
            var cleanedSummary = MarkdownEmojiSanitizer.RemoveEmojisPreserveMarkdown(summary);
            var htmlContent = Markdown.ToHtml(cleanedSummary, pipeline);

            var header = $"<h3>Automated RCA Preflight Summary</h3><div><b>Timestamp (UTC):</b> {System.DateTime.UtcNow:O}</div>";
            var discussionEntry = $"{header}<div style=\"margin-top:8px\">{htmlContent}</div>";

            // Post discussion entry
            var postResult = await _icmPlugin.PostDiscussionEntry(incidentId, discussionEntry);

            // Add generic tag if provided
            if (!string.IsNullOrWhiteSpace(tag))
            {
                await _icmPlugin.AddTagToIncident(incidentId, tag);
            }

            // Add team-specific completed tag
            if (!string.IsNullOrWhiteSpace(teamCompletedTag))
            {
                await _icmPlugin.AddTagToIncident(incidentId, teamCompletedTag);
            }

            return postResult;
        }
        catch (System.Exception ex)
        {
            _logger.LogInternalError(ex, "[RCAPreflightICMPluginDefinition] Failed to post summary to ICM for incident {IncidentId}", incidentId);
            throw;
        }
    }
}
