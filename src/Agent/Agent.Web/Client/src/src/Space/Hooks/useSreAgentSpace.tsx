import { DrawerProps } from '@fluentui/react-components';
import { startTransition, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { Guid } from '../../Common/Helpers/Guid';
import { ActivitiesThreadHeaderResources } from '../../Strings/SREAgentResources';
import { isFinalStreamingMessage, parseThreadFromStreamingText } from '../Activities/Utility';
import { SreAgentContext, StreamingContext } from '../Contracts/Context';
import { PrimaryNavItemValues, SecondaryNavItemValues, ThreadCategoryKey } from '../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from './useAgentSiteNavigate';
import { InputForThreadListWithFavoriteList, useThreadListWithFavoriteList } from './useThreadListWithFavoriteList';

export const useSreAgentSpace = () => {
    const intl = useIntl();

    const { subscribeThreadUpdateEvent, subscribeMessageUpdateEvent } = useContext(StreamingContext);
    const proxy = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { agentLoading } = useContext(SreAgentContext);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const navigate = useAgentSiteNavigate();

    const [showUnreadOnly, setShowUnreadOnly] = useState<boolean>(false);
    const [threadsRenderKey, setThreadsRenderKey] = useState<string>(Guid.newGuid());
    const [openedCategoryNavItems, setOpenedCategoryNavItems] = useState<(PrimaryNavItemValues | ThreadCategoryKey)[]>([
        ThreadCategoryKey.Favorites,
        ThreadCategoryKey.Regular,
    ]);
    const [navBarState, setNavBarState] = useState<{ isNavBarOpen: boolean; navBarType: DrawerProps['type'] }>({
        isNavBarOpen: true,
        navBarType: 'inline',
    });

    const isNavBarHidden = useMemo(() => navBarState.navBarType === 'overlay' && !navBarState.isNavBarOpen, [navBarState]);
    const navBarType = useMemo(() => (navBarState.isNavBarOpen ? navBarState.navBarType : 'inline'), [navBarState]);

    const threadTitleUpdateListeners = useRef<Set<(threadId: string, newTitle: string) => void>>(new Set());
    const threadFavoriteUpdateListeners = useRef<Set<(threadId: string, isFavorite: boolean) => void>>(new Set());

    const onExpandOrCollapseNavBar = useCallback(
        (newState: boolean) => {
            setNavBarState(prev => ({ ...prev, isNavBarOpen: newState }));

            proxy.logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: `${newState ? 'expand' : 'collapse'}ThreadMenu`,
                targetFriendlyName: `${newState ? 'Expand' : 'Collapse'} Thread Menu`,
                valueObjectName: SpecialControlValue.DoAction,
                valueObjectFriendlyName: SpecialControlValue.DoAction,
            });
        },
        [proxy]
    );

    const onNavItemSelected = useCallback(() => {
        if (navBarState.navBarType === 'overlay') {
            onExpandOrCollapseNavBar(false);
        }
    }, [navBarState.navBarType, onExpandOrCollapseNavBar]);

    const onClickCategoryNavItem = useCallback((categoryKey: PrimaryNavItemValues | ThreadCategoryKey) => {
        const removeNonThreadCategoryKeys = (keys: (PrimaryNavItemValues | ThreadCategoryKey)[]) => {
            return keys.filter(key => {
                return !Object.values(PrimaryNavItemValues).some(navItemValue => navItemValue === key);
            });
        };

        setOpenedCategoryNavItems(prevOpenedCategoryNavItems => {
            if (prevOpenedCategoryNavItems.includes(categoryKey)) {
                return prevOpenedCategoryNavItems.filter(key => key !== categoryKey);
            } else {
                return [...removeNonThreadCategoryKeys(prevOpenedCategoryNavItems), categoryKey];
            }
        });
    }, []);

    const onClickNavItem = useCallback(
        (primary: PrimaryNavItemValues, secondary: SecondaryNavItemValues | undefined, threadId: string | undefined) => {
            navigate({
                primaryNavItemValue: primary,
                secondaryNavItemValue: secondary,
                threadId: threadId,
            });
            onNavItemSelected();
        },
        [navigate, onNavItemSelected]
    );

    const onClickThreadNavItem = useCallback(
        (primary: PrimaryNavItemValues, threadId: string | null) => {
            // Since navigate() is in transition, we need to wrap setState in startTransition to put it in the same priority level
            startTransition(() => {
                onClickNavItem(primary, undefined, threadId ? threadId : undefined);
                setThreadsRenderKey(Guid.newGuid());
            });
        },
        [onClickNavItem]
    );

    const onClickNonThreadSubNavItem = useCallback(
        (primary: PrimaryNavItemValues, secondary: SecondaryNavItemValues) => {
            onClickNavItem(primary, secondary, undefined);
        },
        [onClickNavItem]
    );

    const selectThread = useCallback(
        (threadId: string | null) => {
            onClickThreadNavItem(PrimaryNavItemValues.Threads, threadId);
        },
        [onClickThreadNavItem]
    );

    const selectOverview = useCallback(() => {
        onClickThreadNavItem(PrimaryNavItemValues.Overview, null);
    }, [onClickThreadNavItem]);

    const addThread = useCallback(
        (threadId: string) => {
            onClickNavItem(PrimaryNavItemValues.Threads, undefined, threadId);
        },
        [onClickNavItem]
    );

    const excludedSources: ThreadSource[] = useMemo(() => [ThreadSource.incident, ThreadSource.dailyReport], []);

    const filter: InputForThreadListWithFavoriteList = useMemo(
        () => ({
            includedSources: undefined,
            excludedSources,
            unreadOnly: showUnreadOnly,
            searchText: undefined,
        }),
        [excludedSources, showUnreadOnly]
    );

    const isFavoriteThreadListHidden = useMemo(
        () => !openedCategoryNavItems.includes(ThreadCategoryKey.Favorites),
        [openedCategoryNavItems]
    );
    const isRegularThreadListHidden = useMemo(() => !openedCategoryNavItems.includes(ThreadCategoryKey.Regular), [openedCategoryNavItems]);

    const {
        removeThread,
        removeUnreadThreadId,
        onThreadModifiedTimestampUpdated,
        updateThreadWithNewTitle,
        updateThreadFavoriteProperty,
        threadListsState,
        unreadThreadIds,
        favoriteThreadsIntersectionObserverRef,
        regularThreadsIntersectionObserverRef,
        threadItemDivsRef,
        isUpdatingThreadFavoriteProperty,
    } = useThreadListWithFavoriteList(
        isFavoriteThreadListHidden || !navBarState.isNavBarOpen || agentLoading,
        isRegularThreadListHidden || !navBarState.isNavBarOpen || agentLoading,
        filter,
        'modifiedTimestamp'
    );

    const notifyThreadTitleUpdate = useCallback((threadId: string, newTitle: string) => {
        threadTitleUpdateListeners.current.forEach(listener => listener(threadId, newTitle));
    }, []);

    const notifyThreadFavoriteUpdate = useCallback((threadId: string, isFavorite: boolean) => {
        threadFavoriteUpdateListeners.current.forEach(listener => listener(threadId, isFavorite));
    }, []);

    const subscribeThreadTitleUpdate = useCallback((listener: (threadId: string, newTitle: string) => void) => {
        threadTitleUpdateListeners.current.add(listener);
        return () => {
            threadTitleUpdateListeners.current.delete(listener);
        };
    }, []);

    const subscribeThreadFavoriteUpdate = useCallback((listener: (threadId: string, isFavorite: boolean) => void) => {
        threadFavoriteUpdateListeners.current.add(listener);
        return () => {
            threadFavoriteUpdateListeners.current.delete(listener);
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
                updateThreadWithNewTitle(threadId, newTitle);
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

    const updateThreadFavorite = useCallback(
        async (threadId: string, isFavorite: boolean) => {
            const response = await threadClient.updateThreadFavorite(threadId, isFavorite);

            if (response.isSuccessful) {
                proxy.log({
                    action: 'updateThreadFavorite',
                    actionModifier: 'success',
                    logLevel: 'info',
                    resourceId: threadId,
                });
                updateThreadFavoriteProperty(threadId, isFavorite);
                notifyThreadFavoriteUpdate(threadId, isFavorite);
            } else {
                const errorMessage = response.error?.message || response.error?.response?.data;
                proxy.log({
                    action: 'updateThreadFavorite',
                    actionModifier: 'failure',
                    logLevel: 'error',
                    resourceId: threadId,
                    data: {
                        error: errorMessage,
                    },
                });
            }
        },
        [proxy, threadClient]
    );

    const updateThreadLastReadTime = useCallback(
        async (threadId: string) => {
            const response = await threadClient.updateThreadLastReadTime(threadId);

            if (response.isSuccessful) {
                removeUnreadThreadId(threadId);
            }
        },
        [removeUnreadThreadId]
    );

    const removeThreadFromList = useCallback(
        (threadId: string) => {
            removeThread(threadId);
        },
        [removeThread]
    );

    const deleteThread = useCallback(
        async (thread: Thread) => {
            proxy.logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: 'confirmDeleteThread',
                targetFriendlyName: 'Confirm delete thread',
                valueObjectName: thread.id,
                valueObjectFriendlyName: thread.id,
            });

            const id = proxy.startNotification(
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadTitle, { title: thread.title }),
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadInProgressDescription)
            );

            try {
                await threadClient.deleteThread(thread.id);
                removeThreadFromList(thread.id);
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

    const getThread = async (threadId: string): Promise<Thread | undefined> => {
        const response = await threadClient.getThread(threadId);
        if (response.isSuccessful && response.content) {
            return response.content;
        }
        return undefined;
    };

    useEffect(() => {
        const handleResize = () => {
            const smallerScreen = window.innerWidth <= 840;
            setNavBarState(prev => {
                const prevType = prev.navBarType;

                const newType = smallerScreen ? 'overlay' : 'inline';

                if (prevType === 'inline' && newType === 'overlay') {
                    return {
                        isNavBarOpen: false,
                        navBarType: newType,
                    };
                }

                return {
                    ...prev,
                    navBarType: newType,
                };
            });
        };

        handleResize();

        window.addEventListener('resize', handleResize);
        return () => window.removeEventListener('resize', handleResize);
    }, []);

    useEffect(() => {
        const isExcludedThread = (thread: Thread) => {
            return thread.source && excludedSources && excludedSources.includes(thread.source);
        };

        const messageUpdateHandler = async (message: StreamingMessage) => {
            const threadId = message.additionalProperties?.threadId;
            if (threadId && isFinalStreamingMessage(message)) {
                const updatedThread = await getThread(threadId);
                if (updatedThread && !isExcludedThread(updatedThread)) {
                    onThreadModifiedTimestampUpdated(updatedThread);
                }
            }
        };

        const threadCreateHandler = async (message: StreamingMessage) => {
            const threadId = message.additionalProperties?.threadId;
            const text = message.contents?.[0]?.text || '';
            if (threadId) {
                try {
                    const thread = parseThreadFromStreamingText(text);
                    if (!isExcludedThread(thread)) {
                        onThreadModifiedTimestampUpdated(thread);
                    }
                } catch {
                    const updatedThread = await getThread(threadId);
                    if (updatedThread && !isExcludedThread(updatedThread)) {
                        onThreadModifiedTimestampUpdated(updatedThread);
                    }
                }
            }
        };

        const unsubscribeMessageUpdateEvent = subscribeMessageUpdateEvent({
            handler: messageUpdateHandler,
        });

        const unsubscribeThreadUpdateEvent = subscribeThreadUpdateEvent(threadCreateHandler);

        return () => {
            unsubscribeMessageUpdateEvent();
            unsubscribeThreadUpdateEvent();
        };
    }, [subscribeThreadUpdateEvent, subscribeMessageUpdateEvent, onThreadModifiedTimestampUpdated, excludedSources]);

    return {
        threadsRenderKey,
        excludedSources,
        showUnreadOnly,
        setShowUnreadOnly,
        selectThread,
        selectOverview,
        addThread,
        threadListsState,
        unreadThreadIds,
        favoriteThreadsIntersectionObserverRef,
        regularThreadsIntersectionObserverRef,
        threadItemDivsRef,
        isUpdatingThreadFavoriteProperty,
        updateThreadTitle,
        updateThreadFavorite,
        updateThreadLastReadTime,
        deleteThread,
        subscribeThreadTitleUpdate,
        subscribeThreadFavoriteUpdate,

        onExpandOrCollapseNavBar,
        openedCategoryNavItems,
        onClickCategoryNavItem,
        onClickNonThreadSubNavItem,

        isNavOpen: navBarState.isNavBarOpen,
        navBarType,
        navBarTypeWhenOpen: navBarState.navBarType,
        isNavBarHidden,
    };
};
