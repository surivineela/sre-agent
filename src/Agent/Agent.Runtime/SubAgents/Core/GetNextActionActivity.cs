// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.SubAgents.Core;

//[DurableTask]
//public class GetNextActionActivity : TaskActivity<NextActionInput, ChatMessage>
//{
//    private readonly IChatClient _chatClient;
//    private readonly ToolsRepository _toolsRepository;
//    private readonly ILogger<GetNextActionActivity> _logger;

//    public GetNextActionActivity(
//        IChatClient chatClient,
//        ToolsRepository toolsRepository,
//        ILogger<GetNextActionActivity> logger)
//    {
//        _chatClient = chatClient;
//        _toolsRepository = toolsRepository;
//        _logger = logger;
//    }

//    public async override Task<ChatMessage> RunAsync(TaskActivityContext context, NextActionInput input)
//    {
//        var chatHistory = input.ChatMessages;
//        var chatOptions = new ChatOptions
//        {
//            Tools = input.ToolSignatures.Select<string, AITool>(_toolsRepository.FindAiFunction).ToList(),
//            ToolMode = ChatToolMode.RequireAny,
//            AdditionalProperties = new()
//            {
//                ["AllowParallelToolCalls"] = false,
//            }
//        };

//        try
//        {
//            var response = await _chatClient.GetResponseAsync(chatHistory, chatOptions);
//            return response.GetMessage();
//        }
//        catch (Exception ex)
//        {
//            _logger.LogInternalError(ex, "Error getting next action");
//            throw;
//        }
//    }
//}

