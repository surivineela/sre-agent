import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { Button, mergeClasses } from '@fluentui/react-components';
import { ArrowDownRegular } from '@fluentui/react-icons';
import { memo } from 'react';
import ChatLoading from '../Components/ChatLoading';
import ChatMessage from '../Components/ChatMessage';
import Input from '../Components/Input';
import { IChatBoxProps } from '../Contracts/Activities';
import { useChatBox } from '../Hooks/useChatBox';
import { ChatBoxStyles } from '../Styles/Activities.styles';

export const ChatBox = ({ addThread, threadId }: IChatBoxProps) => {
    const {
        messages,
        temporaryUserMessage,
        agentTypingMessage,
        isLoadingInitialChatHistory,
        sendMessage,
        disableInput,
        messagesDivRef,
        onClickDownButton,
        isDownButtonVisible,
        intersectionObserverRef,
        currentThreadId,
        cancelResponse,
    } = useChatBox(addThread, threadId);

    return (
        <div className={ChatBoxStyles.chatBox}>
            <CopilotProvider mode="canvas" className={ChatBoxStyles.root}>
                <div className={ChatBoxStyles.chatContainer} ref={messagesDivRef}>
                    <CopilotChat className={ChatBoxStyles.chat}>
                        <div ref={intersectionObserverRef} />
                        {isLoadingInitialChatHistory && <ChatLoading />}
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
                <Button
                    icon={<ArrowDownRegular />}
                    className={mergeClasses(ChatBoxStyles.arrowDownButton, isDownButtonVisible ? undefined : ChatBoxStyles.hiddenButton)}
                    onClick={onClickDownButton}
                />
                <Input sendMessage={sendMessage} disableInput={disableInput} />
            </CopilotProvider>
        </div>
    );
};

export default memo(ChatBox);
