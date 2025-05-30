import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate, useParams } from 'react-router';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../Common/Helpers/Guid';
import { ActivitiesThreadHeaderResources } from '../../Strings/SREAgentResources';
import { RemoveThreadFromListHandle } from '../Contracts/Activities';

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
    const removeThreadFromListRef = useRef<RemoveThreadFromListHandle>(null);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const proxy = useContext(AzPortalContext);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const pollNewThreadsImmediately = () => setThreadPollingTriggerId(prev => prev + 1);

    const addThread = useCallback(
        (thread: Thread) => {
            untouched.current = false;
            // poll thread immediately to get the thread just added.
            pollNewThreadsImmediately();
            setActiveThreadId(thread.id);
            navigate({ ...location, pathname: `/views/activities/threads/${thread.id}` });
        },
        [navigate, location]
    );

    const promoteThread = useCallback(() => {
        // poll thread immediately to make the recently updated the thread on top.
        pollNewThreadsImmediately();
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

    const deleteThread = useCallback(
        async (thread: Thread) => {
            const id = proxy.startNotification(
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadTitle),
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadInProgressDescription, { title: thread.title })
            );

            try {
                await threadClient.deleteThread(thread.id);
                removeThreadFromListRef.current?.removeThreadFromList(thread);
                selectThread(null);

                proxy.stopNotification(
                    id,
                    true,
                    intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadSuccessDescription, { title: thread.title })
                );
            } catch (e: any) {
                proxy.stopNotification(
                    id,
                    false,
                    intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadFailureDescription, {
                        title: thread.title,
                        errorMessage: e?.message || e?.response?.data,
                    })
                );
            }
        },
        [intl, selectThread]
    );

    useEffect(() => setThreadContentAndActionKey(Guid.newGuid()), [selectedThread]);

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
        }

        return () => {
            isSubscribed = false;
        };
    }, [initialThreadId, activeThreadId, selectThread]);

    return {
        selectedThread,
        addThread,
        promoteThread,
        deleteThread,
        selectThread,
        threadContentAndActionKey,
        activeThreadId,
        threadPollingTriggerId,
        removeThreadFromListRef,
    };
};
