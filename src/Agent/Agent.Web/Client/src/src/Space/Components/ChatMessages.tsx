import { memo } from 'react';
import { ChatMessage } from '../Contracts/Activities';
import ChatMessageV2 from './ChatMessageV2';

interface IChatMessagesProps {
    messages?: ChatMessage[];
    threadId: string;
    prevMessageBeforeTheFistMessage?: ChatMessage;
    nextMessageAfterTheLastMessage?: ChatMessage;
}

const ChatMessages = ({ messages, threadId, prevMessageBeforeTheFistMessage, nextMessageAfterTheLastMessage }: IChatMessagesProps) => {
    return messages ? (
        <>
            {messages.map((message, index) => (
                <ChatMessageV2
                    key={message.id}
                    message={message}
                    threadId={threadId}
                    previousMessage={index === 0 ? prevMessageBeforeTheFistMessage : messages[index - 1]}
                    nextMessage={index === messages.length - 1 ? nextMessageAfterTheLastMessage : messages[index + 1]}
                />
            ))}
        </>
    ) : null;
};

export default memo(ChatMessages);
