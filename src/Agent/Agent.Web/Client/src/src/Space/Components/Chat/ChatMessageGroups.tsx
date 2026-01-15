import { memo } from 'react';
import { UserQuestionResponse } from '../../../Common/Contracts/DataPlane/UserQuestion';
import { ChatMessageGroup } from '../../Contracts/Activities';
import ChatMessageGroupComponent from './ChatMessageGroupComponent';

interface IChatMessageGroupsProps {
    messageGroups?: ChatMessageGroup[];
    threadId: string;
    sendMessage?: (message: string) => Promise<void>;
    onSubmitUserQuestionResponse?: (questionId: string, response: UserQuestionResponse) => void;
}

const ChatMessageGroups = ({ messageGroups, threadId, sendMessage, onSubmitUserQuestionResponse }: IChatMessageGroupsProps) => {
    return messageGroups ? (
        <>
            {messageGroups.map(messageGroup => (
                <ChatMessageGroupComponent
                    key={messageGroup.id}
                    messageGroup={messageGroup}
                    threadId={threadId}
                    sendMessage={sendMessage}
                    onSubmitUserQuestionResponse={onSubmitUserQuestionResponse}
                />
            ))}
        </>
    ) : null;
};

export default memo(ChatMessageGroups);
