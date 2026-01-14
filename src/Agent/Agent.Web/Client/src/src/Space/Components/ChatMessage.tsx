import { CopilotMessage, CopilotMessageProps, UserMessage } from '@fluentui-copilot/react-copilot-chat';
import { Badge, Image, Text, Tooltip, tokens } from '@fluentui/react-components';
import { Bookmark16Regular, Search16Regular } from '@fluentui/react-icons';
import mermaid from 'mermaid';
import { memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import ReactMarkdownComponent from '../../Common/Components/ReactMarkdownComponent';
import { getAgentModeDisplayName } from '../../Common/Helpers/AgentMode';
import { formatDateTimeWithShortYear, getSafeDateTime } from '../../Common/Helpers/Date';
import { Guid } from '../../Common/Helpers/Guid';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { IChatMessageProps } from '../Contracts/Activities';
import { ThreadAgentModeContext } from '../Contracts/Context';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';
import { getScheduledTaskMessage } from '../Hooks/useScheduledTaskMessage';
import { getChatBoxStyles, nameAndTimestampContainerStyle, useChatBoxStyles } from '../Styles/Activities.styles';
import AgentMessage from './AgentMessage';
import AgentMessageLoadingComponent from './AgentMessageLoadingComponent';
import ChatMessageFooter from './ChatMessageFooter';
import ConnectionErrorComponent from './ConnectionErrorComponent';
import DeepInvestigationStatusMessage from './DeepInvestigationStatusMessage';
import ScheduledTaskCreationChatMessage from './ScheduledTaskCreationChatMessage';
import ScheduledTaskExecutionChatMessage from './ScheduledTaskExecutionChatMessage';

// Initialize mermaid with default configuration
mermaid.initialize({
    startOnLoad: false,
    theme: 'neutral',
    flowchart: { useMaxWidth: false },
    securityLevel: 'loose',
});

const ChatMessage = ({
    componentId,
    messages,
    role,
    isTyping,
    threadId,
    isStreamingMessage,
    threadSource,
    toolCallText,
    isWaitingForStreamingMessages,
    messagesToCopy,
    sendMessage,
    updateApprovalOrCliMessageInStreamingMessage,
}: IChatMessageProps) => {
    const chatStyles = useChatBoxStyles();
    const chatBoxStyles = getChatBoxStyles();
    const intl = useIntl();

    const id = useMemo(() => componentId || Guid.newGuid(), [componentId]);

    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    const { threadAgentModeToDisplay } = useContext(ThreadAgentModeContext);

    const agentMessageProps = useMemo(() => {
        const messageProps: CopilotMessageProps = {
            avatar: <Image src="./SreAgent.svg" width={28} height={28} alt={intl.formatMessage(SreAgentResources.azureSreAgent)} />,
            loadingState: 'none',
            mode: 'canvas',
            name: (
                <div style={nameAndTimestampContainerStyle}>
                    <span>{intl.formatMessage(SreAgentResources.azureSreAgent)}</span>
                    {threadAgentModeToDisplay && (
                        <span className={chatStyles.modePill}>{getAgentModeDisplayName(threadAgentModeToDisplay, intl)}</span>
                    )}
                    {!isTyping && messages[0]?.timeStamp && (
                        <Text size={200} color={tokens.colorNeutralForeground3}>
                            {formatDateTimeWithShortYear(getSafeDateTime(messages[0].timeStamp))}
                        </Text>
                    )}
                </div>
            ),
            disclaimer: null,
        };

        return messageProps;
    }, [intl, threadAgentModeToDisplay, chatStyles.modePill, isTyping, messages[0]?.timeStamp]);

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
                    <Text className={chatBoxStyles.toolCallText}>{toolCallText}</Text>
                </>
            )
        );
    };

    if (messages[0]?.deepInvestigationStatus) {
        return <DeepInvestigationStatusMessage isDeepInvestigationTurnedOn={messages[0].deepInvestigationStatus.enabled} />;
    }

    switch (role) {
        case 'SREAgent':
            return (
                <div style={isStreamingMessage ? { minHeight: 'calc(100% - 120px)' } : undefined}>
                    <CopilotMessage
                        {...agentMessageProps}
                        key={componentId}
                        style={{
                            font: 'Segoe UI',
                            lineHeight: '20px',
                            wordBreak: 'unset',
                            maxWidth: '90%',
                        }}
                        className={chatBoxStyles.agentMessage}
                    >
                        {messages.map(message => {
                            return (
                                <AgentMessage
                                    key={message.id}
                                    message={message}
                                    messageId={message.id}
                                    timeStamp={message.timeStamp}
                                    isTyping={isTyping}
                                    threadId={threadId}
                                    sendMessage={sendMessage}
                                    updateApprovalOrCliMessageInStreamingMessage={
                                        isStreamingMessage ? updateApprovalOrCliMessageInStreamingMessage : undefined
                                    }
                                />
                            );
                        })}

                        <ConnectionErrorComponent key={`${id}-connection-error`} isStreamingMessage={isStreamingMessage} />
                        <ToolCallTextComponent key={`${id}-tool-call-text`} />
                        <Loading key={`${id}-loading`} />

                        <ChatMessageFooter
                            key={`${id}-message-footer`}
                            messageContent={messagesToCopy || ''}
                            threadId={threadId}
                            threadSource={threadSource}
                            isTyping={isTyping}
                        />
                    </CopilotMessage>
                </div>
            );
        default:
            return messages.map((message, index) => {
                const userScheduledTaskData = getScheduledTaskMessage(message.text || '');
                // Detect memory command from message text if not explicitly set
                const memoryCommand =
                    message.memoryCommand ||
                    (message.text?.startsWith('#remember ') ? 'remember' : null) ||
                    (message.text?.startsWith('#retrieve ') ? 'retrieve' : null);
                return (
                    <div className={chatBoxStyles.userMessage} key={message.id}>
                        {index !== 0 ? null : (
                            <div style={nameAndTimestampContainerStyle}>
                                {message.author.userId !== userIdAndDisplayName.userId && (
                                    <Text block={true} weight={'semibold'} className={chatStyles.userName}>
                                        {message.author.displayName}
                                    </Text>
                                )}
                                {memoryCommand && (
                                    <Tooltip
                                        content={intl.formatMessage(
                                            memoryCommand === 'remember'
                                                ? ActivitiesResources.memoryRememberBadge
                                                : ActivitiesResources.memoryRetrieveBadge
                                        )}
                                        relationship="label"
                                    >
                                        <Badge
                                            appearance="tint"
                                            size="small"
                                            color="informative"
                                            icon={memoryCommand === 'remember' ? <Bookmark16Regular /> : <Search16Regular />}
                                            style={{ marginRight: tokens.spacingHorizontalS }}
                                        />
                                    </Tooltip>
                                )}
                                <Text size={200} color={tokens.colorNeutralForeground3} style={{ lineHeight: '26px' }}>
                                    {formatDateTimeWithShortYear(getSafeDateTime(message.timeStamp))}
                                </Text>
                            </div>
                        )}
                        {userScheduledTaskData.isScheduledTaskCreationMessage && userScheduledTaskData.task ? (
                            <ScheduledTaskCreationChatMessage task={userScheduledTaskData.task} />
                        ) : userScheduledTaskData.isScheduledTaskMessage && userScheduledTaskData.task ? (
                            <ScheduledTaskExecutionChatMessage
                                task={userScheduledTaskData.task}
                                executionTime={userScheduledTaskData.executionTime}
                            />
                        ) : (
                            <UserMessage
                                className={chatStyles.userBubble}
                                message={{ className: chatStyles.userBubbleMessage }}
                                key={message.id}
                            >
                                <ReactMarkdownComponent key={message.id} content={message.text || ''} variant="chat" isUserMessage />
                            </UserMessage>
                        )}
                    </div>
                );
            });
    }
};

export default memo(ChatMessage);
