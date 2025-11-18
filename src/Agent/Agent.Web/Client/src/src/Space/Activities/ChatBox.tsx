import { CopilotChat, CopilotProvider, CopilotTheme } from '@fluentui-copilot/react-copilot';
import { mergeClasses, webDarkTheme, webLightTheme } from '@fluentui/react-components';
import { useTheme } from '@fluentui/react/lib/Theme';
import { forwardRef, memo, useEffect, useMemo, useRef } from 'react';
import { ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import ChatBoxDeepInvestigationDialog from '../Components/Chat/ChatBoxDeepInvestigationDialog';
import ChatBoxSidePanel, { IChatBoxSidePanelProps } from '../Components/Chat/ChatBoxSidePanel';
import ChatBoxFooter from '../Components/ChatBoxFooter';
import ChatLoading from '../Components/ChatLoading';
import ChatMessage from '../Components/ChatMessage';
import ChatMessages from '../Components/ChatMessages';
import MemorySidePanel from '../Components/MemorySidePanel';
import PermissionErrorChatMessage from '../Components/PermissionErrorChatMessage';
import { AgentTaskGraphHandle, ChatBoxHandleRef, ChatBoxSidePanelType, IChatBoxProps } from '../Contracts/Activities';
import { ChatBoxContext, ChatBoxSidePanelContext, ThreadAgentModeContext } from '../Contracts/Context';
import { useChatBox } from '../Hooks/useChatBox';
import { useChatBoxSidePanel } from '../Hooks/useChatBoxSidePanel';
import { useThreadAgentMode } from '../Hooks/useThreadAgentMode';
import { getChatBoxStyles, ThreadTitleHeight } from '../Styles/Activities.styles';
import AgentTask from './AgentTask/AgentTask';
import AzureSREWelcome from './AzureSREWelcome';
import { ChatSuggestions } from './ChatSuggestions';
import TodoPlan from './TodoPlan/TodoPlan';

export const ChatBox = forwardRef<ChatBoxHandleRef, IChatBoxProps>((props, ref) => {
    const {
        addThread,
        updateThreadLastReadTime,
        threadId,
        threadSource,
        stylesProps,
        sidePanelStylesProps,
        initialSidePanelData,
        canOpenSidePanel,
        onOpenSidePanel,
        onCloseSidePanel,
        setMenuCollapsed,
        setHasToDoPlans,
        forcedAgentName,
        lockAgentSelection,
        onTelemetryUpdate,
        renderEmptyState,
    } = props;

    const theme = useTheme();

    const {
        messages,
        isAgentTyping,
        temporaryUserMessage,
        streamingMessage,
        toolCallText,
        isCancellingStreaming,
        isWaitingForStreamingMessages,
        isLoading,
        sendMessage,
        isNewAndCleanThread,
        messagesDivRef,
        intersectionObserverRef,
        currentThreadId,
        cancelStreaming,
        prompts,
        messagePromptsUsed,
        onScroll,
        downButtonState,
        onClickDownButton,
        getGroupedChatMessages,
        updateApprovalOrCliMessageInStreamingMessage,
        userDefinedThreadIdRef,

        isDeepInvestigationButtonEnabled,
        isDeepInvestigationTurnedOn,
        isDeepInvestigationDialogVisible,
        setIsDeepInvestigationDialogVisible,
        onClickDeepInvestigationDialogActionButton,
        onClickDeepInvestigationButton,
    } = useChatBox(addThread, updateThreadLastReadTime, threadId, threadSource);

    const {
        sidePanelProps: { isSidePanelOpen, selectedSidePanelType, sidePanelWidth, setSidePanelWidth },
        agentTaskProps: { openAgentTask, closeAgentTask, ...restAgentTaskProps },
        todoPlanProps: { openTodoPlan, closeTodoPlan, ...restTodoPlanProps },
        memorySearchResultProps: { openMemorySearchResult, closeMemorySearchResult, memorySearchResult },
    } = useChatBoxSidePanel(
        threadId,
        userDefinedThreadIdRef.current,
        initialSidePanelData,
        isLoading,
        canOpenSidePanel,
        setMenuCollapsed,
        onOpenSidePanel,
        onCloseSidePanel,
        setHasToDoPlans,
        ref
    );

    const threadAgentModeData = useThreadAgentMode(threadId, threadSource);

    const isWelcomeThread = threadSource === ThreadSource.welcomeMessage;

    const { scrollable } = useScrollableComponentStyles();

    const agentTaskGraphRef = useRef<AgentTaskGraphHandle | null>(null);

    const chatBoxStyles = useMemo(() => getChatBoxStyles(isSidePanelOpen, stylesProps), [isSidePanelOpen, stylesProps]);

    useEffect(() => {
        if (!onTelemetryUpdate) {
            return;
        }

        const combined = [...messages];

        if (temporaryUserMessage) {
            combined.push(temporaryUserMessage as any);
        }

        if (streamingMessage) {
            combined.push(streamingMessage as any);
        }

        const snapshot = combined
            .filter(message => !!message)
            .slice(-12)
            .map(message => {
                const hasContentError = Array.isArray((message as any).contents)
                    ? (message as any).contents.some((content: any) => !!content?.error)
                    : false;

                // Extract text from contents array if available, otherwise fall back to message.text
                let messageText = '';
                if (Array.isArray((message as any).contents) && (message as any).contents.length > 0) {
                    // Concatenate text from all content items
                    messageText = (message as any).contents
                        .map((content: any) => content?.text ?? '')
                        .filter((text: string) => text.length > 0)
                        .join(' ');
                } else if ((message as any).text) {
                    messageText = (message as any).text;
                }

                return {
                    id: message.id,
                    authorRole: message.author?.role ?? 'User',
                    text: messageText,
                    timeStamp: message.timeStamp,
                    hasError: Boolean((message as any).error || hasContentError),
                };
            });

        onTelemetryUpdate({ messages: snapshot });
    }, [messages, onTelemetryUpdate, streamingMessage, temporaryUserMessage]);

    const chatBoxSidePanelProps: IChatBoxSidePanelProps = {
        open: isSidePanelOpen,
        stylesForClassNames: sidePanelStylesProps,
        inlineDrawerStyles: {
            minWidth:
                selectedSidePanelType === ChatBoxSidePanelType.AgentTask
                    ? '50%'
                    : selectedSidePanelType === ChatBoxSidePanelType.ToDoPlan
                      ? '480px'
                      : '450px',
        },
        defaultSidePanelWidth:
            selectedSidePanelType === ChatBoxSidePanelType.AgentTask
                ? undefined
                : selectedSidePanelType === ChatBoxSidePanelType.ToDoPlan
                  ? '520px'
                  : '40%',
        onResize: selectedSidePanelType === ChatBoxSidePanelType.AgentTask ? () => agentTaskGraphRef.current?.centerGraph() : undefined,
        sidePanelWidth,
        setSidePanelWidth,
    };

    // Use style instead of classname to override the CopilotProvider styule to avoid global styles conflict
    return (
        <CopilotProvider
            {...CopilotTheme}
            mode={'canvas'}
            theme={theme.isInverted ? webDarkTheme : webLightTheme}
            style={stylesProps?.rootStyle || { height: `calc(100% - ${ThreadTitleHeight + 5}px)` }}
        >
            <ChatBoxContext.Provider value={{ getGroupedChatMessages }}>
                <ChatBoxSidePanelContext.Provider value={{ openAgentTask, openTodoPlan, openMemorySearchResult }}>
                    <ThreadAgentModeContext.Provider value={{ ...threadAgentModeData }}>
                        <div className={chatBoxStyles.chatBoxAndAgentTask}>
                            <div className={chatBoxStyles.chatBox}>
                                <div className={chatBoxStyles.chatBoxInner}>
                                    <div
                                        className={mergeClasses(scrollable, chatBoxStyles.chatContainer)}
                                        ref={messagesDivRef}
                                        onScroll={onScroll}
                                    >
                                        <CopilotChat className={chatBoxStyles.chat}>
                                            <div ref={intersectionObserverRef} />

                                            {isLoading && !isWelcomeThread && <ChatLoading />}

                                            {isNewAndCleanThread &&
                                                !isWelcomeThread &&
                                                (renderEmptyState ? (
                                                    renderEmptyState({ sendMessage, forcedAgentName })
                                                ) : (
                                                    <ChatSuggestions sendMessage={sendMessage} />
                                                ))}

                                            {/* Insert the richer welcome experience once at the top for welcome threads */}
                                            {isWelcomeThread && <AzureSREWelcome threadId={currentThreadId} addThread={addThread} />}

                                            {/* Display permission error message if any*/}
                                            <PermissionErrorChatMessage key={'permission-error-chat-message'} isLoading={isLoading} />

                                            {/* Non streaming messages */}
                                            {!isLoading && (
                                                <>
                                                    <ChatMessages
                                                        messages={messages}
                                                        threadId={currentThreadId || ''}
                                                        nextMessageAfterTheLastMessage={
                                                            temporaryUserMessage || streamingMessage || undefined
                                                        }
                                                    />
                                                    {temporaryUserMessage && (
                                                        <ChatMessage
                                                            key={temporaryUserMessage.id}
                                                            message={temporaryUserMessage}
                                                            threadId={currentThreadId || ''}
                                                            threadSource={threadSource}
                                                            previousMessage={messages[messages.length - 1]}
                                                        />
                                                    )}
                                                    {streamingMessage && (
                                                        <ChatMessage
                                                            key={streamingMessage.id}
                                                            message={streamingMessage}
                                                            isStreamingMessage={true}
                                                            isTyping={isAgentTyping}
                                                            threadId={currentThreadId || ''}
                                                            threadSource={threadSource}
                                                            toolCallText={toolCallText}
                                                            isWaitingForStreamingMessages={isWaitingForStreamingMessages}
                                                            updateApprovalOrCliMessageInStreamingMessage={
                                                                updateApprovalOrCliMessageInStreamingMessage
                                                            }
                                                            previousMessage={temporaryUserMessage || messages[messages.length - 1]}
                                                        />
                                                    )}
                                                </>
                                            )}
                                        </CopilotChat>
                                    </div>

                                    <ChatBoxFooter
                                        sendMessage={sendMessage}
                                        isLoading={isLoading}
                                        downButtonState={downButtonState}
                                        onClickDownButton={onClickDownButton}
                                        prompts={prompts}
                                        messagePromptsUsed={messagePromptsUsed}
                                        cancelStreaming={cancelStreaming}
                                        isTyping={!!isAgentTyping}
                                        isCancellingStreaming={isCancellingStreaming}
                                        threadId={currentThreadId}
                                        threadSource={threadSource}
                                        isDeepInvestigationButtonEnabled={isDeepInvestigationButtonEnabled}
                                        isDeepInvestigationTurnedOn={isDeepInvestigationTurnedOn}
                                        onClickDeepInvestigationButton={onClickDeepInvestigationButton}
                                        forcedAgentName={forcedAgentName}
                                        lockAgentSelection={lockAgentSelection}
                                    />
                                </div>
                            </div>

                            <ChatBoxSidePanel {...chatBoxSidePanelProps}>
                                {selectedSidePanelType === ChatBoxSidePanelType.AgentTask && (
                                    <AgentTask {...restAgentTaskProps} closeAgentTask={closeAgentTask} ref={agentTaskGraphRef} />
                                )}
                                {selectedSidePanelType === ChatBoxSidePanelType.ToDoPlan && (
                                    <TodoPlan {...restTodoPlanProps} closeTodoPlan={closeTodoPlan} />
                                )}
                                {selectedSidePanelType === ChatBoxSidePanelType.MemorySearchResult && (
                                    <MemorySidePanel memoryResult={memorySearchResult} onClose={closeMemorySearchResult} />
                                )}
                            </ChatBoxSidePanel>
                        </div>
                        <ChatBoxDeepInvestigationDialog
                            isOpen={isDeepInvestigationDialogVisible}
                            setIsOpen={setIsDeepInvestigationDialogVisible}
                            onClickDeepInvestigationDialogActionButton={onClickDeepInvestigationDialogActionButton}
                        />
                    </ThreadAgentModeContext.Provider>
                </ChatBoxSidePanelContext.Provider>
            </ChatBoxContext.Provider>
        </CopilotProvider>
    );
});

export default memo(ChatBox);
