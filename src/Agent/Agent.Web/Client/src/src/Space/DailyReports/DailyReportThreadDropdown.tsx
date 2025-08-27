import { Dropdown, Field, Option, Skeleton, SkeletonItem } from '@fluentui/react-components';
import { Dispatch, memo, useEffect, useMemo, useState } from 'react';
import { FormattedMessage } from 'react-intl';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { DailyReportsTabResources } from '../../Strings/SREAgentResources';
import { useThreadList } from '../Hooks/useThreadList';

interface DailyReportThreadDropdownProps {
    selectedThread: Thread | null;
    setSelectedThread: Dispatch<React.SetStateAction<Thread | null>>;
}

const DailyReportThreadDropdown: React.FC<DailyReportThreadDropdownProps> = ({ selectedThread, setSelectedThread }) => {
    const [isDropdownListBoxHidden, setIsDropdownListBoxHidden] = useState<boolean>(true);
    const [selectedThreadId, setSelectedThreadId] = useState<string | undefined>();
    const [value, setValue] = useState<string>('');
    const [selectedOptions, setSelectedOptions] = useState<string[]>([]);

    const includedSources = useMemo(() => [ThreadSource.dailyReport], []);

    const { threads, threadListDivRef, onScroll, isLoadingInitialChatMessages, moreThreadsToLoad, intersectionObserverRef } = useThreadList(
        isDropdownListBoxHidden,
        undefined,
        includedSources,
        undefined,
        undefined,
        undefined,
        'createdTimestamp'
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
        <Field
            label={<FormattedMessage {...DailyReportsTabResources.selectADate} />}
            orientation="horizontal"
            style={{ display: 'flex', justifyContent: 'flex-start', alignItems: 'center', margin: '20px 0px' }}
        >
            {isLoadingInitialChatMessages ? (
                <Skeleton style={{ width: '500px' }}>
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
                        <Skeleton ref={intersectionObserverRef}>
                            <SkeletonItem />
                        </Skeleton>
                    )}
                </Dropdown>
            )}
        </Field>
    );
};

export default memo(DailyReportThreadDropdown);
