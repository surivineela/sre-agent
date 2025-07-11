import { memo, useMemo } from 'react';
import { ChatMessageError } from '../../Common/Contracts/Azure/SreAgent';
import { composeDefaultAgentMessage } from '../Activities/Utility';
import { ChatMessage } from '../Contracts/Activities';
import ChatMessageV2 from './ChatMessageV2';

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
                text: '',
                error,
            },
        ];

        return agentMessage;
    }, [error]);

    return <ChatMessageV2 message={message} previousMessage={previousMessage} nextMessage={nextMessage} threadId="" />;
};

export default memo(ErrorChatMessage);
