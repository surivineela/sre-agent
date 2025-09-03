import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { memo, useMemo } from 'react';
import { ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import ChatBoxFooter from '../Components/ChatBoxFooter';
import ChatLoading from '../Components/ChatLoading';
import ChatMessage from '../Components/ChatMessage';
import ChatMessages from '../Components/ChatMessages';
import PermissionErrorChatMessage from '../Components/PermissionErrorChatMessage';
import { IChatBoxProps } from '../Contracts/Activities';
import { ChatBoxContext, ThreadAgentModeContext } from '../Contracts/Context';
import { useAgentTask } from '../Hooks/useAgentTask';
import { useChatBox } from '../Hooks/useChatBox';
import { useThreadAgentMode } from '../Hooks/useThreadAgentMode';
import { getChatBoxV2Styles } from '../Styles/Activities.styles';
import AgentTask from './AgentTask/AgentTask';
import AzureSREWelcome from './AzureSREWelcome';
import { ChatSuggestions } from './ChatSuggestions';

export const ChatBox = ({
    addThread,
    updateThreadLastReadTime,
    threadId,
    threadSource,
    stylesProps,
    agentTaskStyleProps,
    collapseResizables,
    isAgentTaskEnabled,
}: IChatBoxProps) => {
    const {
        chatHistory,
        newMessages,
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
        updateSpecialMessageInStreamingMessage,
        userDefinedThreadIdRef,
    } = useChatBox(addThread, updateThreadLastReadTime, threadId, threadSource);

    const {
        isAgentTaskCollapsed,
        setIsAgentTaskCollapsed,
        isDeepInvestigationButtonEnabled,
        isDeepInvestigationTurnedOn,
        onClickDeepInvestigationButton,
        openAgentTask,
        ...rest
    } = useAgentTask(threadId, userDefinedThreadIdRef.current, collapseResizables, isLoading);

    const showAgentTask = useConfigSetting(SettingNames.ShowAgentTask);

    const threadAgentModeData = useThreadAgentMode(threadId, threadSource);

    const isWelcomeThread = threadSource === ThreadSource.welcomeMessage;

    const { scrollable } = useScrollableComponentStyles();

    const chatBoxStyles = useMemo(() => getChatBoxV2Styles(!isAgentTaskCollapsed, stylesProps), [isAgentTaskCollapsed, stylesProps]);

    return (
        <ChatBoxContext.Provider value={{ getGroupedChatMessages, openAgentTask }}>
            <ThreadAgentModeContext.Provider value={{ ...threadAgentModeData }}>
                <div className={chatBoxStyles.chatBoxAndAgentTask}>
                    <div className={chatBoxStyles.chatBox}>
                        <CopilotProvider mode="canvas" className={chatBoxStyles.chatBoxInner}>
                            <div className={mergeClasses(scrollable, chatBoxStyles.chatContainer)} ref={messagesDivRef} onScroll={onScroll}>
                                <CopilotChat className={chatBoxStyles.chat}>
                                    <div ref={intersectionObserverRef} />

                                    {isLoading && !isWelcomeThread && <ChatLoading />}

                                    {isNewAndCleanThread && !isWelcomeThread && <ChatSuggestions sendMessage={sendMessage} />}

                                    {/* Insert the richer welcome experience once at the top for welcome threads */}
                                    {isWelcomeThread && <AzureSREWelcome threadId={currentThreadId} addThread={addThread} />}

                                    {/* Display permission error message if any*/}
                                    <PermissionErrorChatMessage key={'permission-error-chat-message'} isLoading={isLoading} />

                                    {/* Chat history */}
                                    {!isLoading &&
                                        chatHistory?.map((messages, index) => {
                                            return (
                                                <ChatMessages
                                                    // set key to chathistory.length - index to ensure the existing page always has the same key to prevent re-rendering
                                                    key={chatHistory.length - index}
                                                    messages={messages}
                                                    threadId={currentThreadId || ''}
                                                    prevMessageBeforeTheFirstMessage={
                                                        chatHistory?.[index - 1]?.[chatHistory?.[index - 1]?.length - 1]
                                                    }
                                                    nextMessageAfterTheLastMessage={
                                                        chatHistory?.[index + 1]?.[0] ||
                                                        newMessages[0] ||
                                                        temporaryUserMessage ||
                                                        streamingMessage
                                                    }
                                                />
                                            );
                                        })}

                                    {/* New messages */}
                                    {!isLoading && (
                                        <ChatMessages
                                            messages={newMessages}
                                            threadId={currentThreadId || ''}
                                            prevMessageBeforeTheFirstMessage={
                                                chatHistory?.[chatHistory.length - 1]?.[chatHistory?.[chatHistory.length - 1]?.length - 1]
                                            }
                                            nextMessageAfterTheLastMessage={temporaryUserMessage || streamingMessage || undefined}
                                        />
                                    )}

                                    {temporaryUserMessage && (
                                        <ChatMessage
                                            key={temporaryUserMessage.id}
                                            message={temporaryUserMessage}
                                            threadId={currentThreadId || ''}
                                            previousMessage={
                                                newMessages[newMessages.length - 1] ||
                                                chatHistory?.[chatHistory.length - 1]?.[chatHistory?.[chatHistory.length - 1]?.length - 1]
                                            }
                                        />
                                    )}

                                    {!isLoading && streamingMessage && (
                                        <ChatMessage
                                            key={streamingMessage.id}
                                            message={streamingMessage}
                                            isStreamingMessage={true}
                                            isTyping={isAgentTyping}
                                            threadId={currentThreadId || ''}
                                            toolCallText={toolCallText}
                                            isWaitingForStreamingMessages={isWaitingForStreamingMessages}
                                            updateSpecialMessageInStreamingMessage={updateSpecialMessageInStreamingMessage}
                                            previousMessage={
                                                temporaryUserMessage ||
                                                newMessages[newMessages.length - 1] ||
                                                chatHistory?.[chatHistory.length - 1]?.[chatHistory?.[chatHistory.length - 1]?.length - 1]
                                            }
                                        />
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
                                showDeepInvestigationButton={showAgentTask && isAgentTaskEnabled}
                                isDeepInvestigationButtonEnabled={isDeepInvestigationButtonEnabled}
                                isDeepInvestigationTurnedOn={isDeepInvestigationTurnedOn}
                                onClickDeepInvestigationButton={onClickDeepInvestigationButton}
                            />
                        </CopilotProvider>
                    </div>
                    {showAgentTask && isAgentTaskEnabled && (
                        <AgentTask
                            {...rest}
                            collapsed={isAgentTaskCollapsed}
                            setCollapsed={setIsAgentTaskCollapsed}
                            stylesProps={agentTaskStyleProps}
                        />
                    )}
                </div>
            </ThreadAgentModeContext.Provider>
        </ChatBoxContext.Provider>
    );
};

export default memo(ChatBox);
