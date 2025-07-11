import { CopilotChat, CopilotProvider } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { ActivitiesResources } from '../../Strings/SREAgentResources';
import ChatBoxFooter from '../Components/ChatBoxFooter';
import ChatLoading from '../Components/ChatLoading';
import ChatMessage from '../Components/ChatMessage';
import { IChatBoxProps } from '../Contracts/Activities';
import { useChatBox } from '../Hooks/useChatBox';
import { ChatBoxStyles } from '../Styles/Activities.styles';
import AzureSREWelcome from './AzureSREWelcome';
import { ChatSuggestions } from './ChatSuggestions';
import { getGroupedMessages } from './Utility';
import { useThreadAgentMode } from '../Hooks/useThreadAgentMode';
import { ThreadAgentModeContext } from '../Contracts/Context';

export const ChatBox = ({ addThread, promoteThread, updateThreadLastReadTime, threadId, threadSource }: IChatBoxProps) => {
    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
    const intl = useIntl();

    const {
        messages,
        temporaryUserMessage,
        agentTypingMessage,
        isLoadingInitialChatHistory,
        sendMessage,
        disableInput,
        isNewAndCleanThread,
        messagesDivRef,
        intersectionObserverRef,
        currentThreadId,
        cancelResponse,
        prompts,
        messagePromptsUsed,
        onScroll,
        showNewMessageButton,
        onClickNewMessageButton,
    } = useChatBox(addThread, promoteThread, updateThreadLastReadTime, threadId);

    const threadAgentModeData = useThreadAgentMode(threadId, threadSource);

    const isWelcomeThread = threadSource === ThreadSource.welcomeMessage;

    const { scrollable } = useScrollableComponentStyles();

    const insufficientPermissionsMessage = useMemo(() => {
        if (hasChatPermissions) {
            return null;
        }

        return {
            id: 'insufficient-permissions',
            text: intl.formatMessage(ActivitiesResources.insufficientChatPermissions),
            author: {
                role: 'SREAgent' as const,
                displayName: 'Azure SRE Agent',
                userId: 'system',
            },
            timeStamp: new Date().toISOString(),
        };
    }, [hasChatPermissions, intl]);

    const displayMessages = useMemo(() => {
        if (insufficientPermissionsMessage) {
            return [...messages, insufficientPermissionsMessage];
        }
        return messages;
    }, [messages, insufficientPermissionsMessage]);

    return (
        <ThreadAgentModeContext.Provider value={{ ...threadAgentModeData }}>
            <div className={ChatBoxStyles.chatBox}>
                <CopilotProvider mode="canvas" className={ChatBoxStyles.chatBoxInner}>
                    <div className={mergeClasses(scrollable, ChatBoxStyles.chatContainer)} ref={messagesDivRef} onScroll={onScroll}>
                        <CopilotChat className={ChatBoxStyles.chat}>
                            <div ref={intersectionObserverRef} />

                            {isLoadingInitialChatHistory && !isWelcomeThread && <ChatLoading />}

                            {isNewAndCleanThread && !isWelcomeThread && <ChatSuggestions sendMessage={sendMessage} />}

                            {/* Insert the richer welcome experience once at the top for welcome threads */}
                            {isWelcomeThread && <AzureSREWelcome threadId={currentThreadId} addThread={addThread} />}

                            {/* Render the remaining chat history */}
                            {displayMessages.map((message, index) => (
                                <ChatMessage
                                    key={index}
                                    message={message}
                                    previousMessage={displayMessages[index - 1]}
                                    nextMessage={displayMessages[index + 1]}
                                    getGroupedMessages={() => getGroupedMessages(displayMessages, index)}
                                    threadId={currentThreadId || ''}
                                />
                            ))}

                            {hasChatPermissions && temporaryUserMessage && (
                                <ChatMessage
                                    message={temporaryUserMessage}
                                    previousMessage={displayMessages[displayMessages.length - 1]}
                                    threadId={currentThreadId || ''}
                                />
                            )}

                            {hasChatPermissions && agentTypingMessage && (
                                <ChatMessage
                                    message={agentTypingMessage}
                                    isTyping
                                    threadId={currentThreadId || ''}
                                    cancelResponse={cancelResponse}
                                />
                            )}
                        </CopilotChat>
                    </div>

                    <ChatBoxFooter
                        sendMessage={sendMessage}
                        disableInput={disableInput || !hasChatPermissions}
                        isNewMessageButtonVisible={showNewMessageButton && hasChatPermissions}
                        onClickNewMessageButton={onClickNewMessageButton}
                        prompts={prompts}
                        messagePromptsUsed={messagePromptsUsed}
                        threadId={currentThreadId}
                    />
                </CopilotProvider>
            </div>
        </ThreadAgentModeContext.Provider>
    );
};

export default memo(ChatBox);
