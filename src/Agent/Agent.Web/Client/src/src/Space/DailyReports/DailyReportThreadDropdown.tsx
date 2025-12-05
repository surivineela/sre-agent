import { Dropdown, Field, Option, Skeleton, SkeletonItem } from '@fluentui/react-components';
import { Dispatch, memo, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getDataPlaneErrorMessage } from '../../Common/Clients/DataPlaneClient';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { ActivitiesThreadHeaderResources, DailyReportsTabResources } from '../../Strings/SREAgentResources';
import ThreadActionsMenu from '../Activities/ThreadActionsMenu';
import { useThreadList } from '../Hooks/useThreadList';

interface DailyReportThreadDropdownProps {
    selectedThread: Thread | null;
    setSelectedThread: Dispatch<React.SetStateAction<Thread | null>>;
}

const DailyReportThreadDropdown: React.FC<DailyReportThreadDropdownProps> = ({ selectedThread, setSelectedThread }) => {
    const intl = useIntl();
    const [isDropdownListBoxHidden, setIsDropdownListBoxHidden] = useState<boolean>(true);
    const [selectedThreadId, setSelectedThreadId] = useState<string | undefined>();
    const [value, setValue] = useState<string>('');
    const [selectedOptions, setSelectedOptions] = useState<string[]>([]);

    const includedSources = useMemo(() => [ThreadSource.dailyReport], []);

    const { threads, threadListDivRef, onScroll, isLoadingInitialThreads, moreThreadsToLoad, intersectionObserverRef, deleteThread } =
        useThreadList(isDropdownListBoxHidden, undefined, includedSources, undefined, undefined, undefined, 'createdTimestamp');

    const proxy = useContext(AzPortalContext);

    const handleThreadDelete = useCallback(
        async (thread: Thread) => {
            proxy.log({
                action: 'deleteReportThread',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId: thread.id,
            });

            const id = proxy.startNotification(
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteReportTitle, { title: thread.title }),
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteReportInProgressDescription)
            );

            deleteThread(thread.id).then(response => {
                if (response.isSuccessful) {
                    setSelectedThreadId(threads.find(t => t.id !== thread.id)?.id);

                    proxy.log({
                        action: 'deleteReportThread',
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId: thread.id,
                    });

                    proxy.stopNotification(id, true, intl.formatMessage(ActivitiesThreadHeaderResources.deleteReportSuccessDescription));
                } else {
                    proxy.log({
                        action: 'deleteReportThread',
                        actionModifier: 'failure',
                        logLevel: 'error',
                        resourceId: thread.id,
                        data: {
                            error: getDataPlaneErrorMessage(response.error),
                        },
                    });

                    proxy.stopNotification(
                        id,
                        false,
                        intl.formatMessage(ActivitiesThreadHeaderResources.deleteReportFailureDescription, {
                            errorMessage: response.error?.message || response.error?.response?.data,
                        })
                    );
                }
            });
        },
        [intl, proxy, deleteThread, threads]
    );

    const firstThreadId = useMemo(() => threads[0]?.id, [threads]);

    useEffect(() => {
        if (firstThreadId) {
            setSelectedThreadId(prev => {
                if (prev) return prev;
                return firstThreadId;
            });
        }
    }, [firstThreadId]);

    useEffect(() => {
        if (selectedThreadId && threads.length > 0) {
            const thread = threads.find(thread => thread.id === selectedThreadId) || null;
            setSelectedThread(prev => {
                if (prev && prev.id === thread?.id) {
                    return prev;
                }
                return thread;
            });
        }
    }, [selectedThreadId, threads]);

    useEffect(() => {
        if (selectedThread) {
            setValue(getSafeDateTime(selectedThread?.createdTimestamp).toLocaleDateString());
            setSelectedOptions([selectedThread.id]);
        } else {
            setValue('');
            setSelectedOptions([]);
        }
    }, [selectedThread]);

    return (
        <div style={{ display: 'flex', flexDirection: 'row', gap: '8px', alignItems: 'center', justifyContent: 'start' }}>
            <Field
                label={<FormattedMessage {...DailyReportsTabResources.selectADate} />}
                orientation="horizontal"
                style={{ display: 'flex', justifyContent: 'flex-start', alignItems: 'center', margin: '20px 0px' }}
            >
                {isLoadingInitialThreads ? (
                    <Skeleton aria-label={intl.formatMessage(DailyReportsTabResources.loadingReportsAriaLabel)} style={{ width: '500px' }}>
                        <SkeletonItem style={{ height: '25px' }} />
                    </Skeleton>
                ) : (
                    <Dropdown
                        value={value}
                        selectedOptions={selectedOptions}
                        onOptionSelect={(_, data) => {
                            setSelectedThreadId(data.optionValue);
                        }}
                        onOpenChange={(_, data) => setIsDropdownListBoxHidden(!data.open)}
                        positioning={{ autoSize: true, overflowBoundaryPadding: { bottom: 20 } }}
                        listbox={{ style: { overflowY: 'auto' }, ref: threadListDivRef, onScroll }}
                    >
                        {threads.map(thread => {
                            return (
                                <Option key={thread.id} value={thread.id}>
                                    {getSafeDateTime(thread.createdTimestamp).toLocaleDateString()}
                                </Option>
                            );
                        })}
                        {moreThreadsToLoad && (
                            <Skeleton
                                aria-label={intl.formatMessage(DailyReportsTabResources.loadingReportsAriaLabel)}
                                ref={intersectionObserverRef}
                            >
                                <SkeletonItem />
                            </Skeleton>
                        )}
                    </Dropdown>
                )}
            </Field>
            {selectedThread && (
                <ThreadActionsMenu
                    thread={selectedThread}
                    handleThreadDelete={() => handleThreadDelete(selectedThread)}
                    hideCopyDeeplink={true}
                    hideFavorite={true}
                    hideRename={true}
                />
            )}
        </div>
    );
};

export default memo(DailyReportThreadDropdown);
