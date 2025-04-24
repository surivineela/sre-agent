import { memo } from 'react';
import { IChatBoxProps } from '../Contracts/Activities';
import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { ChatBoxStyles } from '../Styles/Activities.styles';
import Input from '../Components/Input';
import ChatMessage from '../Components/ChatMessage';
import { useChatBox } from '../Hooks/useChatBox';
import ChatLoading from '../Components/ChatLoading';
import { ArrowDownRegular } from '@fluentui/react-icons';
import { Button, mergeClasses } from '@fluentui/react-components';

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
    currentThreadId
  } = useChatBox(
    addThread,
    threadId
  );

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
            {agentTypingMessage && <ChatMessage message={agentTypingMessage} isTyping threadId={currentThreadId || ''} />}
          </CopilotChat>
        </div>
        <Button icon={<ArrowDownRegular />} className={mergeClasses(ChatBoxStyles.arrowDownButton, isDownButtonVisible ? undefined : ChatBoxStyles.hiddenButton)} onClick={onClickDownButton} />
        <Input sendMessage={sendMessage} disableInput={disableInput} />
      </CopilotProvider>
    </div>
  );
};

export default memo(ChatBox);
