import { createIntl, createIntlCache } from 'react-intl';
import { SREAgentUserId } from '../../Common/Contracts/Azure/SreAgent';
import { AgentTaskStreamingData } from '../../Common/Contracts/DataPlane/AgentTask';
import { GrepSearchResult } from '../../Common/Contracts/DataPlane/GrepResult';
import { McpToolExecution } from '../../Common/Contracts/DataPlane/McpToolExecution';
import {
    Approval,
    ApprovalDecision,
    AzCliExecution,
    KnowledgeGraphSearchResult,
    KubectlExecution,
    MemorySearchResult,
    Message,
    MessageAuthor,
    MessageType,
    PsqlExecution,
} from '../../Common/Contracts/DataPlane/Message';
import { ReadFileResult } from '../../Common/Contracts/DataPlane/ReadFileResult';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { TerminalExecutionResult } from '../../Common/Contracts/DataPlane/TerminalResult';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { TodoInfo } from '../../Common/Contracts/DataPlane/TodoPlan';
import { UserQuestion } from '../../Common/Contracts/DataPlane/UserQuestion';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { Guid } from '../../Common/Helpers/Guid';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { GenericErrorResources } from '../../Strings/SREAgentResources';
import { ChatMessage, ChatMessageGroup, Reasoning, ThreadListsState, ThreadListState } from '../Contracts/Activities';

/**
 * Use this when your thread list does not have favorite section or any addition section.
 * Add additional threads to the existing threads list and update existing threads if they have been modified.
 * @param prevThreads existing threads sorted in descending order by modifiedTimestamp
 * @param threads additional threads sorted in descending order by modifiedTimestamp
 * @param reverse
 * @returns
 */
export const processThreads = (prevThreads: Thread[], threads: Thread[], areThreadsNew: boolean) => {
    if (threads.length === 0) {
        return {
            threads: prevThreads,
            addedThreads: [],
        };
    }

    const threadIdsToRemoveFromPrevThreads: Set<string> = new Set<string>();

    const threadsMap: Map<string, Thread> = new Map();
    threads.forEach(thread => threadsMap.set(thread.id, thread));

    for (let i = 0; i < prevThreads.length; i++) {
        const prevThreadId = prevThreads[i].id;
        const duplicatedThread = threadsMap.get(prevThreadId);
        if (duplicatedThread) {
            if (areThreadsNew && duplicatedThread.modifiedTimestamp > prevThreads[i].modifiedTimestamp) {
                // if the threads are new and the modified time is greter than the existing duplicated one from prev threads, then remove it from the prev threads
                threadIdsToRemoveFromPrevThreads.add(prevThreadId);
            } else {
                // Remove thread out of the threadsMap because the thread is already in the existing threads and has not been modified
                threadsMap.delete(prevThreadId);
            }
        }
    }

    const threadsToAdd: Thread[] = Array.from(threadsMap.values());
    threadsToAdd.sort((a, b) => getSafeDateTime(b.modifiedTimestamp).getTime() - getSafeDateTime(a.modifiedTimestamp).getTime());

    const updatedExistingThreads = [...prevThreads].filter(thread => {
        return !threadIdsToRemoveFromPrevThreads.has(thread.id);
    });

    const existingThreads = threadIdsToRemoveFromPrevThreads.size > 0 ? updatedExistingThreads : prevThreads;

    if (threadsToAdd.length === 0) {
        return {
            threads: existingThreads,
            addedThreads: [],
        };
    }

    return {
        threads: areThreadsNew ? [...threadsToAdd, ...existingThreads] : [...existingThreads, ...threadsToAdd],
        addedThreads: threadsToAdd,
    };
};

/**
 * Use this when your component has multiple thread list section interacting among each other.
 *
 * Add old threads returned from threads pagination to bottom of the list.
 * @param prevThreads existing threads sorted in descending order by modifiedTimestamp
 * @param prevThreadsThatHaveFavoritePropertyChanged threads that have favorite property changed sorted in descending order by modifiedTimestamp
 */
export const addOldThreads = (
    prevThreadListState: ThreadListState,
    oldThreads: Thread[],
    moreThreadsToLoad: boolean
): {
    threadListState: ThreadListState;
    addedThreads: Thread[];
} => {
    const getThreadListStateWithNoOldThreadsToAdd = () => {
        return {
            threadListState:
                moreThreadsToLoad !== prevThreadListState.moreThreadsToLoad
                    ? {
                          ...prevThreadListState,
                          moreThreadsToLoad,
                      }
                    : prevThreadListState,
            addedThreads: [],
        };
    };

    if (oldThreads.length === 0) {
        return getThreadListStateWithNoOldThreadsToAdd();
    }

    const { threads: prevThreads, threadsThatHaveFavoritePropertyChanged: prevThreadsThatHaveFavoritePropertyChanged } =
        prevThreadListState;

    // Remove thread from old threads if it is duplicated in prevThreads
    const uniqueOldThreads = (
        prevThreads.length > 0
            ? [
                  ...oldThreads.filter(thread => {
                      return !prevThreads.find(prevThread => prevThread.id === thread.id);
                  }),
              ]
            : [...oldThreads]
    ).sort((a, b) => getSafeDateTime(b.modifiedTimestamp).getTime() - getSafeDateTime(a.modifiedTimestamp).getTime());

    // Remove thread from prevThreadsThatHaveFavoritePropertyChanged if it is duplicated in old threads because those threads
    // now can be properly added to the thread list
    const updatedThreadsThatHaveFavoritePropertyChanged =
        uniqueOldThreads.length > 0
            ? prevThreadsThatHaveFavoritePropertyChanged.filter(thread => {
                  return !uniqueOldThreads.find(oldThread => oldThread.id === thread.id);
              })
            : prevThreadsThatHaveFavoritePropertyChanged;

    if (uniqueOldThreads.length === 0) {
        return getThreadListStateWithNoOldThreadsToAdd();
    }

    return {
        threadListState: {
            threads: [...prevThreads, ...uniqueOldThreads],
            threadsThatHaveFavoritePropertyChanged: updatedThreadsThatHaveFavoritePropertyChanged,
            moreThreadsToLoad,
        },
        addedThreads: uniqueOldThreads,
    };
};

const getIndexToInsertThread = (threads: Thread[], threadToInsert: Thread): number => {
    let start = 0,
        end = threads.length - 1;
    const threadToInsertTimestamp = getSafeDateTime(threadToInsert.modifiedTimestamp).getTime();

    while (start <= end) {
        const mid = start + Math.floor((end - start) / 2);
        const midTimestamp = getSafeDateTime(threads[mid].modifiedTimestamp).getTime();
        if (midTimestamp < threadToInsertTimestamp) {
            end = mid - 1;
        } else if (midTimestamp > threadToInsertTimestamp) {
            start = mid + 1;
        } else {
            return mid;
        }
    }
    return start;
};

export const insertThreadToThreadList = (threads: Thread[], thread: Thread): Thread[] => {
    const indexToInsert = getIndexToInsertThread(threads, thread);
    const updatedThreads = [...threads];
    updatedThreads.splice(indexToInsert, 0, thread);
    return updatedThreads;
};

/**
 * Use this when your component has multiple thread list section interacting among each other.
 * When being notified that a new thread is added or an existing thread's modified time is updated, add it to the temporary storage while the initial loading is not finished
 * @param prevThreadListState
 * @param thread
 */
export const addThreadToThreadsThatHaveModifiedTimestampUpdated = (
    threadListsState: ThreadListsState,
    thread: Thread
): {
    threadListsState: ThreadListsState;
    addedThreads: Thread[];
} => {
    const threadsThatHaveModifiedTimestampUpdated = [...threadListsState.threadsThatHaveModifiedTimestampUpdated];

    // Check if this thread is duplicated.
    for (let i = 0; i < threadsThatHaveModifiedTimestampUpdated.length; i++) {
        if (threadsThatHaveModifiedTimestampUpdated[i].id === thread.id) {
            // If the thread is duplicated and the one is being added is newer than the existing one, replace the existing one
            if (threadsThatHaveModifiedTimestampUpdated[i].modifiedTimestamp < thread.modifiedTimestamp) {
                const updatedThreadsThatHaveModifiedTimestampUpdated = [...threadsThatHaveModifiedTimestampUpdated];
                updatedThreadsThatHaveModifiedTimestampUpdated.splice(i, 1);
                updatedThreadsThatHaveModifiedTimestampUpdated.unshift(thread);
                return {
                    threadListsState: {
                        ...threadListsState,
                        threadsThatHaveModifiedTimestampUpdated: updatedThreadsThatHaveModifiedTimestampUpdated,
                    },
                    addedThreads: [thread],
                };
                // If the thread is duplicated and the one is being added is older than or same as the existing one, do nothing
            } else {
                return {
                    threadListsState,
                    addedThreads: [],
                };
            }
        }
    }

    // If no duplication, add the thread to the temporary list
    return {
        threadListsState: {
            ...threadListsState,
            threadsThatHaveModifiedTimestampUpdated: [thread, ...threadsThatHaveModifiedTimestampUpdated],
        },
        addedThreads: [thread],
    };
};

export const removeThreadFromThreadListsState = (threadListsState: ThreadListsState, threadId: string): ThreadListsState => {
    return {
        ...threadListsState,
        favoriteThreadListState: {
            ...threadListsState.favoriteThreadListState,
            threads: threadListsState.favoriteThreadListState.threads.filter(t => t.id !== threadId),
            threadsThatHaveFavoritePropertyChanged: threadListsState.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged.filter(
                t => t.id !== threadId
            ),
        },
        regularThreadListState: {
            ...threadListsState.regularThreadListState,
            threads: threadListsState.regularThreadListState.threads.filter(t => t.id !== threadId),
            threadsThatHaveFavoritePropertyChanged: threadListsState.regularThreadListState.threadsThatHaveFavoritePropertyChanged.filter(
                t => t.id !== threadId
            ),
        },
        threadsThatHaveModifiedTimestampUpdated: threadListsState.threadsThatHaveModifiedTimestampUpdated.filter(t => t.id !== threadId),
    };
};

/**
 * Use this when your component has multiple thread list section interacting among each other.
 *
 * Insert the thread that has favorite property changed to the existing threads list or the threadsThatHaveFavoritePropertyChanged list based on its modified timestamp.
 * @param prevThreadListState
 * @param thread
 * @returns
 */
export const insertThreadThatHasFavoritePropertyChangedToThreadListState = (
    prevThreadListState: ThreadListState,
    thread: Thread
): ThreadListState => {
    const {
        threads: prevThreads,
        threadsThatHaveFavoritePropertyChanged: prevThreadsThatHaveFavoritePropertyChanged,
        moreThreadsToLoad,
    } = prevThreadListState;

    // If the thread is already in the existing threads list, do nothing
    if (prevThreads.some(t => t.id === thread.id)) {
        return prevThreadListState;
    } else {
        // Find the index to insert the thread in the existing threads list to maintain the descending order by modifiedTimestamp
        const indexToInsertInPrevThreads = getIndexToInsertThread(prevThreads, thread);
        // Do not insert it to the bottom of the thread list if there are more old threads to load, otherwise it will break the pagination based on the oldest existing thread's modified timestamp. In this
        // case, insert it to the threadsThatHaveFavoritePropertyChanged list instead.
        if (indexToInsertInPrevThreads < prevThreads.length || !moreThreadsToLoad) {
            const updatedThreads = [...prevThreads];
            updatedThreads.splice(indexToInsertInPrevThreads, 0, thread);

            // Do a filter in case the prevThreadsThatHaveFavoritePropertyChanged already has the thread
            const updatedThreadsThatHaveFavoritePropertyChanged = prevThreadsThatHaveFavoritePropertyChanged.filter(
                t => t.id !== thread.id
            );
            return {
                ...prevThreadListState,
                threads: updatedThreads,
                threadsThatHaveFavoritePropertyChanged: updatedThreadsThatHaveFavoritePropertyChanged,
            };
        } else if (!prevThreadsThatHaveFavoritePropertyChanged.some(t => t.id === thread.id)) {
            return {
                ...prevThreadListState,
                threads: prevThreads,
                threadsThatHaveFavoritePropertyChanged: insertThreadToThreadList(prevThreadsThatHaveFavoritePropertyChanged, thread),
            };
        } else {
            return prevThreadListState;
        }
    }
};

/**
 * Use this when your component has multiple thread list section interacting among each other.
 * @param prevThreads
 * @param prevThreadsThatHaveFavoritePropertyChanged
 * @param thread
 * @returns
 */
export const insertThreadThatHasFavoritePropertyChangedToThreadListsState = (
    prevThreadListsState: ThreadListsState,
    thread: Thread
): ThreadListsState => {
    const fromFavoriteToRegular = !thread.favorite;

    return {
        ...prevThreadListsState,
        favoriteThreadListState: fromFavoriteToRegular
            ? prevThreadListsState.favoriteThreadListState
            : insertThreadThatHasFavoritePropertyChangedToThreadListState(prevThreadListsState.favoriteThreadListState, thread),
        regularThreadListState: fromFavoriteToRegular
            ? insertThreadThatHasFavoritePropertyChangedToThreadListState(prevThreadListsState.regularThreadListState, thread)
            : prevThreadListsState.regularThreadListState,
    };
};

/**
 * Use this when your component has multiple thread list section interacting among each other.
 * Push all threads that have favorite property changed to the existing threads list and maintain the descending order by modifiedTimestamp, when there are no more threads to load
 * @param prevThreads
 * @param prevThreadsThatHaveFavoritePropertyChanged
 * @returns
 */
export const pushAllThreadsThatHaveFavoritePropertyChangedToThreadLists = (prevThreadsList: ThreadListsState): ThreadListsState => {
    const { favoriteThreadListState, regularThreadListState } = prevThreadsList;

    if (
        favoriteThreadListState.threadsThatHaveFavoritePropertyChanged.length === 0 &&
        regularThreadListState.threadsThatHaveFavoritePropertyChanged.length === 0
    )
        return prevThreadsList;

    return {
        ...prevThreadsList,
        favoriteThreadListState: {
            ...favoriteThreadListState,
            threads: [...favoriteThreadListState.threads, ...favoriteThreadListState.threadsThatHaveFavoritePropertyChanged],
            threadsThatHaveFavoritePropertyChanged: [],
        },
        regularThreadListState: {
            ...regularThreadListState,
            threads: [...regularThreadListState.threads, ...regularThreadListState.threadsThatHaveFavoritePropertyChanged],
            threadsThatHaveFavoritePropertyChanged: [],
        },
    };
};

/**
 * Use this when your component has multiple thread list section interacting among each other.
 * Remove threadsThatHaveModifiedTimestampUpdated and push to the top of the existing threads list when initial loading is completed.
 * @param prevThreads
 * @param prevThreadsThatHaveFavoritePropertyChanged
 * @param newThreads
 * @returns
 */
export const pushAllThreadsThatHaveModifiedTimestampUpdatedToThreadLists = (
    prevThreadListsState: ThreadListsState,
    newThreads: Thread[]
): {
    threadListsState: ThreadListsState;
    addedThreads: Thread[];
} => {
    const { favoriteThreadListState, regularThreadListState } = prevThreadListsState;

    if (newThreads.length === 0) {
        return {
            threadListsState: prevThreadListsState,
            addedThreads: [],
        };
    }

    const threadIdsToRemoveFromPrevFavoriteThreads: Set<string> = new Set<string>();
    const threadIdsToRemoveFromPrevFavoriteThreadsThatHaveFavoritePropertyChanged: Set<string> = new Set<string>();
    const threadIdsToRemoveFromPrevRegularThreads: Set<string> = new Set<string>();
    const threadIdsToRemoveFromPrevRegularThreadsThatHaveFavoritePropertyChanged: Set<string> = new Set<string>();

    const newFavoriteThreadsMap: Map<string, Thread> = new Map();
    const newRegularThreadsMap: Map<string, Thread> = new Map();
    newThreads.forEach(thread => {
        // Manually set the thread's favorite property in case the existing property is outdated.
        if (
            favoriteThreadListState.threads.some(t => t.id === thread.id) ||
            favoriteThreadListState.threadsThatHaveFavoritePropertyChanged.some(t => t.id === thread.id)
        ) {
            newFavoriteThreadsMap.set(thread.id, { ...thread, favorite: true });
        } else {
            newRegularThreadsMap.set(thread.id, { ...thread, favorite: false });
        }
    });

    const processThreads = (oldThreads: Thread[], oldThreadIdsToRemove: Set<string>, newThreadsMap: Map<string, Thread>) => {
        if (newThreadsMap.size === 0) return;

        for (let i = 0; i < oldThreads.length; i++) {
            const oldThreadId = oldThreads[i].id;
            const duplicatedThread = newThreadsMap.get(oldThreadId);
            if (duplicatedThread) {
                if (duplicatedThread.modifiedTimestamp > oldThreads[i].modifiedTimestamp) {
                    // If the threads are new and the modified time is greter than the existing duplicated one from the old threads, then remove it from the old threads
                    oldThreadIdsToRemove.add(oldThreadId);
                } else {
                    // Remove thread out of the threadsMap because the thread is already in the existing threads and has not been modified
                    newThreadsMap.delete(oldThreadId);
                }
            }
        }
    };

    processThreads(favoriteThreadListState.threads, threadIdsToRemoveFromPrevFavoriteThreads, newFavoriteThreadsMap);
    processThreads(
        favoriteThreadListState.threadsThatHaveFavoritePropertyChanged,
        threadIdsToRemoveFromPrevFavoriteThreadsThatHaveFavoritePropertyChanged,
        newFavoriteThreadsMap
    );
    processThreads(regularThreadListState.threads, threadIdsToRemoveFromPrevRegularThreads, newRegularThreadsMap);
    processThreads(
        regularThreadListState.threadsThatHaveFavoritePropertyChanged,
        threadIdsToRemoveFromPrevRegularThreadsThatHaveFavoritePropertyChanged,
        newRegularThreadsMap
    );

    const favoriteThreadsToAdd: Thread[] = Array.from(newFavoriteThreadsMap.values());
    favoriteThreadsToAdd.sort((a, b) => getSafeDateTime(b.modifiedTimestamp).getTime() - getSafeDateTime(a.modifiedTimestamp).getTime());

    const regularThreadsToAdd: Thread[] = Array.from(newRegularThreadsMap.values());
    regularThreadsToAdd.sort((a, b) => getSafeDateTime(b.modifiedTimestamp).getTime() - getSafeDateTime(a.modifiedTimestamp).getTime());

    const filteredFavoriteThreads = [...favoriteThreadListState.threads].filter(thread => {
        return !threadIdsToRemoveFromPrevFavoriteThreads.has(thread.id);
    });

    const filteredFavoriteThreadsThatHaveFavoritePropertyChanged = [
        ...favoriteThreadListState.threadsThatHaveFavoritePropertyChanged,
    ].filter(thread => {
        return !threadIdsToRemoveFromPrevFavoriteThreadsThatHaveFavoritePropertyChanged.has(thread.id);
    });

    const filteredRegularThreads = [...regularThreadListState.threads].filter(thread => {
        return !threadIdsToRemoveFromPrevRegularThreads.has(thread.id);
    });

    const filteredRegularThreadsThatHaveFavoritePropertyChanged = [...regularThreadListState.threadsThatHaveFavoritePropertyChanged].filter(
        thread => {
            return !threadIdsToRemoveFromPrevRegularThreadsThatHaveFavoritePropertyChanged.has(thread.id);
        }
    );

    if (favoriteThreadsToAdd.length === 0 && regularThreadsToAdd.length === 0) {
        return {
            threadListsState: prevThreadListsState,
            addedThreads: [],
        };
    }

    return {
        threadListsState: {
            ...prevThreadListsState,
            favoriteThreadListState: {
                ...favoriteThreadListState,
                threads: [...favoriteThreadsToAdd, ...filteredFavoriteThreads],
                threadsThatHaveFavoritePropertyChanged: filteredFavoriteThreadsThatHaveFavoritePropertyChanged,
            },
            regularThreadListState: {
                ...regularThreadListState,
                threads: [...regularThreadsToAdd, ...filteredRegularThreads],
                threadsThatHaveFavoritePropertyChanged: filteredRegularThreadsThatHaveFavoritePropertyChanged,
            },
        },
        addedThreads: [...favoriteThreadsToAdd, ...regularThreadsToAdd],
    };
};

export const getDefaultSREAgentAuthor = (): MessageAuthor => {
    return {
        role: 'SREAgent',
        userId: SREAgentUserId,
        displayName: '',
    };
};

export const isAgentMessage = (message: ChatMessage): boolean => {
    return equals(message.author.role, 'SREAgent', AntUxStringComparison.IgnoreCase);
};

export const getSpecialMessageContentFromStreamingMessage = <T,>(
    streamingMessage: StreamingMessage,
    streamingMessageType: MessageType
): T | undefined => {
    const additionalProperties = streamingMessage.additionalProperties;
    const messageType = additionalProperties?.streamMessageType || '';
    const text = getStreamingMessageText(streamingMessage);

    if (
        additionalProperties &&
        messageType &&
        streamingMessageType &&
        equals(messageType, streamingMessageType, AntUxStringComparison.IgnoreCase) &&
        text
    ) {
        try {
            return JSON.parse(text) as T;
        } catch (error) {
            return undefined;
        }
    }
};

export const createChatMessageFromStreamingMessage = (streamingMessage: StreamingMessage): ChatMessage => {
    const messageContent = streamingMessage.contents?.[0];

    const messageId = streamingMessage.additionalProperties?.messageId || '';

    const azCliExecution = getSpecialMessageContentFromStreamingMessage<AzCliExecution>(streamingMessage, 'azcli');
    const kubectlExecution = getSpecialMessageContentFromStreamingMessage<KubectlExecution>(streamingMessage, 'kubectl');
    const memorySearchResult = getSpecialMessageContentFromStreamingMessage<MemorySearchResult>(streamingMessage, 'memorysearch');
    const knowledgeGraphSearchResult = getSpecialMessageContentFromStreamingMessage<KnowledgeGraphSearchResult>(
        streamingMessage,
        'knowledgegraph'
    );
    const grepSearchResult = getSpecialMessageContentFromStreamingMessage<GrepSearchResult>(streamingMessage, 'grepsearch');
    const readFileResult = getSpecialMessageContentFromStreamingMessage<ReadFileResult>(streamingMessage, 'readfile');
    const terminalResult = getSpecialMessageContentFromStreamingMessage<TerminalExecutionResult>(streamingMessage, 'terminal');
    const todoInfo = getSpecialMessageContentFromStreamingMessage<TodoInfo>(streamingMessage, 'todoplan');
    const psqlExecution = getSpecialMessageContentFromStreamingMessage<PsqlExecution>(streamingMessage, 'psql');
    const userQuestion = getSpecialMessageContentFromStreamingMessage<UserQuestion>(streamingMessage, 'userquestion');
    const mcpToolExecution = getSpecialMessageContentFromStreamingMessage<McpToolExecution>(streamingMessage, 'mcptool');

    let approval = getSpecialMessageContentFromStreamingMessage<Approval>(streamingMessage, 'approval');
    const agentTaskData = getSpecialMessageContentFromStreamingMessage<AgentTaskStreamingData>(streamingMessage, 'deepinvestigation');
    // Agent task sometimes contains approval data as well
    approval = approval || agentTaskData?.approval || undefined;
    if (approval) {
        approval = {
            ...approval,
            status: processApprovalStreamingMessageStatus(approval.status),
        };
    }

    const isReasoning = isReasoningStreamingMessageType(streamingMessage);
    const reasoning: Reasoning | undefined = isReasoning
        ? {
              active: true,
              items: [
                  {
                      messageId,
                      content: messageContent?.text || '',
                  },
              ],
          }
        : undefined;

    const text =
        messageContent?.text &&
        !approval &&
        !azCliExecution &&
        !kubectlExecution &&
        !memorySearchResult &&
        !knowledgeGraphSearchResult &&
        !grepSearchResult &&
        !readFileResult &&
        !terminalResult &&
        !psqlExecution &&
        !todoInfo &&
        !agentTaskData &&
        !userQuestion &&
        !mcpToolExecution &&
        !isReasoning
            ? messageContent.text
            : '';

    const isImage = isImageStreamingMessageType(streamingMessage);

    const chatMessageContent: ChatMessage = {
        id: messageId,
        text,
        author: getDefaultSREAgentAuthor(),
        title: undefined,
        timeStamp: streamingMessage.createdAt || '',
        isImage,
        approval,
        azCliExecution,
        kubectlExecution,
        psqlExecution,
        memorySearchResult,
        knowledgeGraphSearchResult,
        grepSearchResult,
        readFileResult,
        terminalResult,
        userQuestion,
        mcpToolExecution,
        agentTaskInfo: agentTaskData?.agentTaskInfo,
        todoInfo,
        changeDiff: undefined,
        isDailyReport: false,
        isComplete: true,
        isImageContent: isImage,
        messageType: streamingMessage.additionalProperties?.streamMessageType || null,
        reasoning: reasoning,
    };

    return chatMessageContent;
};

export const isDefaultStreamingMessageType = (streamingMessage: StreamingMessage): boolean => {
    const streamMessageType = streamingMessage.additionalProperties?.streamMessageType;
    // Default message type excludes special types like images, charts, and trajectory insights
    return !streamMessageType;
};

export const isReasoningStreamingMessageType = (streamingMessage: StreamingMessage): boolean => {
    const streamingMessageType = streamingMessage.additionalProperties?.streamMessageType || '';

    return equals(streamingMessageType, 'reasoning', AntUxStringComparison.IgnoreCase);
};

export const isReasoningMessage = (message: Message): boolean => {
    return equals(message.messageType || '', 'reasoning', AntUxStringComparison.IgnoreCase);
};

export const isReasoningChatMessage = (chatMessage: ChatMessage): boolean => {
    return isReasoningMessage(chatMessage) || !!chatMessage.reasoning;
};

export const isImageStreamingMessageType = (streamingMessage: StreamingMessage): boolean => {
    const streamingMessageType = streamingMessage.additionalProperties?.streamMessageType || '';
    return (
        equals(streamingMessageType, 'image', AntUxStringComparison.IgnoreCase) ||
        equals(streamingMessageType, 'chart', AntUxStringComparison.IgnoreCase) ||
        equals(streamingMessageType, 'mermaid', AntUxStringComparison.IgnoreCase)
    );
};

export const getStreamingMessageText = (streamingMessage: StreamingMessage) => {
    return streamingMessage.contents?.[0]?.text || '';
};

export const getToolCallText = (streamingMessage: StreamingMessage): string | null => {
    const messageContent = streamingMessage.contents?.[0];
    if (messageContent && equals(messageContent.$type || '', 'functionCall', AntUxStringComparison.IgnoreCase)) {
        return messageContent.additionalProperties?.userDescription || messageContent.additionalProperties?.functionCallDescription || null;
    }
    return null;
};

export const isFinalStreamingMessage = (streamingMessage: StreamingMessage): boolean => {
    const { finishReason, additionalProperties } = streamingMessage;

    return (
        equals(finishReason || '', 'stop', AntUxStringComparison.IgnoreCase) ||
        equals(finishReason || '', 'length', AntUxStringComparison.IgnoreCase) ||
        !!additionalProperties?.isCancelled
    );
};

export const isUserStreamingMessage = (streamingMessage: StreamingMessage): boolean => {
    return equals(streamingMessage.role || '', 'user', AntUxStringComparison.IgnoreCase);
};

export const isInitialState = (status: string | null | undefined): boolean => {
    return (
        !!status &&
        (equals(status, 'Pending', AntUxStringComparison.IgnoreCase) ||
            equals(status, 'PendingAuthorization', AntUxStringComparison.IgnoreCase) ||
            equals(status, 'Running', AntUxStringComparison.IgnoreCase))
    );
};

export const isUpdatedCliOrApprovalStreamingMessage = (streamingMessage: StreamingMessage): boolean => {
    const approval = getSpecialMessageContentFromStreamingMessage<Approval>(streamingMessage, 'approval');
    const azCliExecution = getSpecialMessageContentFromStreamingMessage<AzCliExecution>(streamingMessage, 'azcli');
    const kubectlExecution = getSpecialMessageContentFromStreamingMessage<AzCliExecution>(streamingMessage, 'kubectl');
    const psqlExecution = getSpecialMessageContentFromStreamingMessage<AzCliExecution>(streamingMessage, 'psql');
    const userQuestion = getSpecialMessageContentFromStreamingMessage<UserQuestion>(streamingMessage, 'userquestion');
    const mcpToolExecution = getSpecialMessageContentFromStreamingMessage<McpToolExecution>(streamingMessage, 'mcptool');
    const status = approval?.status || azCliExecution?.status || kubectlExecution?.status || psqlExecution?.status;

    // UserQuestion updates have status 'Answered' - detect as update when not Pending
    const isUserQuestionUpdate = !!userQuestion && userQuestion.status !== 'Pending';

    // McpToolExecution updates have status 'Completed', 'Failed', or 'Cancelled' - detect as update when not Running
    const isMcpToolExecutionUpdate = !!mcpToolExecution && mcpToolExecution.status !== 'Running';

    return (
        ((!!approval || !!azCliExecution || !!psqlExecution || !!kubectlExecution) && !isInitialState(status)) ||
        isUserQuestionUpdate ||
        isMcpToolExecutionUpdate
    );
};

export const processApprovalStreamingMessageStatus = (status: ApprovalDecision | number): ApprovalDecision => {
    if (status !== null && status !== undefined && typeof status === 'number') {
        return status === 0
            ? ApprovalDecision.Pending
            : status === 1
              ? ApprovalDecision.Approved
              : status === 2
                ? ApprovalDecision.Cancelled
                : status === 3
                  ? ApprovalDecision.PendingAuthorization
                  : ApprovalDecision.Authorized;
    }

    return status;
};

export const composeUserMessage = (userId: string, userDisplayName: string, message: string): ChatMessage => {
    const id = Guid.newGuid();

    return {
        id,
        timeStamp: new Date().toISOString(),
        author: {
            role: 'User',
            userId: userId,
            displayName: userDisplayName,
        },
        text: message,
        title: undefined,
        approval: undefined,
        azCliExecution: undefined,
        kubectlExecution: undefined,
        psqlExecution: undefined,
        memorySearchResult: undefined,
        todoInfo: undefined,
        isDailyReport: false,
        changeDiff: undefined,
        agentTaskInfo: undefined,
        isComplete: true,
        isImageContent: undefined,
        messageType: null,
        isImage: undefined,
        deepInvestigationStatus: undefined,
        reasoning: undefined,
        knowledgeGraphSearchResult: undefined,
    };
};

export const composeDefaultAgentMessage = (): ChatMessage => {
    return {
        id: Guid.newGuid(),
        timeStamp: new Date().toISOString(),
        author: getDefaultSREAgentAuthor(),
        text: '',
        title: undefined,
        approval: undefined,
        azCliExecution: undefined,
        kubectlExecution: undefined,
        psqlExecution: undefined,
        memorySearchResult: undefined,
        todoInfo: undefined,
        isDailyReport: false,
        changeDiff: undefined,
        agentTaskInfo: undefined,
        isComplete: true,
        isImageContent: undefined,
        messageType: null,
        isImage: undefined,
        deepInvestigationStatus: undefined,
        reasoning: undefined,
        knowledgeGraphSearchResult: undefined,
    };
};

export const isChatMessageContentNonImageText = (chatMessage: ChatMessage): boolean => {
    return (
        !chatMessage.approval &&
        !chatMessage.azCliExecution &&
        !chatMessage.kubectlExecution &&
        !chatMessage.psqlExecution &&
        !chatMessage.memorySearchResult &&
        !chatMessage.isImage &&
        !chatMessage.isImageContent &&
        !chatMessage.todoInfo &&
        !chatMessage.deepInvestigationStatus &&
        !chatMessage.changeDiff &&
        !chatMessage.agentTaskInfo &&
        !chatMessage.reasoning
    );
};

export const convertMessageToChatMessage = (message: Message): ChatMessage => {
    return {
        ...message,
        isImage: message.isImageContent,
        text: isReasoningMessage(message) ? '' : message.text,
        reasoning: isReasoningMessage(message)
            ? {
                  active: false,
                  items: [
                      {
                          messageId: message.id,
                          content: message.text,
                      },
                  ],
              }
            : undefined,
    };
};

export const getOldestMessageInChatMessageGroup = (messageGroup: ChatMessageGroup) => {
    return messageGroup.userMessages.length > 0 ? messageGroup.userMessages[0] : messageGroup.agentMessages[0];
};

export const doesChatMessageBelongToChatMessageGroup = (message: ChatMessage, messageGroup: ChatMessageGroup) => {
    if (
        message &&
        !message.deepInvestigationStatus &&
        !messageGroup.agentMessages[0]?.deepInvestigationStatus &&
        (messageGroup.userMessages.length > 0 || messageGroup.agentMessages.length > 0)
    ) {
        const oldestMessageInMessageGroup = getOldestMessageInChatMessageGroup(messageGroup);
        const isMessageAgentMessage = isAgentMessage(message);
        const isOldestMessageInMessageGroupAgentMessage = isAgentMessage(oldestMessageInMessageGroup);

        if (isMessageAgentMessage && isOldestMessageInMessageGroupAgentMessage) {
            return true;
        } else if (isMessageAgentMessage && !isOldestMessageInMessageGroupAgentMessage) {
            return false;
        } else if (!isMessageAgentMessage && isOldestMessageInMessageGroupAgentMessage) {
            return true;
        } else {
            return (
                message.author.userId === oldestMessageInMessageGroup.author.userId &&
                Math.abs(getSafeDateTime(message.timeStamp).getTime() - getSafeDateTime(oldestMessageInMessageGroup.timeStamp).getTime()) <=
                    5 * 60 * 1000
            );
        }
    }

    return false;
};

/**
 *
 * @param messages messges in descending order based on timestamp
 * @param messageGroups message groups in ascending order based on the timestamp
 * @returns
 */
export const updateChatMessageGroups = (messages: ChatMessage[], messageGroups: ChatMessageGroup[]): ChatMessageGroup[] => {
    if (messages.length === 0) return messageGroups;

    const updatedMessageGroups = [...messageGroups];

    for (let i = 0; i < messages.length; i++) {
        const message = messages[i];
        const isMessageAgentMessage = isAgentMessage(message);

        const newMessageGroup: ChatMessageGroup = {
            id: Guid.newGuid(),
            userMessages: isMessageAgentMessage ? [] : [message],
            agentMessages: isMessageAgentMessage ? [message] : [],
        };

        if (updatedMessageGroups.length === 0) {
            updatedMessageGroups.push(newMessageGroup);
        } else {
            const oldestMessageGroup = updatedMessageGroups[0];
            if (doesChatMessageBelongToChatMessageGroup(message, oldestMessageGroup)) {
                const agentMessages = [...oldestMessageGroup.agentMessages];
                const userMessages = [...oldestMessageGroup.userMessages];

                if (isMessageAgentMessage) {
                    const oldestMessageInMessageGroup = getOldestMessageInChatMessageGroup(oldestMessageGroup);
                    const isOldestMessageInMessageGroupAgentMessage = isAgentMessage(oldestMessageInMessageGroup);

                    if (
                        isMessageAgentMessage &&
                        isOldestMessageInMessageGroupAgentMessage &&
                        isReasoningChatMessage(message) &&
                        isReasoningChatMessage(oldestMessageInMessageGroup)
                    ) {
                        agentMessages[0] = {
                            ...message,
                            reasoning: {
                                active: false,
                                items: [...(message.reasoning?.items || []), ...(oldestMessageInMessageGroup.reasoning?.items || [])],
                            },
                        };
                    } else {
                        agentMessages.unshift(message);
                    }
                } else {
                    userMessages.unshift(message);
                }
                updatedMessageGroups.splice(0, 1, {
                    id: oldestMessageGroup.id,
                    userMessages: [...userMessages],
                    agentMessages: [...agentMessages],
                });
            } else {
                updatedMessageGroups.unshift(newMessageGroup);
            }
        }
    }
    return [...updatedMessageGroups];
};

export const getDefaultDeepInvestigationStatusChatMessageGroup = (isDeepInvestigationTurnedOn: boolean): ChatMessageGroup => {
    const agentMessage: ChatMessage = {
        id: Guid.newGuid(),
        timeStamp: new Date().toISOString(),
        author: getDefaultSREAgentAuthor(),
        text: '',
        title: undefined,
        approval: undefined,
        azCliExecution: undefined,
        kubectlExecution: undefined,
        psqlExecution: undefined,
        memorySearchResult: undefined,
        todoInfo: undefined,
        isDailyReport: false,
        changeDiff: undefined,
        agentTaskInfo: undefined,
        isComplete: true,
        isImageContent: undefined,
        messageType: null,
        isImage: undefined,
        deepInvestigationStatus: {
            enabled: isDeepInvestigationTurnedOn,
        },
        reasoning: undefined,
        knowledgeGraphSearchResult: undefined,
    };

    return {
        id: Guid.newGuid(),
        userMessages: [],
        agentMessages: [agentMessage],
    };
};

export const getFilteredThreads = (
    threads: Thread[],
    includedSources: ThreadSource[] | undefined,
    excludedSources: ThreadSource[] | undefined,
    unreadOnly: boolean | undefined,
    searchText: string | undefined
): Thread[] => {
    return threads.filter(thread => {
        let match = true;

        if (thread.source && includedSources && includedSources.length > 0) {
            match = includedSources.includes(thread.source);
        }

        if (thread.source && excludedSources && excludedSources.length > 0) {
            match = !excludedSources.includes(thread.source);
        }

        if (searchText) {
            match = thread.title.toLocaleLowerCase().includes(searchText.toLocaleLowerCase());
        }

        if (unreadOnly) {
            match = match && isThreadUnread(thread);
        }

        return match;
    });
};

/**
 * @param exponentialBackoffDepth
 * @returns The time in millseconds to wait before issuing a next request. The max interval is 15 minutes.
 */
export const getIntervalBetweenLoading = (exponentialBackoffDepth: number) => {
    const base = 100;
    const maxInterval = 15 * 60 * 1000; // 15 minutes in milliseconds
    return Math.min(base + Math.floor(Math.pow(2, exponentialBackoffDepth)) * 1000, maxInterval);
};

export const isThreadUnread = (thread: Thread): boolean => {
    if (thread.lastReadTime && thread.modifiedTimestamp) {
        return getSafeDateTime(thread.lastReadTime).getTime() < getSafeDateTime(thread.modifiedTimestamp).getTime();
    }

    // If lastReadTime is null, return true if welcome message and false if not (so we don't suddenly mark all old threads as unread)
    return thread.source === ThreadSource.welcomeMessage;
};

export const getUpdatedUnreadThreadIds = (unreadThreadsIds: Set<string>, addedThreads: Thread[]): Set<string> => {
    const updatedUnreadThreadIds = new Set(unreadThreadsIds);
    let isUnreadThreadsUpdated = false;

    for (const thread of addedThreads) {
        if (isThreadUnread(thread)) {
            updatedUnreadThreadIds.add(thread.id);
            isUnreadThreadsUpdated = true;
        }
    }

    if (isUnreadThreadsUpdated) {
        return updatedUnreadThreadIds;
    }

    return unreadThreadsIds;
};

export const removeThreadIdsFromUnreadThreads = (unreadThreadsIds: Set<string>, threadIdToRemove: string): Set<string> => {
    const updatedUnreadThreadIds = new Set(unreadThreadsIds);

    if (updatedUnreadThreadIds.has(threadIdToRemove)) {
        updatedUnreadThreadIds.delete(threadIdToRemove);
        return updatedUnreadThreadIds;
    }

    return unreadThreadsIds;
};

export const isAgentMessagesEmpty = (messageGroup?: ChatMessageGroup | null): boolean => {
    return (
        !messageGroup ||
        messageGroup.agentMessages.length === 0 ||
        !messageGroup.agentMessages.some(agentMessage => {
            return (
                !!agentMessage.text ||
                !!agentMessage.approval ||
                !!agentMessage.azCliExecution ||
                !!agentMessage.kubectlExecution ||
                !!agentMessage.psqlExecution ||
                !!agentMessage.memorySearchResult ||
                !!agentMessage.todoInfo ||
                !!agentMessage.agentTaskInfo ||
                !!agentMessage.changeDiff
            );
        })
    );
};

export const isChatMessageAMatchedApprovalOrCliMessage = (content: ChatMessage, specialMessageId: string | undefined): boolean => {
    return (
        !!specialMessageId &&
        (content.approval?.id === specialMessageId ||
            content.azCliExecution?.id === specialMessageId ||
            content.kubectlExecution?.id === specialMessageId ||
            content.psqlExecution?.id === specialMessageId)
    );
};

export const isChatMessageAMatchedAgentTaskMessage = (content: ChatMessage, specialMessageId: string | undefined): boolean => {
    return !!specialMessageId && content.agentTaskInfo?.id === specialMessageId;
};

export const parseThreadFromStreamingText = (text: string) => {
    const thread = JSON.parse(text) as Thread;
    if (thread && thread.id && thread.startMessage && thread.title && thread.lastMessage && thread.modifiedTimestamp) {
        return thread;
    } else {
        // This utility is not a React component; create a lightweight intl instance for localization.
        const cache = createIntlCache();
        const intl = createIntl({ locale: 'en', messages: {} }, cache);
        throw new Error(intl.formatMessage(GenericErrorResources.invalidThreadDataFromStream));
    }
};
