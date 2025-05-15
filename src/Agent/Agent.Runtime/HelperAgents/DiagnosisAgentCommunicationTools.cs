// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Runtime.Services;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.HelperAgents;

public class DiagnosisAgentCommunicationTools
{
    private readonly IThreadRepository _threadRepository;
    private readonly Guid _threadId;
    private readonly ILogger<DiagnosisAgentCommunicationTools> _logger;
    private bool _isInitialized = false;

    private Message _investigationMessage = new Message(
        Id: Guid.NewGuid(),
        TimeStamp: DateTime.UtcNow,
        Author: new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
        Text: ChatMessageService.InitializeInvestigationSummariesMessage(
            "Starting investigation and diagnosis",
            [
                new("Planning", "📝 Gathering information about the issue", true)
            ])
    );

    public DiagnosisAgentCommunicationTools(
        IThreadRepository threadRepository,
        Guid threadId,
        ILogger<DiagnosisAgentCommunicationTools> logger
    )
    {
        _threadRepository = threadRepository;
        _threadId = threadId;
        _logger = logger;
    }

    // called directly in agent code, not by the LLM
    public Task InitializeSummaryAsync()
    {
        return InitIfNeededAsync();
    }

    [Description("Add a new summary of investigative work to the investigation result")]
    public async Task AddNewSummary(
        [Description("Title of this summary, should be brief and descriptive of the work that was done")]
        string title,
        [Description("The summary content. 2-3 sentences maximum, should be concise and well formatted using bulletpoints")]
        string summary
    )
    {
        await InitIfNeededAsync();

        _investigationMessage = _investigationMessage with
        {
            Text = ChatMessageService.AppendInvestigationSummary(
                _investigationMessage.Text,
                title,
                summary
            )
        };

        await _threadRepository.UpdateMessageAsync(_threadId, _investigationMessage);

        _logger.LogInternalInformation("Successfully appended investigation summary '{Title}' to message {MessageId}", title, _investigationMessage.Id);
    }

    // Called directly in agent code, not by LLM
    public async Task AddFinalSummaryAsync(string finalSummary)
    {
        await InitIfNeededAsync();

        _investigationMessage = _investigationMessage with
        {
            Text = ChatMessageService.AppendInvestigationSummary(
                _investigationMessage.Text,
                "Final Summary",
                finalSummary,
                status: "completed",
                isFinal: true
            )
        };

        await _threadRepository.UpdateMessageAsync(_threadId, _investigationMessage);

        _logger.LogInternalInformation("Successfully added finally summary to investigation message {MessageId}", _investigationMessage.Id);
    }

    private async Task InitIfNeededAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _investigationMessage = _investigationMessage with
        {
            TimeStamp = DateTime.UtcNow
        };

        _investigationMessage = await _threadRepository.AddMessageAsync(_threadId, _investigationMessage);

        _isInitialized = true;
    }
}
