import { memo } from 'react';
import { IAgentMessageProps } from '../Contracts/Activities';
import ApprovalMessage from './ApprovalMessage';
import AzCliExecutionMessage from './AzCliExecutionMessage';
import DailyReportMessage from './DailyReportMessage';
import ErrorChatMessage from './ErrorMessage';
import KubectlExecutionMessage from './KubectlExecutionMessage';
import TextOrImageMessage from './TextOrImageMessage';

const AgentMessage = ({
    messageContent,
    messageId,
    timeStamp,
    isTyping,
    threadId,
    updateSpecialMessageInStreamingMessage,
}: IAgentMessageProps) => {
    return (
        <>
            {/* For messages with approval - text content may be empty, so we may only need to render approval UI */}
            {messageContent.approval ? (
                <ApprovalMessage
                    approval={messageContent.approval}
                    messageId={messageId}
                    threadId={threadId}
                    updateSpecialMessageInStreamingMessage={updateSpecialMessageInStreamingMessage}
                />
            ) : messageContent.isDailyReport ? (
                <DailyReportMessage text={messageContent.text} timeStamp={timeStamp} />
            ) : messageContent.azCliExecution ? (
                <AzCliExecutionMessage
                    execution={messageContent.azCliExecution}
                    threadId={threadId}
                    updateSpecialMessageInStreamingMessage={updateSpecialMessageInStreamingMessage}
                />
            ) : messageContent.kubectlExecution ? (
                <KubectlExecutionMessage
                    execution={messageContent.kubectlExecution}
                    threadId={threadId}
                    updateSpecialMessageInStreamingMessage={updateSpecialMessageInStreamingMessage}
                />
            ) : messageContent.error ? (
                <ErrorChatMessage error={messageContent.error} />
            ) : messageContent.text || isTyping ? (
                <TextOrImageMessage text={messageContent.text} />
            ) : null}
        </>
    );
};

export default memo(AgentMessage);
