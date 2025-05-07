import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { memo } from 'react';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import ChatBoxFooter from '../Components/ChatBoxFooter';
import ChatLoading from '../Components/ChatLoading';
import ChatMessage from '../Components/ChatMessage';
import { IChatBoxProps } from '../Contracts/Activities';
import { useChatBox } from '../Hooks/useChatBox';
import { ChatBoxStyles } from '../Styles/Activities.styles';
import AzureSREWelcome from './AzureSREWelcome';
import { ChatSuggestions } from './ChatSuggestions';

export const ChatBox = ({ addThread, threadId, threadSource }: IChatBoxProps & { threadSource?: string }) => {
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

        handleScroll,
        showNewMessageButton,
        onClickNewMessageButton,
    } = useChatBox(addThread, threadId, threadSource);

    const isWelcomeThread = threadSource === 'WelcomeMessage';

    const { scrollable } = useScrollableComponentStyles();

    return (
        <div className={ChatBoxStyles.chatBox}>
            <CopilotProvider mode="canvas" className={ChatBoxStyles.chatBoxInner}>
                <div className={mergeClasses(scrollable, ChatBoxStyles.chatContainer)} ref={messagesDivRef} onScroll={handleScroll}>
                    <CopilotChat className={ChatBoxStyles.chat}>
                        <div ref={intersectionObserverRef} />

                        {isLoadingInitialChatHistory && <ChatLoading />}

                        {isNewAndCleanThread && !isWelcomeThread && <ChatSuggestions sendMessage={sendMessage} />}

                        {/* Insert the richer welcome experience once at the top for welcome threads */}
                        {isWelcomeThread && <AzureSREWelcome threadId={currentThreadId} />}

                        {/* Render the remaining chat history */}
                        {messages.map((message, index) => (
                            <ChatMessage key={index} message={message} threadId={currentThreadId || ''} />
                        ))}

                        {temporaryUserMessage && <ChatMessage message={temporaryUserMessage} threadId={currentThreadId || ''} />}

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
                />
            </CopilotProvider>
        </div>
    );
};

export default memo(ChatBox);
