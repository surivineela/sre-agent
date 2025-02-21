namespace FirstPartyAgent.Web.Services;

using Agent.Core.Models;
using Agent.Runtime;
using Markdig;

public class SessionChatService : IChatService
{
    private readonly ILogger<SessionChatService> _logger;
    private readonly MarkdownPipeline _markdownPipeline;
    private readonly Session _conversation;

    public SessionChatService(ILogger<SessionChatService> logger, Session conversation)
    {
        _logger = logger;
        _conversation = conversation;
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()           // Disable HTML parsing
            .Build();
    }

    public async Task<ChatMessage> ProcessMessageAsync(string message)
    {
        var messageTime = _conversation.AddUserMessage(message);

        _logger.LogInformation("User > " + message);

        // For now this is hacked to wait for a response. Ideally the razon page would poll for new messages and refresh the chat window
        List<ChatMessage> response;
        do
        {
            await Task.Delay(1000);
            response = _conversation.GetMessages(messageTime);

        } while (response.Count == 0);

        foreach (var chatMessage in response)
        {
            _logger.LogInformation("Assistant > " + chatMessage.Message);
        }

        var firstMessage = response.First();

        firstMessage.Message = Markdown.ToHtml(firstMessage.Message, _markdownPipeline);

        return firstMessage;
    }
}