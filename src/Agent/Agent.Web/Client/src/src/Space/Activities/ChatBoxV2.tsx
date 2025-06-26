import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { memo } from 'react';
import { ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import ChatBoxFooterV2 from '../Components/ChatBoxFooterV2';
import ChatLoading from '../Components/ChatLoading';
import ChatMessageV2 from '../Components/ChatMessageV2';
import { IChatBoxProps } from '../Contracts/Activities';
import { useChatBoxV2 } from '../Hooks/useChatBoxV2';
import { ChatBoxStyles } from '../Styles/Activities.styles';
import AzureSREWelcome from './AzureSREWelcome';
import { ChatSuggestions } from './ChatSuggestions';

export const ChatBoxV2 = ({ addThread, promoteThread, updateThreadLastReadTime, threadId, threadSource }: IChatBoxProps) => {
    const {
        messages,
        isAgentTyping,
        streamingMessage,
        toolCallText,
        isCancellingStreaming,
        isLoadingInitialChatHistory,
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

    const isWelcomeThread = threadSource === ThreadSource.welcomeMessage;

    const { scrollable } = useScrollableComponentStyles();

    return (
        <div className={ChatBoxStyles.chatBox}>
            <CopilotProvider mode="canvas" className={ChatBoxStyles.chatBoxInner}>
                <div className={mergeClasses(scrollable, ChatBoxStyles.chatContainer)} ref={messagesDivRef} onScroll={onScroll}>
                    <CopilotChat className={ChatBoxStyles.chat}>
                        <div ref={intersectionObserverRef} />

                        {isLoadingInitialChatHistory && !isWelcomeThread && <ChatLoading />}

                        {isNewAndCleanThread && !isWelcomeThread && <ChatSuggestions sendMessage={sendMessage} />}

                        {/* Insert the richer welcome experience once at the top for welcome threads */}
                        {isWelcomeThread && <AzureSREWelcome threadId={currentThreadId} addThread={addThread} />}

                        {/* Render the remaining chat history */}
                        {messages.map((message, index) => (
                            <ChatMessageV2
                                key={message.id}
                                message={message}
                                previousMessage={messages[index - 1]}
                                nextMessage={messages[index + 1]}
                                getGroupedMessages={getGroupedChatMessages}
                                threadId={currentThreadId || ''}
                            />
                        ))}

                        {streamingMessage && (
                            <ChatMessageV2
                                key={streamingMessage.id}
                                message={streamingMessage}
                                previousMessage={messages[messages.length - 1]}
                                isStreamingMessage={true}
                                isTyping={isAgentTyping}
                                threadId={currentThreadId || ''}
                                toolCallText={toolCallText}
                            />
                        )}
                    </CopilotChat>
                </div>

                <ChatBoxFooterV2
                    sendMessage={sendMessage}
                    disableInput={isLoadingInitialChatHistory}
                    downButtonState={downButtonState}
                    onClickDownButton={onClickDownButton}
                    prompts={prompts}
                    messagePromptsUsed={messagePromptsUsed}
                    cancelStreaming={cancelStreaming}
                    isTyping={isAgentTyping}
                    isCancellingStreaming={isCancellingStreaming}
                />
            </CopilotProvider>
        </div>
    );
};

export default memo(ChatBoxV2);
