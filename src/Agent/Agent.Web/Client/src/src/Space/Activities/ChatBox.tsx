import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { memo } from 'react';
import ChatBoxFooter from '../Components/ChatBoxFooter';
import ChatLoading from '../Components/ChatLoading';
import ChatMessage from '../Components/ChatMessage';
import NewMessageButton from '../Components/NewMessageButton';
import { IChatBoxProps } from '../Contracts/Activities';
import { useChatBox } from '../Hooks/useChatBox';
import { ChatBoxStyles } from '../Styles/Activities.styles';
import { ChatSuggestions } from './ChatSuggestions';

export const ChatBox = ({ addThread, threadId }: IChatBoxProps) => {
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
    } = useChatBox(addThread, threadId);

    return (
        <div className={ChatBoxStyles.chatBox}>
            <CopilotProvider mode="canvas" className={ChatBoxStyles.chatBoxInner}>
                <CopilotChat className={ChatBoxStyles.chatContainer} ref={messagesDivRef} onScroll={handleScroll}>
                    <div ref={intersectionObserverRef} />
                    {isLoadingInitialChatHistory && <ChatLoading />}

                    {isNewAndCleanThread && <ChatSuggestions sendMessage={sendMessage} />}

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
                <NewMessageButton isVisible={showNewMessageButton} onClick={onClickNewMessageButton} />
                <ChatBoxFooter sendMessage={sendMessage} disableInput={disableInput} />
            </CopilotProvider>
        </div>
    );
};

export default memo(ChatBox);
