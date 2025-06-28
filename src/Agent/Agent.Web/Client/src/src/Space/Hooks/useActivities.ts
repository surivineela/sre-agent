import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { Thread, ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../Common/Helpers/Guid';
import { ActivitiesThreadHeaderResources } from '../../Strings/SREAgentResources';
import { SelectedTimes } from '../Activities/TimeDropdown';
import { getUTCTimestampBasedOnSelectedThreadCutoffTime, isThreadUnread } from '../Activities/Utility';
import { ThreadListHandle } from '../Contracts/Activities';

export const useActivities = () => {
    const intl = useIntl();
    const { threadId: initialThreadId } = useParams();
    const navigate = useNavigate();
    const location = useLocation();

    const [selectedThread, setSelectedThread] = useState<Thread | null>(null);
    const [threadContentAndActionKey, setThreadContentAndActionKey] = useState<string>(Guid.newGuid());
    const [activeThreadId, setActiveThreadId] = useState<string>('');
    const [threadPollingTriggerId, setThreadPollingTriggerId] = useState<number>(0);

    const untouched = useRef<boolean>(true);
    const threadListHandleRef = useRef<ThreadListHandle>(null);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const proxy = useContext(AzPortalContext);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const pollNewThreadsImmediately = () => setThreadPollingTriggerId(prev => prev + 1);

    const promoteThread = useCallback((threadId: string) => {
        // poll thread immediately to make the recently updated the thread on top.
        threadListHandleRef.current?.promoteThread(threadId, pollNewThreadsImmediately);
    }, []);

    const selectThread = useCallback(
        (thread: Thread | null) => {
            untouched.current = false;
            setSelectedThread(thread);
            setThreadContentAndActionKey(Guid.newGuid());
            setActiveThreadId(thread?.id || '');
            navigate({
                ...location,
                pathname: thread?.id ? `/views/activities/threads/${thread.id}` : '/views/activities/',
            });
        },
        [navigate, location]
    );

    const addThread = useCallback(
        (threadId: string, newThreadToSelect?: Thread) => {
            untouched.current = false;
            // poll thread immediately to get the thread just added.
            pollNewThreadsImmediately();

            if (newThreadToSelect) {
                selectThread(newThreadToSelect);
            } else {
                setActiveThreadId(threadId);
                navigate({ ...location, pathname: `/views/activities/threads/${threadId}` });
            }
        },
        [navigate, location, selectThread]
    );

    const updateThread = useCallback(
        (updatedThread: Thread) => {
            // Update the selected thread if it matches the updated thread
            if (selectedThread?.id === updatedThread.id) {
                setSelectedThread(updatedThread);
            }
        },
        [selectedThread]
    );

    const deleteThread = useCallback(
        async (thread: Thread) => {
            const id = proxy.startNotification(
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadTitle, { title: thread.title }),
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadInProgressDescription)
            );

            try {
                await threadClient.deleteThread(thread.id);
                threadListHandleRef.current?.removeThreadFromList(thread);
                selectThread(null);

                proxy.log({
                    action: 'deleteThread',
                    actionModifier: 'success',
                    logLevel: 'info',
                    resourceId: thread.id,
                });

                proxy.stopNotification(id, true, intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadSuccessDescription));
            } catch (e: any) {
                proxy.log({
                    action: 'deleteThread',
                    actionModifier: 'failure',
                    logLevel: 'error',
                    resourceId: thread.id,
                    data: {
                        error: e?.message || e?.response?.data,
                    },
                });

                proxy.stopNotification(
                    id,
                    false,
                    intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadFailureDescription, {
                        errorMessage: e?.message || e?.response?.data,
                    })
                );
            }
        },
        [intl, selectThread, proxy, threadClient]
    );

    const updateThreadLastReadTime = useCallback((threadId: string) => {
        threadListHandleRef.current?.updateThreadLastReadTime(threadId);
    }, []);

    useEffect(() => {
        // Only regenerate the key when the thread ID changes, not when thread properties update
        setThreadContentAndActionKey(Guid.newGuid());
    }, [selectedThread?.id]);

    useEffect(() => {
        let isSubscribed = true;

        if (initialThreadId && !activeThreadId && untouched.current) {
            const setInitialThread = async () => {
                const threadResponse = await threadClient.getThread(initialThreadId);
                if (isSubscribed && threadResponse.isSuccessful && threadResponse.content) {
                    selectThread(threadResponse.content);
                }
            };

            setInitialThread();
        } else if (!initialThreadId && !activeThreadId && untouched.current) {
            // Check for welcome thread if no specific thread ID is provided
            const checkForWelcomeThread = async () => {
                const timestampCutoff = getUTCTimestampBasedOnSelectedThreadCutoffTime(SelectedTimes.OneDay);

                const threadsResponse = await threadClient.getThreads({
                    skip: 0,
                    top: 1,
                    descending: true,
                    filters: {
                        source: ThreadSource.welcomeMessage,
                        timestamps: {
                            min: {
                                timestamp: timestampCutoff,
                                inclusive: true,
                            },
                        },
                    },
                });

                if (isSubscribed && threadsResponse.isSuccessful && threadsResponse.content && threadsResponse.content.length > 0) {
                    const welcomeThread = threadsResponse.content[0];

                    if (isThreadUnread(welcomeThread)) {
                        selectThread(welcomeThread);
                    }
                }
            };

            checkForWelcomeThread();
        }

        return () => {
            isSubscribed = false;
        };
    }, [initialThreadId, activeThreadId, selectThread, threadClient]);

    return {
        selectedThread,
        addThread,
        promoteThread,
        deleteThread,
        selectThread,
        updateThreadLastReadTime,
        threadContentAndActionKey,
        activeThreadId,
        threadPollingTriggerId,
        threadListHandleRef,
        updateThread,
    };
};
