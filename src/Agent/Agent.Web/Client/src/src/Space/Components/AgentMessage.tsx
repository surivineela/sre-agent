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
import ScheduledTaskCreationCard from './ScheduledTaskCreationCard';
import ScheduledTaskExecutionCard from './ScheduledTaskExecutionCard';
import TextOrImageMessage from './TextOrImageMessage';
import TodoPlanChatMessage from './TodoPlanChatMessage';

const AgentMessage = ({
    messageContent,
    messageId,
    timeStamp,
    isTyping,
    threadId,
    updateSpecialMessageInStreamingMessage,
}: IAgentMessageProps) => {
    // Check if this is a scheduled task execution message
    const scheduledTaskData = useScheduledTaskMessage(messageContent.text || '');

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
            ) : messageContent.changeDiff ? (
                <ChangeDiffMessage changeDiffData={messageContent.changeDiff} />
            ) : messageContent.isDailyReport ? (
                <DailyReportMessage text={messageContent.text} timeStamp={timeStamp} />
            ) : scheduledTaskData.isScheduledTaskCreationMessage && scheduledTaskData.task ? (
                <ScheduledTaskCreationCard
                    data={{
                        taskId: scheduledTaskData.task.id,
                        taskName: scheduledTaskData.task.name,
                        description: scheduledTaskData.task.description,
                        cronExpression: scheduledTaskData.task.cronExpression,
                        agentPrompt: scheduledTaskData.task.agentPrompt,
                        status: scheduledTaskData.task.status,
                        durationText: 'No limit',
                        maxExecutionsText: 'No limit',
                        createdAt: scheduledTaskData.task.createdAt,
                    }}
                />
            ) : scheduledTaskData.isScheduledTaskMessage && scheduledTaskData.task ? (
                <ScheduledTaskExecutionCard task={scheduledTaskData.task} executionTime={scheduledTaskData.executionTime?.toISOString()} />
            ) : messageContent.azCliExecution ? (
                <ExecutionMessage
                    type={ExecutionMessageType.AzCli}
                    execution={messageContent.azCliExecution}
                    threadId={threadId}
                    updateSpecialMessageInStreamingMessage={updateSpecialMessageInStreamingMessage}
                />
            ) : messageContent.kubectlExecution ? (
                <ExecutionMessage
                    type={ExecutionMessageType.Kubectl}
                    execution={messageContent.kubectlExecution}
                    threadId={threadId}
                    updateSpecialMessageInStreamingMessage={updateSpecialMessageInStreamingMessage}
                />
            ) : messageContent.psqlExecution ? (
                <PsqlExecutionMessage
                    execution={messageContent.psqlExecution}
                    threadId={threadId}
                    updateSpecialMessageInStreamingMessage={updateSpecialMessageInStreamingMessage}
                />
            ) : messageContent.agentTaskInfo ? (
                <AgentTaskChatMessage agentTask={messageContent.agentTaskInfo} />
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
