import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { memo, useCallback } from 'react';
import { Thread, ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import ChatBoxFooterV2 from '../Components/ChatBoxFooterV2';
import ChatLoading from '../Components/ChatLoading';
import ChatMessages from '../Components/ChatMessages';
import ChatMessageV2 from '../Components/ChatMessageV2';
import { IChatBoxProps } from '../Contracts/Activities';
import { ChatBoxContext } from '../Contracts/Context';
import { useChatBoxV2 } from '../Hooks/useChatBoxV2';
import { ChatBoxStyles } from '../Styles/Activities.styles';
import AzureSREWelcome from './AzureSREWelcome';
import { ChatSuggestions } from './ChatSuggestions';

export const ChatBoxV2 = ({
    addThread,
    promoteThread,
    updateThreadLastReadTime,
    threadId,
    threadSource,
    thread,
    onThreadUpdate,
}: IChatBoxProps) => {
    const {
        chatHistory,
        chatMessagesFromExistingStreamingMessages,
        newMessages,
        isAgentTyping,
        streamingMessage,
        toolCallText,
        isCancellingStreaming,
        isWaitingForStreamingMessages,
        isLoading,
        sendMessage,
        isNewAndCleanThread,
        messagesDivRef,
        intersectionObserverRef,
        currentThreadId,
        cancelStreaming,
        prompts,
        messagePromptsUsed,
        onScroll,
        downButtonState,
        onClickDownButton,
        getGroupedChatMessages,
    } = useChatBoxV2(addThread, promoteThread, updateThreadLastReadTime, threadId, threadSource);

    const handleAgentModeChange = useCallback(
        (updatedThread: Thread) => {
            console.log('Agent mode updated for thread:', updatedThread);
            // Pass the updated thread to the parent component
            onThreadUpdate?.(updatedThread);
        },
        [onThreadUpdate]
    );

    const isWelcomeThread = threadSource === ThreadSource.welcomeMessage;

    const { scrollable } = useScrollableComponentStyles();

    return (
        <ChatBoxContext.Provider value={{ getGroupedChatMessages }}>
            <div className={ChatBoxStyles.chatBox}>
                <CopilotProvider mode="canvas" className={ChatBoxStyles.chatBoxInner}>
                    <div className={mergeClasses(scrollable, ChatBoxStyles.chatContainer)} ref={messagesDivRef} onScroll={onScroll}>
                        <CopilotChat className={ChatBoxStyles.chat}>
                            <div ref={intersectionObserverRef} />

                            {isLoading && !isWelcomeThread && <ChatLoading />}

                            {isNewAndCleanThread && !isWelcomeThread && <ChatSuggestions sendMessage={sendMessage} />}

                            {/* Insert the richer welcome experience once at the top for welcome threads */}
                            {isWelcomeThread && <AzureSREWelcome threadId={currentThreadId} addThread={addThread} />}

                            {/* Chat history */}
                            {chatHistory?.map((messages, index) => {
                                return (
                                    <ChatMessages
                                        // set key to chathistory.length - index to ensure the existing page always has the same key to prevent re-rendering
                                        key={chatHistory.length - index}
                                        messages={messages}
                                        threadId={currentThreadId || ''}
                                        prevMessageBeforeTheFirstMessage={chatHistory?.[index - 1]?.[chatHistory?.[index - 1]?.length - 1]}
                                        nextMessageAfterTheLastMessage={
                                            chatHistory?.[index + 1]?.[0] || chatMessagesFromExistingStreamingMessages[0] || newMessages[0]
                                        }
                                    />
                                );
                            })}

                            {/* Existing streaming messages */}
                            <ChatMessages
                                messages={chatMessagesFromExistingStreamingMessages}
                                threadId={currentThreadId || ''}
                                prevMessageBeforeTheFirstMessage={
                                    chatHistory?.[chatHistory.length - 1]?.[chatHistory?.[chatHistory.length - 1]?.length - 1]
                                }
                                nextMessageAfterTheLastMessage={newMessages[0]}
                            />

                            {/* New messages */}
                            <ChatMessages
                                messages={newMessages}
                                threadId={currentThreadId || ''}
                                prevMessageBeforeTheFirstMessage={
                                    chatMessagesFromExistingStreamingMessages[chatMessagesFromExistingStreamingMessages.length - 1] ||
                                    chatHistory?.[chatHistory.length - 1]?.[chatHistory?.[chatHistory.length - 1]?.length - 1]
                                }
                            />

                            {streamingMessage && !isLoading && (
                                <ChatMessageV2
                                    key={streamingMessage.id}
                                    message={streamingMessage}
                                    isStreamingMessage={true}
                                    isTyping={isAgentTyping}
                                    threadId={currentThreadId || ''}
                                    toolCallText={toolCallText}
                                    isWaitingForStreamingMessages={isWaitingForStreamingMessages}
                                    previousMessage={
                                        newMessages[newMessages.length - 1] ||
                                        chatMessagesFromExistingStreamingMessages[chatMessagesFromExistingStreamingMessages.length - 1] ||
                                        chatHistory?.[chatHistory.length - 1]?.[chatHistory?.[chatHistory.length - 1]?.length - 1]
                                    }
                                />
                            )}
                        </CopilotChat>
                    </div>

                    <ChatBoxFooterV2
                        sendMessage={sendMessage}
                        disableInput={isLoading}
                        downButtonState={downButtonState}
                        onClickDownButton={onClickDownButton}
                        prompts={prompts}
                        messagePromptsUsed={messagePromptsUsed}
                        cancelStreaming={cancelStreaming}
                        isTyping={!!isAgentTyping}
                        isCancellingStreaming={isCancellingStreaming}
                        threadId={threadId}
                        currentAgentMode={thread?.agentMode}
                        onAgentModeChange={handleAgentModeChange}
                    />
                </CopilotProvider>
            </div>
        </ChatBoxContext.Provider>
    );
};

export default memo(ChatBoxV2);
