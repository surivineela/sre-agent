import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { memo, useCallback, useMemo, useState } from 'react';
import { AgentTaskMetaData } from '../../Common/Contracts/DataPlane/AgentTask';
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
import { useChatBox } from '../Hooks/useChatBox';
import { useThreadAgentMode } from '../Hooks/useThreadAgentMode';
import { getChatBoxV2Styles } from '../Styles/Activities.styles';
import AgentTask from './AgentTask/AgentTask';
import AgentTaskDev from './AgentTaskDev';
import AzureSREWelcome from './AzureSREWelcome';
import { ChatSuggestions } from './ChatSuggestions';
import { Resizable, ResizableChildProps } from './Resizable';

export const ChatBox = ({
    addThread,
    updateThreadLastReadTime,
    threadId,
    threadSource,
    stylesProps,
    collapseResizables,
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

    const showAgentTaskDev = useConfigSetting(SettingNames.ShowAgentTaskDev);

    const threadAgentModeData = useThreadAgentMode(threadId, threadSource);

    const isWelcomeThread = threadSource === ThreadSource.welcomeMessage;

    const { scrollable } = useScrollableComponentStyles();

    const [isAgentTaskCollapsed, setIsAgentTaskCollapsed] = useState<boolean>(true);
    const [task, setTask] = useState<AgentTaskMetaData | null>(null);

    const chatBoxStyles = useMemo(() => getChatBoxV2Styles(!isAgentTaskCollapsed, stylesProps), [isAgentTaskCollapsed, stylesProps]);

    const openAgentTask = useCallback(
        (task: AgentTaskMetaData | null) => {
            if (isAgentTaskCollapsed) {
                setIsAgentTaskCollapsed(false);
                collapseResizables?.();
            }
            if (task) {
                setTask(task);
            }
        },
        [collapseResizables, isAgentTaskCollapsed]
    );

    return (
        <ChatBoxContext.Provider value={{ getGroupedChatMessages }}>
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
                                openAgentTask={openAgentTask}
                            />
                        </CopilotProvider>
                    </div>

                    <Resizable
                        position="right"
                        initialWidth="65%"
                        minWidthPixels={500}
                        collapsedWidthPixels={isAgentTaskCollapsed ? 0 : 500}
                        collapsed={isAgentTaskCollapsed}
                        setCollapsed={setIsAgentTaskCollapsed}
                        style={{ height: 'calc(100vh - 100px)', width: '100%' }}
                    >
                        {(resizableChildProps: ResizableChildProps) => (
                            <AgentTask
                                threadId={threadId}
                                task={task}
                                userDefinedThreadId={userDefinedThreadIdRef.current}
                                {...resizableChildProps}
                            />
                        )}
                    </Resizable>
                    {showAgentTaskDev && <AgentTaskDev collapseResizables={collapseResizables} />}
                </div>
            </ThreadAgentModeContext.Provider>
        </ChatBoxContext.Provider>
    );
};

export default memo(ChatBox);
