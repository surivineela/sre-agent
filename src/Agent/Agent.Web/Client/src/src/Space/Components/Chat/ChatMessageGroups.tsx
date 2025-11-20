import { memo } from 'react';
import { ChatMessageGroup } from '../../Contracts/Activities';
import ChatMessageGroupComponent from './ChatMessageGroupComponent';

interface IChatMessageGroupsProps {
    messageGroups?: ChatMessageGroup[];
    threadId: string;
    sendMessage?: (message: string) => Promise<void>;
}

const ChatMessageGroups = ({ messageGroups, threadId, sendMessage }: IChatMessageGroupsProps) => {
    return messageGroups ? (
        <>
            {messageGroups.map(messageGroup => (
                <ChatMessageGroupComponent
                    key={messageGroup.id}
                    messageGroup={messageGroup}
                    threadId={threadId}
                    sendMessage={sendMessage}
                />
            ))}
        </>
    ) : null;
};

export default memo(ChatMessageGroups);
