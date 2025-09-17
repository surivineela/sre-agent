import {
    CopilotMessageV2 as CopilotMessage,
    CopilotMessageV2Props as CopilotMessageProps,
    UserMessageV2 as UserMessage,
} from '@fluentui-copilot/react-copilot-chat';
import { mergeStyleSets } from '@fluentui/react';
import { Image, mergeClasses, Text, tokens } from '@fluentui/react-components';
import mermaid from 'mermaid';
import { memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import ReactMarkdownComponent from '../../Common/Components/ReactMarkdownComponent';
import { getAgentModeDisplayName } from '../../Common/Helpers/AgentMode';
import { formatDateTimeWithShortYear, getSafeDateTime } from '../../Common/Helpers/Date';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { shouldGroupWithPreviousMessage } from '../Activities/Utility';
import { IChatMessageProps } from '../Contracts/Activities';
import { ThreadAgentModeContext } from '../Contracts/Context';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';
import { useScheduledTaskMessage } from '../Hooks/useScheduledTaskMessage';
import { getChatBoxV2Styles, nameAndTimestampContainerStyle, useChatBoxStyles } from '../Styles/Activities.styles';
import AgentMessage from './AgentMessage';
import AgentMessageLoadingComponent from './AgentMessageLoadingComponent';
import ChatMessageFooter from './ChatMessageFooter';
import ConnectionErrorComponent from './ConnectionErrorComponent';
import DeepInvestigationStatusMessage from './DeepInvestigationStatusMessage';
import ScheduledTaskCreationCard from './ScheduledTaskCreationCard';
import ScheduledTaskExecutionCard from './ScheduledTaskExecutionCard';

const chatMessageStyles = mergeStyleSets({
    regularMessageContent: {
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '0px 16px',
        borderRadius: tokens.borderRadiusXLarge,
    },
    codeBlock: {
        backgroundColor: tokens.colorNeutralBackground6,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'inline-block',
        padding: '2px 4px',
        borderRadius: tokens.borderRadiusSmall,
    },
    codeBlockInPre: {
        backgroundColor: tokens.colorTransparentBackground,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'block',
    },
    preBlock: {
        overflowX: 'auto',
        overflowY: 'hidden',
        backgroundColor: tokens.colorNeutralBackground6,
        borderRadius: tokens.borderRadiusSmall,
        padding: '15px',
    },
    toolCallText: {
        background: `linear-gradient(90deg, ${tokens.colorNeutralForeground3}, ${tokens.colorNeutralBackground6}, ${tokens.colorNeutralForeground1})`,
        backgroundSize: '200% 100%',
        backgroundClip: 'text',
        color: 'transparent',
        animation: 'shimmer 2s infinite linear',
        marginBottom: `${tokens.spacingVerticalS}px`,
    },
});

// Initialize mermaid with default configuration
mermaid.initialize({
    startOnLoad: false,
    theme: 'neutral',
    flowchart: { useMaxWidth: false },
    securityLevel: 'loose',
});

const ChatMessage = ({
    message,
    previousMessage,
    nextMessage,
    isTyping,
    threadId,
    threadSource,
    isStreamingMessage,
    toolCallText,
    isWaitingForStreamingMessages,
    updateSpecialMessageInStreamingMessage,
}: IChatMessageProps) => {
    const chatStyles = useChatBoxStyles();
    const chatBoxStyles = getChatBoxV2Styles();
    const intl = useIntl();

    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    const { threadAgentModeToDisplay } = useContext(ThreadAgentModeContext);

    const agentMessageProps = useMemo(() => {
        const messageProps: CopilotMessageProps = {
            avatar: <Image src="./SreAgent.svg" width={28} height={28} alt={intl.formatMessage(SreAgentResources.azureSreAgent)} />,
            loadingState: 'none',
            mode: 'canvas',
            name: (
                <div style={nameAndTimestampContainerStyle}>
                    <span>{intl.formatMessage(SreAgentResources.sreAgent)}</span>
                    {threadAgentModeToDisplay && (
                        <span className={chatStyles.modePill}>{getAgentModeDisplayName(threadAgentModeToDisplay, intl)}</span>
                    )}
                    {!isTyping && message.timeStamp && (
                        <Text size={200} color={tokens.colorNeutralForeground3}>
                            {formatDateTimeWithShortYear(getSafeDateTime(message.timeStamp))}
                        </Text>
                    )}
                </div>
            ),
            disclaimer: null,
        };

        return messageProps;
    }, [intl, threadAgentModeToDisplay, chatStyles.modePill, isTyping, message.timeStamp]);

    // Hide message's icon, name and timestamp if the message is grouped with the previous one
    const hideMessageHeader = useMemo(() => shouldGroupWithPreviousMessage(message, previousMessage), [message, previousMessage]);

    // Check if user message contains a scheduled task execution (called at top level to satisfy React hooks rules)
    const userMessageText = message.contents?.[0]?.text || '';
    const userScheduledTaskData = useScheduledTaskMessage(userMessageText);

    // If the previous message is an agent scheduled task creation/execution card and this user message only repeats the same
    // structured marker (e.g., user echo of system JSON), suppress rendering to reduce noise.
    const suppressEchoedScheduledTask = useMemo(() => {
        if (!previousMessage) return false;
        const prevText = previousMessage.contents?.[0]?.text || '';
        // Only consider suppression if current is user plain text and not parsed into special card.
        if (userScheduledTaskData.isScheduledTaskMessage || userScheduledTaskData.isScheduledTaskCreationMessage) return false;
        const hasMarker = /\[(SCHEDULED_TASK_CREATED|SCHEDULED_TASK_EXECUTION)\]/.test(userMessageText);
        if (!hasMarker) return false;
        // If previous already had the same marker type, hide this one.
        const prevMarkerMatch = prevText.match(/\[(SCHEDULED_TASK_CREATED|SCHEDULED_TASK_EXECUTION)\]/);
        const currMarkerMatch = userMessageText.match(/\[(SCHEDULED_TASK_CREATED|SCHEDULED_TASK_EXECUTION)\]/);
        if (prevMarkerMatch && currMarkerMatch && prevMarkerMatch[1] === currMarkerMatch[1]) {
            return true;
        }
        return false;
    }, [
        previousMessage,
        userMessageText,
        userScheduledTaskData.isScheduledTaskMessage,
        userScheduledTaskData.isScheduledTaskCreationMessage,
    ]);

    const Loading = () => {
        return (
            isTyping &&
            !toolCallText &&
            isWaitingForStreamingMessages && (
                <div style={{ margin: '5px 5px 0px 5px' }}>
                    <AgentMessageLoadingComponent />
                </div>
            )
        );
    };

    const ToolCallTextComponent = () => {
        const styles = `
            @keyframes shimmer {
                0% {
                    background-position: 100% 0;
                }
                100% {
                    background-position: -100% 0;
                }
            }
        `;

        return (
            isTyping &&
            toolCallText && (
                <>
                    <style>{styles}</style>
                    <Text className={chatMessageStyles.toolCallText}>{toolCallText}</Text>
                </>
            )
        );
    };

    if (message.contents[0]?.deepInvestigationStatus) {
        return <DeepInvestigationStatusMessage isDeepInvestigationTurnedOn={message.contents[0].deepInvestigationStatus.enabled} />;
    }

    switch (message.author.role) {
        case 'SREAgent':
            return (
                <div style={isStreamingMessage ? { minHeight: 'calc(100% - 120px)' } : undefined}>
                    <CopilotMessage
                        {...agentMessageProps}
                        key={message.id}
                        style={{ font: 'Segoe UI', lineHeight: '20px', wordBreak: 'unset', maxWidth: '90%' }}
                        className={mergeClasses(
                            chatBoxStyles.agentMessage,
                            hideMessageHeader ? chatBoxStyles.hideAgentMessageHeader : undefined
                        )}
                    >
                        {message.contents.map((content, index) => {
                            return (
                                <AgentMessage
                                    key={index}
                                    messageContent={content}
                                    messageId={message.id}
                                    timeStamp={message.timeStamp}
                                    isTyping={isTyping}
                                    threadId={threadId}
                                    updateSpecialMessageInStreamingMessage={
                                        isStreamingMessage ? updateSpecialMessageInStreamingMessage : undefined
                                    }
                                />
                            );
                        })}

                        <ConnectionErrorComponent key={`${message.id}-connection-error`} isStreamingMessage={isStreamingMessage} />
                        <ToolCallTextComponent key={`${message.id}-tool-call-text`} />
                        <Loading key={`${message.id}-loading`} />

                        <ChatMessageFooter
                            key={`${message.id}-message-footer`}
                            message={message}
                            threadId={threadId}
                            threadSource={threadSource}
                            nextMessage={nextMessage}
                            isTyping={isTyping}
                            isStreamingMessage={isStreamingMessage}
                        />
                    </CopilotMessage>
                </div>
            );
        default:
            return (
                <div className={chatBoxStyles.userMessage} key={message.id}>
                    {hideMessageHeader ? null : (
                        <div style={nameAndTimestampContainerStyle}>
                            {message.author.userId !== userIdAndDisplayName.userId && (
                                <Text block={true} weight={'semibold'} className={chatStyles.userName}>
                                    {message.author.displayName}
                                </Text>
                            )}
                            <Text size={200} color={tokens.colorNeutralForeground3} style={{ lineHeight: '26px' }}>
                                {formatDateTimeWithShortYear(getSafeDateTime(message.timeStamp))}
                            </Text>
                        </div>
                    )}
                    {suppressEchoedScheduledTask ? null : userScheduledTaskData.isScheduledTaskCreationMessage &&
                      userScheduledTaskData.task ? (
                        <ScheduledTaskCreationCard
                            data={{
                                taskId: userScheduledTaskData.task.id,
                                taskName: userScheduledTaskData.task.name,
                                description: userScheduledTaskData.task.description,
                                cronExpression: userScheduledTaskData.task.cronExpression,
                                agentPrompt: userScheduledTaskData.task.agentPrompt,
                                status: userScheduledTaskData.task.status,
                                durationText: 'No limit',
                                maxExecutionsText: 'No limit',
                                createdAt: userScheduledTaskData.task.createdAt,
                            }}
                        />
                    ) : suppressEchoedScheduledTask ? null : userScheduledTaskData.isScheduledTaskMessage && userScheduledTaskData.task ? (
                        <ScheduledTaskExecutionCard
                            task={userScheduledTaskData.task}
                            executionTime={userScheduledTaskData.executionTime?.toISOString()}
                        />
                    ) : (
                        <UserMessage
                            className={chatStyles.userBubble}
                            message={{ className: chatStyles.userBubbleMessage }}
                            key={message.id}
                        >
                            <ReactMarkdownComponent key={message.id} content={userMessageText} variant="chat" isUserMessage />
                        </UserMessage>
                    )}
                </div>
            );
    }
};

export default memo(ChatMessage);
