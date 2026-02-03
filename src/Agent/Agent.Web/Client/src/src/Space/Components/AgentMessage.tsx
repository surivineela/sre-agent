import { memo, useContext, useMemo } from 'react';
import { IAgentMessageProps } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { useScheduledTaskMessage } from '../Hooks/useScheduledTaskMessage';
import { useTaskToolExecutions } from '../Hooks/useTaskToolExecutions';
import AgentTaskChatMessage from './AgentTaskChatMessage';
import ApprovalMessage from './ApprovalMessage';
import ChangeDiffMessage from './ChangeDiffMessage';
import DailyReportMessage from './DailyReportMessage';
import ErrorChatMessage from './ErrorMessage';
import ExecutionMessage, { ExecutionMessageType } from './ExecutionMessage';
import KnowledgeGraphChatMessage from './KnowledgeGraphChatMessage';
import McpToolExecutionMessage from './McpToolExecutionMessage';
import MemoryChatMessage from './MemoryChatMessage';
import PsqlExecutionMessage from './PsqlExecutionMessage';
import ReasoningChatMessage from './ReasoningChatMessage';
import ScheduledTaskCreationChatMessage from './ScheduledTaskCreationChatMessage';
import ScheduledTaskExecutionChatMessage from './ScheduledTaskExecutionChatMessage';
import SessionInsightCard from './SessionInsightCard';
import TaskToolExecutionMessage from './TaskToolExecutionMessage';
import TextOrImageMessage from './TextOrImageMessage';
import TodoPlanChatMessage from './TodoPlanChatMessage';
import { ToolCallCard } from './ToolCallCard';
import UserQuestionMessage from './UserQuestionMessage';

const AgentMessage = ({
    message,
    messageId,
    timeStamp,
    isTyping,
    threadId,
    updateApprovalOrCliMessageInStreamingMessage,
    onSubmitUserQuestionResponse,
}: IAgentMessageProps) => {
    // Check if this is a scheduled task execution message
    const scheduledTaskData = useScheduledTaskMessage(message.text || '');

    // Check if this is a trajectory insight message
    const isTrajectoryInsight = (message.text || '').includes('# Session Insight');

    // Get streaming context for cancel functionality
    const { cancelTaskExecution } = useContext(StreamingContext);

    // Get real-time Task tool execution updates
    const { getExecutionById, getGroupById } = useTaskToolExecutions(threadId);

    // Merge streaming updates with static message data for Task tool executions
    const taskToolExecution = useMemo(() => {
        if (!message.taskToolExecution) return undefined;
        const streamingExec = getExecutionById(message.taskToolExecution.id);
        return streamingExec ?? message.taskToolExecution;
    }, [message.taskToolExecution, getExecutionById]);

    const taskToolExecutionGroup = useMemo(() => {
        if (!message.taskToolExecutionGroup) return undefined;
        const streamingGroup = getGroupById(message.taskToolExecutionGroup.id);
        return streamingGroup ?? message.taskToolExecutionGroup;
    }, [message.taskToolExecutionGroup, getGroupById]);

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
                <SessionInsightCard insightText={message.text} threadId={threadId} />
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
            ) : message.memorySearchResult && message.memorySearchResult.totalResults > 0 ? (
                <MemoryChatMessage memorySearchResult={message.memorySearchResult} />
            ) : message.knowledgeGraphSearchResult ? (
                <KnowledgeGraphChatMessage knowledgeGraphSearchResult={message.knowledgeGraphSearchResult} />
            ) : message.grepSearchResult ? (
                <ToolCallCard toolType="grep" grepResult={message.grepSearchResult} />
            ) : message.readFileResult ? (
                <ToolCallCard toolType="readfile" readFileResult={message.readFileResult} />
            ) : message.terminalResult ? (
                <ToolCallCard toolType="terminal" terminalResult={message.terminalResult} />
            ) : message.userQuestion ? (
                <UserQuestionMessage userQuestion={message.userQuestion} onSubmitResponse={onSubmitUserQuestionResponse} />
            ) : message.mcpToolExecution ? (
                <McpToolExecutionMessage execution={message.mcpToolExecution} />
            ) : taskToolExecutionGroup ? (
                <TaskToolExecutionMessage executionGroup={taskToolExecutionGroup} onCancelExecution={cancelTaskExecution} />
            ) : taskToolExecution ? (
                <TaskToolExecutionMessage execution={taskToolExecution} onCancelExecution={cancelTaskExecution} />
            ) : message.reasoning ? (
                <ReasoningChatMessage reasoning={message.reasoning} />
            ) : (message.text || isTyping) &&
              !scheduledTaskData.isScheduledTaskMessage &&
              !scheduledTaskData.isScheduledTaskCreationMessage ? (
                <TextOrImageMessage text={message.text} threadId={threadId} />
            ) : null}
        </>
    );
};

export default memo(AgentMessage);
