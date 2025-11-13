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
    messageContent,
    messageId,
    timeStamp,
    isTyping,
    threadId,
    sendMessage,
    updateApprovalOrCliMessageInStreamingMessage,
}: IAgentMessageProps) => {
    // Check if this is a scheduled task execution message
    const scheduledTaskData = useScheduledTaskMessage(messageContent.text || '');

    // Check if this is a trajectory insight message
    const isTrajectoryInsight = messageContent.isTrajectoryInsight || (messageContent.text || '').includes('# Session Insight');

    return (
        <>
            {/* For messages with approval - text content may be empty, so we may only need to render approval UI */}
            {messageContent.approval && !messageContent.agentTaskInfo ? (
                <ApprovalMessage
                    approval={messageContent.approval}
                    messageId={messageId}
                    threadId={threadId}
                    updateApprovalOrCliMessageInStreamingMessage={updateApprovalOrCliMessageInStreamingMessage}
                />
            ) : messageContent.changeDiff ? (
                <ChangeDiffMessage changeDiffData={messageContent.changeDiff} />
            ) : messageContent.isDailyReport ? (
                <DailyReportMessage text={messageContent.text} timeStamp={timeStamp} />
            ) : isTrajectoryInsight && messageContent.text ? (
                <SessionInsightCard insightText={messageContent.text} onRequestRefinement={sendMessage} />
            ) : scheduledTaskData.isScheduledTaskCreationMessage && scheduledTaskData.task ? (
                <ScheduledTaskCreationChatMessage task={scheduledTaskData.task} />
            ) : scheduledTaskData.isScheduledTaskMessage && scheduledTaskData.task ? (
                <ScheduledTaskExecutionChatMessage task={scheduledTaskData.task} executionTime={scheduledTaskData.executionTime} />
            ) : messageContent.azCliExecution ? (
                <ExecutionMessage
                    type={ExecutionMessageType.AzCli}
                    execution={messageContent.azCliExecution}
                    threadId={threadId}
                    updateApprovalOrCliMessageInStreamingMessage={updateApprovalOrCliMessageInStreamingMessage}
                />
            ) : messageContent.kubectlExecution ? (
                <ExecutionMessage
                    type={ExecutionMessageType.Kubectl}
                    execution={messageContent.kubectlExecution}
                    threadId={threadId}
                    updateApprovalOrCliMessageInStreamingMessage={updateApprovalOrCliMessageInStreamingMessage}
                />
            ) : messageContent.psqlExecution ? (
                <PsqlExecutionMessage
                    execution={messageContent.psqlExecution}
                    threadId={threadId}
                    updateApprovalOrCliMessageInStreamingMessage={updateApprovalOrCliMessageInStreamingMessage}
                />
            ) : messageContent.agentTaskInfo ? (
                <AgentTaskChatMessage
                    agentTask={messageContent.agentTaskInfo}
                    approval={messageContent.approval}
                    messageId={messageId}
                    threadId={threadId}
                    updateApprovalOrCliMessageInStreamingMessage={updateApprovalOrCliMessageInStreamingMessage}
                />
            ) : messageContent.todoInfo ? (
                <TodoPlanChatMessage todoPlan={messageContent.todoInfo} />
            ) : messageContent.error ? (
                <ErrorChatMessage error={messageContent.error} />
            ) : messageContent.memorySearchResult ? (
                <MemoryChatMessage memorySearchResult={messageContent.memorySearchResult} />
            ) : (messageContent.text || isTyping) &&
              !scheduledTaskData.isScheduledTaskMessage &&
              !scheduledTaskData.isScheduledTaskCreationMessage ? (
                <TextOrImageMessage text={messageContent.text} />
            ) : null}
        </>
    );
};

export default memo(AgentMessage);
