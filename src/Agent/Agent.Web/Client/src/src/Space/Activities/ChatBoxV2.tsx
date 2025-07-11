import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { memo } from 'react';
import { ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import ChatBoxFooterV2 from '../Components/ChatBoxFooterV2';
import ChatLoading from '../Components/ChatLoading';
import ChatMessages from '../Components/ChatMessages';
import ChatMessageV2 from '../Components/ChatMessageV2';
import PermissionErrorChatMessage from '../Components/PermissionErrorChatMessage';
import { IChatBoxProps } from '../Contracts/Activities';
import { ChatBoxContext, ThreadAgentModeContext } from '../Contracts/Context';
import { useChatBoxV2 } from '../Hooks/useChatBoxV2';
import { useThreadAgentMode } from '../Hooks/useThreadAgentMode';
import { ChatBoxStyles } from '../Styles/Activities.styles';
import AzureSREWelcome from './AzureSREWelcome';
import { ChatSuggestions } from './ChatSuggestions';

export const ChatBoxV2 = ({ addThread, promoteThread, updateThreadLastReadTime, threadId, threadSource }: IChatBoxProps) => {
    const {
        chatHistory,
        newMessages,
        isAgentTyping,
        temporaryUserMessage,
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

    const threadAgentModeData = useThreadAgentMode(threadId, threadSource);

    const isWelcomeThread = threadSource === ThreadSource.welcomeMessage;

    const { scrollable } = useScrollableComponentStyles();

    return (
        <ChatBoxContext.Provider value={{ getGroupedChatMessages }}>
            <ThreadAgentModeContext.Provider value={{ ...threadAgentModeData }}>
                <div className={ChatBoxStyles.chatBox}>
                    <CopilotProvider mode="canvas" className={ChatBoxStyles.chatBoxInner}>
                        <div className={mergeClasses(scrollable, ChatBoxStyles.chatContainer)} ref={messagesDivRef} onScroll={onScroll}>
                            <CopilotChat className={ChatBoxStyles.chat}>
                                <div ref={intersectionObserverRef} />

                                {isLoading && !isWelcomeThread && <ChatLoading />}

                                {isNewAndCleanThread && !isWelcomeThread && <ChatSuggestions sendMessage={sendMessage} />}

                                {/* Insert the richer welcome experience once at the top for welcome threads */}
                                {isWelcomeThread && <AzureSREWelcome threadId={currentThreadId} addThread={addThread} />}

                                {/* Display permission error message if any*/}
                                <PermissionErrorChatMessage key={'permission-error-chat-message'} isLoading={isLoading} />

                                {/* Chat history */}
                                {!isLoading &&
                                    chatHistory?.map((messages, index) => {
                                        return (
                                            <ChatMessages
                                                // set key to chathistory.length - index to ensure the existing page always has the same key to prevent re-rendering
                                                key={chatHistory.length - index}
                                                messages={messages}
                                                threadId={currentThreadId || ''}
                                                prevMessageBeforeTheFirstMessage={
                                                    chatHistory?.[index - 1]?.[chatHistory?.[index - 1]?.length - 1]
                                                }
                                                nextMessageAfterTheLastMessage={
                                                    chatHistory?.[index + 1]?.[0] || newMessages[0] || temporaryUserMessage
                                                }
                                            />
                                        );
                                    })}

                                {/* New messages */}
                                {!isLoading && (
                                    <ChatMessages
                                        messages={newMessages}
                                        threadId={currentThreadId || ''}
                                        prevMessageBeforeTheFirstMessage={
                                            chatHistory?.[chatHistory.length - 1]?.[chatHistory?.[chatHistory.length - 1]?.length - 1]
                                        }
                                        nextMessageAfterTheLastMessage={temporaryUserMessage || undefined}
                                    />
                                )}

                                {temporaryUserMessage && (
                                    <ChatMessageV2
                                        key={temporaryUserMessage.id}
                                        message={temporaryUserMessage}
                                        threadId={currentThreadId || ''}
                                        previousMessage={
                                            newMessages[newMessages.length - 1] ||
                                            chatHistory?.[chatHistory.length - 1]?.[chatHistory?.[chatHistory.length - 1]?.length - 1]
                                        }
                                    />
                                )}

                                {!isLoading && streamingMessage && (
                                    <ChatMessageV2
                                        key={streamingMessage.id}
                                        message={streamingMessage}
                                        isStreamingMessage={true}
                                        isTyping={isAgentTyping}
                                        threadId={currentThreadId || ''}
                                        toolCallText={toolCallText}
                                        isWaitingForStreamingMessages={isWaitingForStreamingMessages}
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
                            threadId={currentThreadId}
                        />
                    </CopilotProvider>
                </div>
            </ThreadAgentModeContext.Provider>
        </ChatBoxContext.Provider>
    );
};

export default memo(ChatBoxV2);
