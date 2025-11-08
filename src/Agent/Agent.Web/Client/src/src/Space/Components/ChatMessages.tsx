import { memo } from 'react';
import { ChatMessage } from '../Contracts/Activities';
import ChatMessageComponent from './ChatMessage';

interface IChatMessagesProps {
    messages?: ChatMessage[];
    threadId: string;
    prevMessageBeforeTheFirstMessage?: ChatMessage;
    nextMessageAfterTheLastMessage?: ChatMessage;
    sendMessage?: (message: string) => Promise<void>;
}

const ChatMessages = ({
    messages,
    threadId,
    prevMessageBeforeTheFirstMessage,
    nextMessageAfterTheLastMessage,
    sendMessage,
}: IChatMessagesProps) => {
    return messages ? (
        <>
            {messages.map((message, index) => (
                <ChatMessageComponent
                    key={message.id}
                    message={message}
                    threadId={threadId}
                    previousMessage={index === 0 ? prevMessageBeforeTheFirstMessage : messages[index - 1]}
                    nextMessage={index === messages.length - 1 ? nextMessageAfterTheLastMessage : messages[index + 1]}
                    sendMessage={sendMessage}
                />
            ))}
        </>
    ) : null;
};

export default memo(ChatMessages);
