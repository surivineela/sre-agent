import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { Guid } from '../../Common/Helpers/Guid';
import { ActivitiesThreadHeaderResources } from '../../Strings/SREAgentResources';
import { isThreadUnread } from '../Activities/Utility';
import { ThreadMenuHandle } from '../Contracts/Activities';
import { SreAgentContext } from '../Contracts/Context';

export const useActivities = () => {
    const intl = useIntl();
    const { threadId: initialThreadId } = useParams();
    const navigate = useNavigate();
    const location = useLocation();

    const [selectedThread, setSelectedThread] = useState<Thread | null>(null);
    const [threadContentAndActionKey, setThreadContentAndActionKey] = useState<string>(Guid.newGuid());
    const [activeThreadId, setActiveThreadId] = useState<string>('');

    const untouched = useRef<boolean>(true);
    const threadMenuHandleRef = useRef<ThreadMenuHandle>(null);
    const activeThreadIdRef = useRef<string>(activeThreadId);
    activeThreadIdRef.current = activeThreadId;

    const threadTitleUpdateListeners = useRef<Set<(threadId: string, newTitle: string) => void>>(new Set());

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const proxy = useContext(AzPortalContext);
    const {
        activities: { setLastVisitedThreadId },
    } = useContext(SreAgentContext);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const selectThread = useCallback(
        (thread: Thread | null) => {
            untouched.current = false;
            setSelectedThread(thread);
            setThreadContentAndActionKey(Guid.newGuid());
            setActiveThreadId(thread?.id || '');
            setLastVisitedThreadId(thread?.id || '');
            navigate({
                ...location,
                pathname: thread?.id ? `/views/activities/threads/${thread.id}` : '/views/activities/',
            });
        },
        [navigate, location, setLastVisitedThreadId]
    );

    const addThread = useCallback(
        (threadId: string, newThreadToSelect?: Thread) => {
            untouched.current = false;

            if (newThreadToSelect) {
                selectThread(newThreadToSelect);
            } else {
                setLastVisitedThreadId(threadId);
                setActiveThreadId(threadId);
                navigate({ ...location, pathname: `/views/activities/threads/${threadId}` });
            }
        },
        [navigate, location, selectThread, setLastVisitedThreadId]
    );

    const deleteThread = useCallback(
        async (thread: Thread) => {
            const id = proxy.startNotification(
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadTitle, { title: thread.title }),
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadInProgressDescription)
            );

            try {
                await threadClient.deleteThread(thread.id);
                threadMenuHandleRef.current?.removeThreadFromList(thread.id);
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

    const notifyThreadTitleUpdate = useCallback((threadId: string, newTitle: string) => {
        threadTitleUpdateListeners.current.forEach(listener => listener(threadId, newTitle));
    }, []);

    const subscribeThreadTitleUpdate = useCallback((listener: (threadId: string, newTitle: string) => void) => {
        threadTitleUpdateListeners.current.add(listener);
        return () => {
            threadTitleUpdateListeners.current.delete(listener);
        };
    }, []);

    const updateThreadTitle = useCallback(
        async (threadId: string, newTitle: string) => {
            const id = proxy.startNotification(
                intl.formatMessage(ActivitiesThreadHeaderResources.renameThreadTitle),
                intl.formatMessage(ActivitiesThreadHeaderResources.renameThreadInProgressDescription, { title: newTitle })
            );

            const response = await threadClient.updateThreadTitle(threadId, newTitle);

            proxy.log({
                action: 'updateThreadTitle',
                actionModifier: 'success',
                logLevel: 'info',
                resourceId: threadId,
            });

            if (response.isSuccessful) {
                proxy.log({
                    action: 'updateThreadTitle',
                    actionModifier: 'success',
                    logLevel: 'info',
                    resourceId: threadId,
                });

                proxy.stopNotification(
                    id,
                    true,
                    intl.formatMessage(ActivitiesThreadHeaderResources.renameThreadSuccessDescription, { title: newTitle })
                );
                threadMenuHandleRef.current?.updateThreadWithNewTitle(threadId, newTitle);
                notifyThreadTitleUpdate(threadId, newTitle);
            } else {
                const errorMessage = response.error?.message || response.error?.response?.data;
                proxy.log({
                    action: 'updateThreadTitle',
                    actionModifier: 'failure',
                    logLevel: 'error',
                    resourceId: threadId,
                    data: {
                        error: errorMessage,
                    },
                });

                proxy.stopNotification(
                    id,
                    false,
                    intl.formatMessage(ActivitiesThreadHeaderResources.renameThreadFailureDescription, {
                        errorMessage,
                    })
                );
            }
        },
        [intl, proxy, notifyThreadTitleUpdate]
    );

    const updateThreadLastReadTime = useCallback((threadId: string) => {
        threadMenuHandleRef.current?.updateThreadLastReadTime(threadId);
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
                // Get the welcome thread for the last 24 hours if it exists
                const timestampCutoff = new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString();

                const threadsResponse = await threadClient.getThreads({
                    skip: 0,
                    top: 1,
                    orderBy: 'modifiedTimestamp',
                    descending: true,
                    filters: {
                        sources: [ThreadSource.welcomeMessage],
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

                        proxy.logAmplitudeOperationEvent({
                            targetType: 'load',
                            targetAction: 'loaded',
                            targetName: 'autoLoadWelcomeThread',
                            targetFriendlyName: 'Auto-load welcome thread',
                            metadata: {
                                threadId: welcomeThread.id,
                                threadType: ThreadSource.welcomeMessage,
                            },
                        });
                    }
                }
            };

            checkForWelcomeThread();
        }

        return () => {
            isSubscribed = false;
        };
    }, [initialThreadId, activeThreadId, selectThread, threadClient, proxy]);

    return {
        selectedThread,
        addThread,
        deleteThread,
        selectThread,
        updateThreadLastReadTime,
        updateThreadTitle,
        notifyThreadTitleUpdate,
        subscribeThreadTitleUpdate,
        threadContentAndActionKey,
        activeThreadId,
        threadMenuHandleRef,
    };
};
