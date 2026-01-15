import { memo, useMemo } from 'react';
import { composeDefaultAgentMessage } from '../../Activities/Utility';
import { AgentMessageRegex, IChatMessageGroupProps } from '../../Contracts/Activities';
import ChatMessage from '../ChatMessage';

const ChatMessageGroupComponent = ({
    messageGroup: { id, userMessages, agentMessages },
    threadId,
    threadSource,
    isTyping,
    isStreamingMessage,
    toolCallText,
    isWaitingForStreamingMessages,
    sendMessage,
    updateApprovalOrCliMessageInStreamingMessage,
    onSubmitUserQuestionResponse,
}: IChatMessageGroupProps) => {
    const messagesToCopy = useMemo(
        () =>
            agentMessages.reduce((acc, msg) => {
                const content =
                    msg.text
                        .trim()
                        .replace(AgentMessageRegex.imageRegex, '[Image]')
                        .replace(AgentMessageRegex.mermaidRegex, '[Mermaid Diagram]')
                        .replace(AgentMessageRegex.chartRegex, '[Chart]') + '\n\n';

                if (content) {
                    return acc + content + '\n\n';
                }
                return acc;
            }, ''),
        [agentMessages]
    );

    const agentMessageToDisplay = useMemo(() => {
        return isStreamingMessage && isTyping && agentMessages.length === 0 ? [composeDefaultAgentMessage()] : agentMessages;
    }, [isStreamingMessage, isTyping, agentMessages]);

    return (
        <>
            <ChatMessage messages={userMessages} threadId={threadId} role={'User'} />
            {agentMessageToDisplay.length > 0 && (
                <ChatMessage
                    componentId={id}
                    messages={agentMessageToDisplay}
                    threadId={threadId}
                    isStreamingMessage={isStreamingMessage}
                    isWaitingForStreamingMessages={isWaitingForStreamingMessages}
                    threadSource={threadSource}
                    toolCallText={toolCallText}
                    messagesToCopy={messagesToCopy}
                    sendMessage={sendMessage}
                    updateApprovalOrCliMessageInStreamingMessage={updateApprovalOrCliMessageInStreamingMessage}
                    onSubmitUserQuestionResponse={onSubmitUserQuestionResponse}
                    isTyping={isTyping}
                    role={'SREAgent'}
                />
            )}
        </>
    );
};

export default memo(ChatMessageGroupComponent);
