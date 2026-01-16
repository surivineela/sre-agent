import { CopilotChat } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { forwardRef, memo, useEffect, useMemo, useRef } from 'react';
import { ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import ChatBoxDeepInvestigationDialog from '../Components/Chat/ChatBoxDeepInvestigationDialog';
import ChatBoxSidePanel, { IChatBoxSidePanelProps } from '../Components/Chat/ChatBoxSidePanel';
import ChatMessageGroupComponent from '../Components/Chat/ChatMessageGroupComponent';
import ChatMessageGroups from '../Components/Chat/ChatMessageGroups';
import { OverviewChatBox } from '../Components/Chat/OverviewChatBox';
import ChatBoxFooter from '../Components/ChatBoxFooter';
import ChatLoading from '../Components/ChatLoading';
import KnowledgeGraphSidePanel from '../Components/KnowledgeGraphSidePanel';
import MemorySidePanel from '../Components/MemorySidePanel';
import PermissionErrorChatMessage from '../Components/PermissionErrorChatMessage';
import { AgentTaskGraphHandle, ChatBoxHandleRef, ChatBoxSidePanelType, IChatBoxProps } from '../Contracts/Activities';
import { ChatBoxSidePanelContext, ThreadAgentModeContext } from '../Contracts/Context';
import { useChatBox } from '../Hooks/useChatBox';
import { useChatBoxSidePanel } from '../Hooks/useChatBoxSidePanel';
import { useThreadAgentMode } from '../Hooks/useThreadAgentMode';
import { getChatBoxStyles } from '../Styles/Activities.styles';
import AgentTask from './AgentTask/AgentTask';
import AzureSREWelcome from './AzureSREWelcome';
import { ChatSuggestions } from './ChatSuggestions';
import TodoPlan from './TodoPlan/TodoPlan';

export const ChatBox = forwardRef<ChatBoxHandleRef, IChatBoxProps>((props, ref) => {
    const {
        addThread,
        selectThread,
        updateThreadLastReadTime,
        threadId,
        threadSource,
        stylesProps,
        sidePanelStylesProps,
        initialSidePanelData,
        canOpenSidePanel,
        onOpenSidePanel,
        onCloseSidePanel,
        expandOrCollapseNavBar,
        setHasToDoPlans,
        forcedAgentName,
        lockAgentSelection,
        onTelemetryUpdate,
        renderEmptyState,
        inputDisabledMessage,
        initialRetroModeEnabled,
        isOverview,
    } = props;

    const {
        messageGroups,
        isAgentTyping,
        streamingMessageGroup,
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
        threadIdUsedForCreatingNewThread,
        onScroll,
        downButtonState,
        onClickDownButton,
        updateApprovalOrCliMessageInStreamingMessage,
        submitUserQuestionResponse,
        isDeepInvestigationButtonEnabled,
        isDeepInvestigationTurnedOn,
        isDeepInvestigationDialogVisible,
        setIsDeepInvestigationDialogVisible,
        onClickDeepInvestigationDialogActionButton,
        onClickDeepInvestigationButton,
        isIncidentRetroModeTurnedOn,
        hasPendingUserQuestion,
        toggleIncidentRetroMode,
    } = useChatBox(addThread, updateThreadLastReadTime, threadId, threadSource, initialRetroModeEnabled);

    const {
        sidePanelProps: { isSidePanelOpen, selectedSidePanelType, sidePanelWidth, setSidePanelWidth },
        agentTaskProps: { openAgentTask, closeAgentTask, ...restAgentTaskProps },
        todoPlanProps: { openTodoPlan, closeTodoPlan, ...restTodoPlanProps },
        memorySearchResultProps: { openMemorySearchResult, closeMemorySearchResult, memorySearchResult },
        knowledgeGraphSearchResultProps: { openKnowledgeGraphSearchResult, closeKnowledgeGraphSearchResult, knowledgeGraphSearchResult },
    } = useChatBoxSidePanel(
        threadId,
        threadIdUsedForCreatingNewThread,
        initialSidePanelData,
        isLoading,
        canOpenSidePanel,
        expandOrCollapseNavBar,
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

        const combined = messageGroups.flatMap(group => [...group.userMessages, ...group.agentMessages]);

        if (streamingMessageGroup) {
            combined.push(...streamingMessageGroup.userMessages, ...streamingMessageGroup.agentMessages);
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
    }, [messageGroups, onTelemetryUpdate, streamingMessageGroup]);

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

    return (
        <ChatBoxSidePanelContext.Provider value={{ openAgentTask, openTodoPlan, openMemorySearchResult, openKnowledgeGraphSearchResult }}>
            <ThreadAgentModeContext.Provider value={{ ...threadAgentModeData }}>
                <div style={stylesProps?.rootStyle || { minHeight: '0px', flex: '1' }}>
                    <div className={chatBoxStyles.chatBoxAndAgentTask}>
                        <div className={chatBoxStyles.chatBox}>
                            <div className={chatBoxStyles.chatBoxInner}>
                                {isOverview ? (
                                    <OverviewChatBox
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
                                        inputDisabledMessage={inputDisabledMessage}
                                        isIncidentRetroModeTurnedOn={isIncidentRetroModeTurnedOn}
                                        toggleIncidentRetroMode={toggleIncidentRetroMode}
                                        hasPendingUserQuestion={hasPendingUserQuestion}
                                    />
                                ) : (
                                    <>
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
                                                {isWelcomeThread && (
                                                    <AzureSREWelcome threadId={currentThreadId} selectThread={selectThread} />
                                                )}

                                                {/* Display permission error message if any*/}
                                                <PermissionErrorChatMessage key={'permission-error-chat-message'} isLoading={isLoading} />

                                                {/* Non streaming messages */}
                                                {!isLoading && (
                                                    <>
                                                        <ChatMessageGroups
                                                            messageGroups={messageGroups}
                                                            threadId={currentThreadId || ''}
                                                            sendMessage={sendMessage}
                                                            onSubmitUserQuestionResponse={submitUserQuestionResponse}
                                                        />
                                                        {streamingMessageGroup && (
                                                            <ChatMessageGroupComponent
                                                                key={streamingMessageGroup.id}
                                                                messageGroup={streamingMessageGroup}
                                                                isStreamingMessage={true}
                                                                isTyping={isAgentTyping}
                                                                threadId={currentThreadId || ''}
                                                                threadSource={threadSource}
                                                                toolCallText={toolCallText}
                                                                isWaitingForStreamingMessages={isWaitingForStreamingMessages}
                                                                updateApprovalOrCliMessageInStreamingMessage={
                                                                    updateApprovalOrCliMessageInStreamingMessage
                                                                }
                                                                onSubmitUserQuestionResponse={submitUserQuestionResponse}
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
                                            inputDisabledMessage={inputDisabledMessage}
                                            isIncidentRetroModeTurnedOn={isIncidentRetroModeTurnedOn}
                                            toggleIncidentRetroMode={toggleIncidentRetroMode}
                                            hasPendingUserQuestion={hasPendingUserQuestion}
                                        />
                                    </>
                                )}
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
                            {selectedSidePanelType === ChatBoxSidePanelType.KnowledgeGraphSearchResult && (
                                <KnowledgeGraphSidePanel
                                    knowledgeGraphResult={knowledgeGraphSearchResult}
                                    onClose={closeKnowledgeGraphSearchResult}
                                />
                            )}
                        </ChatBoxSidePanel>
                    </div>
                    <ChatBoxDeepInvestigationDialog
                        isOpen={isDeepInvestigationDialogVisible}
                        setIsOpen={setIsDeepInvestigationDialogVisible}
                        onClickDeepInvestigationDialogActionButton={onClickDeepInvestigationDialogActionButton}
                    />
                </div>
            </ThreadAgentModeContext.Provider>
        </ChatBoxSidePanelContext.Provider>
    );
});

export default memo(ChatBox);
