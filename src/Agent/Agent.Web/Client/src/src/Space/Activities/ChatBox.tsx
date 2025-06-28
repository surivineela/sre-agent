import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { memo, useCallback } from 'react';
import { Thread, ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import ChatBoxFooter from '../Components/ChatBoxFooter';
import ChatLoading from '../Components/ChatLoading';
import ChatMessage from '../Components/ChatMessage';
import { IChatBoxProps } from '../Contracts/Activities';
import { useChatBox } from '../Hooks/useChatBox';
import { ChatBoxStyles } from '../Styles/Activities.styles';
import AzureSREWelcome from './AzureSREWelcome';
import { ChatSuggestions } from './ChatSuggestions';
import { getGroupedMessages } from './Utility';

export const ChatBox = ({
    addThread,
    promoteThread,
    updateThreadLastReadTime,
    threadId,
    threadSource,
    thread,
    onThreadUpdate,
}: IChatBoxProps) => {
    const {
        messages,
        temporaryUserMessage,
        agentTypingMessage,
        isLoadingInitialChatHistory,
        sendMessage,
        disableInput,
        isNewAndCleanThread,
        messagesDivRef,
        intersectionObserverRef,
        currentThreadId,
        cancelResponse,
        prompts,
        messagePromptsUsed,
        onScroll,
        showNewMessageButton,
        onClickNewMessageButton,
    } = useChatBox(addThread, promoteThread, updateThreadLastReadTime, threadId);

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
                            <ChatMessage
                                key={index}
                                message={message}
                                previousMessage={messages[index - 1]}
                                nextMessage={messages[index + 1]}
                                getGroupedMessages={() => getGroupedMessages(messages, index)}
                                threadId={currentThreadId || ''}
                            />
                        ))}

                        {temporaryUserMessage && (
                            <ChatMessage
                                message={temporaryUserMessage}
                                previousMessage={messages[messages.length - 1]}
                                threadId={currentThreadId || ''}
                            />
                        )}

                        {agentTypingMessage && (
                            <ChatMessage
                                message={agentTypingMessage}
                                isTyping
                                threadId={currentThreadId || ''}
                                cancelResponse={cancelResponse}
                            />
                        )}
                    </CopilotChat>
                </div>

                <ChatBoxFooter
                    sendMessage={sendMessage}
                    disableInput={disableInput}
                    isNewMessageButtonVisible={showNewMessageButton}
                    onClickNewMessageButton={onClickNewMessageButton}
                    prompts={prompts}
                    messagePromptsUsed={messagePromptsUsed}
                    threadId={threadId}
                    currentAgentMode={thread?.agentMode}
                    onAgentModeChange={handleAgentModeChange}
                />
            </CopilotProvider>
        </div>
    );
};

export default memo(ChatBox);
