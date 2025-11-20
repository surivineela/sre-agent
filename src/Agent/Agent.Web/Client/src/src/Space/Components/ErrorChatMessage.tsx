import { memo, useMemo } from 'react';
import { ChatMessageError } from '../../Common/Contracts/DataPlane/Message';
import { composeDefaultAgentMessage } from '../Activities/Utility';
import { ChatMessage } from '../Contracts/Activities';
import ChatMessageComponent from './ChatMessage';

/**
 * A chat message component that only shows error message
 */
const ErrorChatMessage = ({ error }: { error?: ChatMessageError }) => {
    const messages = useMemo((): ChatMessage[] => {
        const agentMessage = composeDefaultAgentMessage();
        return [
            {
                ...agentMessage,
                error,
            },
        ];
    }, [error]);

    return <ChatMessageComponent messages={messages} threadId="" role={'SREAgent'} />;
};

export default memo(ErrorChatMessage);
