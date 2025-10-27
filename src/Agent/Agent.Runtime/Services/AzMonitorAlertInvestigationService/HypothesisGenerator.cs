// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.DataModels.IncidentModel;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services.AzMonitorAlertInvestigation;

public class HypothesisGenerator : IHypothesisGenerator
{
    private readonly IChatClientProvider _chatClientProvider;
    private readonly ILogger<HypothesisGenerator> _logger;

    public HypothesisGenerator(IChatClientProvider chatClientProvider, ILogger<HypothesisGenerator> logger)
    {
        _chatClientProvider = chatClientProvider;
        _logger = logger;
    }

    public async Task<List<Hypothesis>> GenerateHypothesesAsync(
        InvestigationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string prompt = BuildHypothesesGenerationPrompt(context);

            var options = new ChatOptions
            {
                Temperature = (float)0.1,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "json"
                }
            };

            var response = await _chatClientProvider.DefaultModel.GetResponseAsync(
                new List<ChatMessage> { new ChatMessage(ChatRole.System, prompt) },
                options);

            return DeserializeHypotheses(response.Text);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error generating hypotheses");
            return new List<Hypothesis>();
        }
    }

    private string BuildHypothesesGenerationPrompt(InvestigationContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert Azure SRE analyzing evidence to form hypotheses about an alert's root cause.");
        sb.AppendLine();
        // alert details
        sb.AppendLine("# Alert Details");
        sb.AppendLine("```");
        sb.AppendLine(FormatAlertDetails(context.Alert));
        sb.AppendLine("```");
        sb.AppendLine();
        // collected evidence
        sb.AppendLine("# Evidence Collected");

        foreach (var evidence in context.CollectedEvidence)
        {
            sb.AppendLine($"## {evidence.Key}");
            sb.AppendLine("```");
            sb.AppendLine(evidence.Value.RawOutput);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Add existing hypotheses if any
        if (context.CurrentHypotheses.Any())
        {
            sb.AppendLine("# Current Hypotheses");
            foreach (var hypothesis in context.CurrentHypotheses)
            {
                sb.AppendLine($"- {hypothesis.Description} (Confidence: {hypothesis.Confidence:P0})");
            }
            sb.AppendLine();
        }

        sb.AppendLine(@"# Your Task
Based on the evidence above, generate hypotheses about the root cause of this alert.
For each hypothesis:
1. Provide a clear and specific description
2. Assign a confidence score (0.0-1.0)
3. List specific evidence that supports this hypothesis
4. List any evidence that conflicts with this hypothesis
Return your analysis as a JSON object with the following structure:
```json
{
  ""hypotheses"": [
    {
      ""description"": ""Clear description of the potential root cause"",
      ""confidence"": 0.85,
      ""supportingEvidence"": [
        ""Specific piece of evidence that supports this hypothesis"",
        ""Another piece of supporting evidence""
      ],
      ""conflictingEvidence"": [
        ""Evidence that doesn't align with this hypothesis"",
        ""Another conflicting piece of evidence""
      ]
    },
    // Additional hypotheses...
  ]
}
```
Important:
- Focus on specific, non-generic root causes
- Assign realistic confidence scores
- Support each hypothesis with concrete evidence
- Include at most 3 hypotheses, prioritizing those with strongest evidence");

        return sb.ToString();
    }

    private List<Hypothesis> DeserializeHypotheses(string response)
    {
        try
        {
            // Try to extract JSON from the response
            var match = Regex.Match(response, @"\{[\s\S]*\}");

            string jsonContent = match.Success ? match.Value : response;

            var hypothesisResponse = JsonSerializer.Deserialize<HypothesisResponse>(jsonContent);

            if (hypothesisResponse?.Hypotheses == null || !hypothesisResponse.Hypotheses.Any())
            {
                _logger.LogInternalWarning("No hypotheses found in response");
                return new List<Hypothesis>();
            }

            return hypothesisResponse.Hypotheses.Select(h => new Hypothesis
            {
                Description = h.Description,
                Confidence = h.Confidence,
                SupportingEvidence = h.SupportingEvidence,
                ConflictingEvidence = h.ConflictingEvidence
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error deserializing hypotheses response");
            return new List<Hypothesis>();
        }
    }

    private string FormatAlertDetails(AlertItem alert)
    {
        var essentials = alert.Properties?.Essentials;
        return $@"ID: {alert.Id ?? "Unknown"}
Name: {alert.Name ?? "Unknown"}
Rule: {essentials?.AlertRule ?? "Unknown"}
Severity: {essentials?.Severity ?? "Unknown"}
Condition: {essentials?.MonitorCondition ?? "Unknown"}
Description: {essentials?.Description ?? "Unknown"}
Resource: {essentials?.TargetResourceName ?? essentials?.TargetResource ?? "Unknown"}
Type: {essentials?.TargetResourceType ?? "Unknown"}
Time: {essentials?.StartDateTime ?? "Unknown"}";
    }
}
