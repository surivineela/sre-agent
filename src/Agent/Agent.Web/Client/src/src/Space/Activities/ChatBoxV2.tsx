import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { memo } from 'react';
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
        showDownButton,
        onClickDownButton,
    } = useChatBoxV2(addThread, promoteThread, updateThreadLastReadTime, threadId);

    const isWelcomeThread = threadSource === 'WelcomeMessage';

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
                        {isWelcomeThread && <AzureSREWelcome threadId={currentThreadId} />}

                        {/* Render the remaining chat history */}
                        {messages.map((message, index) => (
                            <ChatMessageV2
                                key={index}
                                message={message}
                                previousMessage={messages[index - 1]}
                                nextMessage={messages[index + 1]}
                                threadId={currentThreadId || ''}
                            />
                        ))}

                        {streamingMessage && (
                            <ChatMessageV2
                                message={streamingMessage}
                                isStreamingMessage={true}
                                isTyping={isAgentTyping}
                                threadId={currentThreadId || ''}
                            />
                        )}
                    </CopilotChat>
                </div>

                <ChatBoxFooterV2
                    sendMessage={sendMessage}
                    disableInput={isLoadingInitialChatHistory}
                    isDownButtonVisible={showDownButton}
                    onClickDownButton={onClickDownButton}
                    prompts={prompts}
                    messagePromptsUsed={messagePromptsUsed}
                    cancelStreaming={cancelStreaming}
                    isTyping={isAgentTyping}
                />
            </CopilotProvider>
        </div>
    );
};

export default memo(ChatBoxV2);
