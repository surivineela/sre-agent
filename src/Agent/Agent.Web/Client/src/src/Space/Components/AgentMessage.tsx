import { memo } from 'react';
import { IAgentMessageProps } from '../Contracts/Activities';
import { useScheduledTaskMessage } from '../Hooks/useScheduledTaskMessage';
import AgentTaskChatMessage from './AgentTaskChatMessage';
import ApprovalMessage from './ApprovalMessage';
import ChangeDiffMessage from './ChangeDiffMessage';
import DailyReportMessage from './DailyReportMessage';
import ErrorChatMessage from './ErrorMessage';
import ExecutionMessage, { ExecutionMessageType } from './ExecutionMessage';
import MemoryChatMessage from './MemoryChatMessage';
import PsqlExecutionMessage from './PsqlExecutionMessage';
import ScheduledTaskCreationChatMessage from './ScheduledTaskCreationChatMessage';
import ScheduledTaskExecutionChatMessage from './ScheduledTaskExecutionChatMessage';
import SessionInsightCard from './SessionInsightCard';
import TextOrImageMessage from './TextOrImageMessage';
import TodoPlanChatMessage from './TodoPlanChatMessage';

const AgentMessage = ({
    message,
    messageId,
    timeStamp,
    isTyping,
    threadId,
    sendMessage,
    updateApprovalOrCliMessageInStreamingMessage,
}: IAgentMessageProps) => {
    // Check if this is a scheduled task execution message
    const scheduledTaskData = useScheduledTaskMessage(message.text || '');

    // Check if this is a trajectory insight message
    const isTrajectoryInsight = (message.text || '').includes('# Session Insight');

    return (
        <>
            {/* For messages with approval - text content may be empty, so we may only need to render approval UI */}
            {message.approval && !message.agentTaskInfo ? (
                <ApprovalMessage
                    approval={message.approval}
                    messageId={messageId}
                    threadId={threadId}
                    updateApprovalOrCliMessageInStreamingMessage={updateApprovalOrCliMessageInStreamingMessage}
                />
            ) : message.changeDiff ? (
                <ChangeDiffMessage changeDiffData={message.changeDiff} />
            ) : message.isDailyReport ? (
                <DailyReportMessage text={message.text} timeStamp={timeStamp} />
            ) : isTrajectoryInsight && message.text ? (
                <SessionInsightCard insightText={message.text} onRequestRefinement={sendMessage} />
            ) : scheduledTaskData.isScheduledTaskCreationMessage && scheduledTaskData.task ? (
                <ScheduledTaskCreationChatMessage task={scheduledTaskData.task} />
            ) : scheduledTaskData.isScheduledTaskMessage && scheduledTaskData.task ? (
                <ScheduledTaskExecutionChatMessage task={scheduledTaskData.task} executionTime={scheduledTaskData.executionTime} />
            ) : message.azCliExecution ? (
                <ExecutionMessage
                    type={ExecutionMessageType.AzCli}
                    execution={message.azCliExecution}
                    threadId={threadId}
                    updateApprovalOrCliMessageInStreamingMessage={updateApprovalOrCliMessageInStreamingMessage}
                />
            ) : message.kubectlExecution ? (
                <ExecutionMessage
                    type={ExecutionMessageType.Kubectl}
                    execution={message.kubectlExecution}
                    threadId={threadId}
                    updateApprovalOrCliMessageInStreamingMessage={updateApprovalOrCliMessageInStreamingMessage}
                />
            ) : message.psqlExecution ? (
                <PsqlExecutionMessage
                    execution={message.psqlExecution}
                    threadId={threadId}
                    updateApprovalOrCliMessageInStreamingMessage={updateApprovalOrCliMessageInStreamingMessage}
                />
            ) : message.agentTaskInfo ? (
                <AgentTaskChatMessage
                    agentTask={message.agentTaskInfo}
                    approval={message.approval || undefined}
                    messageId={messageId}
                    threadId={threadId}
                    updateApprovalOrCliMessageInStreamingMessage={updateApprovalOrCliMessageInStreamingMessage}
                />
            ) : message.todoInfo ? (
                <TodoPlanChatMessage todoPlan={message.todoInfo} />
            ) : message.error ? (
                <ErrorChatMessage error={message.error} />
            ) : message.memorySearchResult ? (
                <MemoryChatMessage memorySearchResult={message.memorySearchResult} />
            ) : (message.text || isTyping) &&
              !scheduledTaskData.isScheduledTaskMessage &&
              !scheduledTaskData.isScheduledTaskCreationMessage ? (
                <TextOrImageMessage text={message.text} />
            ) : null}
        </>
    );
};

export default memo(AgentMessage);
