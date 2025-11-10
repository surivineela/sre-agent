import { memo, useMemo } from 'react';
import { ChatMessageError } from '../../Common/Contracts/DataPlane/Message';
import { composeDefaultAgentMessage } from '../Activities/Utility';
import { ChatMessage } from '../Contracts/Activities';
import ChatMessageComponent from './ChatMessage';

/**
 * A chat message component that only shows error message
 */
const ErrorChatMessage = ({
    error,
    previousMessage,
    nextMessage,
}: {
    error?: ChatMessageError;
    previousMessage?: ChatMessage;
    nextMessage?: ChatMessage;
}) => {
    const message = useMemo(() => {
        const agentMessage = composeDefaultAgentMessage();
        agentMessage.contents = [
            {
                messageId: '',
                text: '',
                error,
            },
        ];

        return agentMessage;
    }, [error]);

    return <ChatMessageComponent message={message} previousMessage={previousMessage} nextMessage={nextMessage} threadId="" />;
};

export default memo(ErrorChatMessage);
