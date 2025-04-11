import { memo, useEffect, useRef } from 'react';
import { IChatBoxProps } from '../Contracts/Activities';
import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { ChatBoxStyles } from '../Styles/Activities.styles';
import Input from '../Components/Input';
import ChatMessage from '../Components/ChatMessage';
import { useChatBox } from '../Hooks/useChatBox';
import ChatLoading from '../Components/ChatLoading';

export const ChatBox = ({ addThread, threadId }: IChatBoxProps) => {
  const { messages, temporaryUserMessage, agentTypingMessage, shouldLoadHistory, sendMessage, disableInput } = useChatBox(
    addThread,
    threadId
  );
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollTo({ top: messagesEndRef.current.scrollHeight, behavior: 'smooth' });
  });

  return (
    <div className={ChatBoxStyles.chatBox}>
    <CopilotProvider mode="canvas" className={ChatBoxStyles.root}>
      <div className={ChatBoxStyles.chatContainer} ref={messagesEndRef}>
        <CopilotChat className={ChatBoxStyles.chat}>
          {shouldLoadHistory && <ChatLoading />}
          {messages.map((message, index) => (
            <ChatMessage key={index} message={message} threadId={threadId || ''} />
          ))}
          {temporaryUserMessage && <ChatMessage message={temporaryUserMessage} threadId={threadId || ''} />}
          {agentTypingMessage && <ChatMessage message={agentTypingMessage} isTyping threadId={threadId || ''} />}
        </CopilotChat>
      </div>
      <Input sendMessage={sendMessage} disableInput={disableInput} />
    </CopilotProvider>
    </div>
  );
};

export default memo(ChatBox);
