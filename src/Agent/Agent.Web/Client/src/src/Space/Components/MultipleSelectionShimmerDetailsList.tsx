import {
    ConstrainMode,
    DetailsListLayoutMode,
    IColumn,
    IShimmeredDetailsListProps,
    mergeStyles,
    mergeStyleSets,
    SelectionMode,
    ShimmeredDetailsList,
} from '@fluentui/react';
import {
    Button,
    Checkbox,
    InputOnChangeData,
    makeStyles,
    SearchBox,
    SearchBoxChangeEvent,
    SearchBoxProps,
    Skeleton,
    SkeletonItem,
} from '@fluentui/react-components';
import { Add12Regular, Checkmark12Regular } from '@fluentui/react-icons';
import { debounce } from 'lodash';
import { forwardRef, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import InfiniteScroll from 'react-infinite-scroll-component';
import { useIntl } from 'react-intl';
import { WithSelection } from '../../Common/Contracts/Azure/IncidentHandler';
import { Guid } from '../../Common/Helpers/Guid';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { getIntervalBetweenLoading } from '../Activities/Utility';

const useIntersectionObserver = (
    isLoadingInitialItems: boolean,
    hasMoreOldItems: boolean,
    loadMoreOldItems: (overflowDiv: boolean) => Promise<boolean | undefined>
) => {
    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);
    const intersectionObserverRef = useRef<HTMLDivElement | null>(null);
    const timeoutId = useRef<NodeJS.Timeout | null>(null);

    // Use an intersection observer to load more items to overflow the items list div if the current number of items
    // does not overflow the items list div anymore due to events such as zoom out, which makes InifiniteScroll not able to work.
    useEffect(() => {
        const observer = new IntersectionObserver((entries: IntersectionObserverEntry[]) => {
            const entry = entries[0];
            setIsIntersecting(entry.isIntersecting);
        });
        if (observer && intersectionObserverRef.current && !isLoadingInitialItems) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
            setIsIntersecting(false);
        };
    }, [isLoadingInitialItems]);

    useEffect(() => {
        if (isIntersecting && hasMoreOldItems) {
            let exponentialBackoffDepth = -1;

            const loadOldItems = async () => {
                const isSuccessful = await loadMoreOldItems(true);

                exponentialBackoffDepth = isSuccessful === false ? exponentialBackoffDepth + 1 : -1;
                const interval = getIntervalBetweenLoading(exponentialBackoffDepth);

                timeoutId.current = setTimeout(loadOldItems, interval);
            };
            loadOldItems();
        }

        return () => {
            if (timeoutId.current !== null) {
                clearTimeout(timeoutId.current);
                timeoutId.current = null;
            }
        };
    }, [loadMoreOldItems, isIntersecting, hasMoreOldItems]);

    useEffect(() => {
        // Cleanup the timeout when the component unmounts or dependencies change
        return () => {
            if (timeoutId.current !== null) {
                clearTimeout(timeoutId.current);
                timeoutId.current = null;
            }
        };
    }, []);

    return {
        intersectionObserverRef,
    };
};

export interface MultipleSelectionShimmerDetailsListProps<T extends object> {
    data: T[] | undefined;
    selectedKeys?: string[];
    onChange: (selectedKeys: string[]) => void;
    getKey: (item: T) => string;
    loading: boolean;
    columns: IColumn[];
    filter?: (searchTerm: string, item: T) => boolean;
    disabled?: boolean;
    selectionLimit?: number;
    searchBoxClassName?: SearchBoxProps['className'];
    listContainerStyle?: React.CSSProperties;
    listStyles?: IShimmeredDetailsListProps['styles'];
    isLoadingInitialItems?: boolean;
    hasMoreItems?: boolean;
    loadMoreItems?: (overflowDiv: boolean) => Promise<boolean | undefined>;
    isPicker?: boolean;
    disallowSelection?: boolean;
}

const useStyles = makeStyles({
    searchBox: {
        width: '300px',
        fontSize: '13px',
        marginBottom: '16px',
        zIndex: 1,
    },
    iconButton: {
        position: 'absolute',
        margin: '0',
        padding: '0',
        border: 'none',
        minWidth: 'auto',
        minHeight: 'auto',
        '&:hover': {
            backgroundColor: 'transparent',
        },
        '&:active': {
            backgroundColor: 'transparent',
        },
    },
    iconContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        margin: '0',
        padding: '0',
        minWidth: 'auto',
        minHeight: 'auto',
        width: '20px',
        height: '20px',
    },
});

type MultipleSelectionShimmerDetailsListForwardRef = <T extends object>(
    props: MultipleSelectionShimmerDetailsListProps<T> & React.RefAttributes<HTMLDivElement | null>
) => React.ReactElement | null;

export const MultipleSelectionShimmerDetailsList: MultipleSelectionShimmerDetailsListForwardRef = forwardRef(
    <T extends object>(props: MultipleSelectionShimmerDetailsListProps<T>, ref: React.ForwardedRef<HTMLDivElement | null>) => {
        const {
            data,
            selectedKeys,
            onChange,
            columns,
            getKey,
            filter: filter,
            loading,
            disabled,
            selectionLimit,
            searchBoxClassName,
            listContainerStyle,
            listStyles,
            isLoadingInitialItems,
            loadMoreItems,
            hasMoreItems,
            isPicker,
            disallowSelection,
        } = props;
        const [listWrapperId] = useState(Guid.newShortGuid());
        const [searchTerm, setSearchTerm] = useState<string>('');
        const intl = useIntl();
        const styles = useStyles();

        const searchBoxClassNameMerged = mergeStyles(styles.searchBox, searchBoxClassName);
        const listContainerStyleMerged: React.CSSProperties = useMemo(
            () => ({
                overflowY: 'scroll',
                overflowX: 'auto',
                maxHeight: '365px',
                ...listContainerStyle,
            }),
            [listContainerStyle]
        );

        const { intersectionObserverRef } = useIntersectionObserver(
            !!isLoadingInitialItems,
            !!hasMoreItems,
            loadMoreItems ?? (() => Promise.resolve(true))
        );

        const dataWithSelectedState = useMemo(() => {
            return data?.map(item => {
                const itemKey = getKey(item);
                const isSelected = selectedKeys?.includes(itemKey) ?? false;
                return {
                    ...item,
                    selected: isSelected,
                };
            });
        }, [data, selectedKeys, getKey]);

        const selectedItemCount = useMemo(() => selectedKeys?.length ?? 0, [selectedKeys?.length]);

        const filterFunction = useCallback(
            (item: T) => {
                if (!filter) {
                    return true;
                }
                return filter(searchTerm, item);
            },
            [filter, searchTerm]
        );

        const filteredItems = useMemo(() => {
            let items = dataWithSelectedState ?? [];
            items = items.filter(item => item && Object.keys(item).length > 0);
            if (searchTerm) {
                items = items.filter(filterFunction);
            }
            return items;
        }, [dataWithSelectedState, searchTerm, filterFunction]);

        const toggleItemSelection = useCallback(
            (id: string) => {
                if (!selectedKeys) {
                    onChange([id]);
                } else {
                    if (selectedKeys.includes(id)) {
                        // If the item is already selected, remove it from the selection
                        onChange(selectedKeys.filter(key => key !== id));
                    } else {
                        // If the item is not selected, add it to the selection
                        onChange([...selectedKeys, id]);
                    }
                }
            },
            [selectedKeys, onChange]
        );

        const onRenderCheckbox = useCallback(
            (item: WithSelection<T>) => {
                return (
                    <Checkbox
                        checked={item.selected}
                        onChange={() => toggleItemSelection(getKey(item))}
                        disabled={disabled || (!!selectionLimit && selectedItemCount >= selectionLimit && !item.selected)}
                        input={{
                            style: { width: 16 },
                        }}
                        indicator={{
                            style: { margin: 'auto' },
                        }}
                        aria-label={intl.formatMessage(SreAgentResources.selectRowAriaLabel)}
                    />
                );
            },
            [disabled, selectionLimit, selectedItemCount, intl, toggleItemSelection, getKey]
        );

        const onRenderAddButton = useCallback(
            (item: WithSelection<T>) => {
                return !item.selected ? (
                    <Button
                        icon={<Add12Regular />}
                        onClick={() => toggleItemSelection(getKey(item))}
                        disabled={item.selected || disabled || (!!selectionLimit && selectedItemCount >= selectionLimit)}
                        appearance="subtle"
                        className={styles.iconButton}
                        aria-label={intl.formatMessage(SreAgentResources.add)}
                    />
                ) : (
                    <div className={styles.iconContainer}>
                        <Checkmark12Regular />
                    </div>
                );
            },
            [disabled, selectionLimit, selectedItemCount, intl, toggleItemSelection, getKey, styles.iconButton, styles.iconContainer]
        );

        const allSelectedState = useMemo((): boolean | 'mixed' => {
            if (!filteredItems || filteredItems.length === 0) {
                // There are no items, so we can't determine a selection state
                return false;
            }

            if (filteredItems.every(item => item.selected)) {
                // All items are selected, so return true
                return true;
            }

            if (filteredItems.every(item => !item.selected)) {
                // No items are selected, so return false
                return false;
            }

            return 'mixed';
        }, [filteredItems]);

        const toggleSelectAll = useCallback(
            (checked: boolean) => {
                const selectedKeys: string[] = [];
                dataWithSelectedState?.forEach(item => {
                    const itemKey = getKey(item);
                    const isSelected = filteredItems?.some(filteredItem => getKey(filteredItem) === itemKey) ? checked : item.selected;
                    if (isSelected) {
                        selectedKeys.push(itemKey);
                    }
                });
                onChange(selectedKeys);
            },
            [filteredItems, onChange, dataWithSelectedState, getKey]
        );

        const onRenderCheckboxHeader = useCallback(() => {
            return (
                <Checkbox
                    checked={allSelectedState}
                    onChange={(_, data) => toggleSelectAll(!!data.checked)}
                    disabled={disabled || !!selectionLimit}
                    input={{
                        style: { width: 16 },
                    }}
                    indicator={{
                        style: { margin: 'auto' },
                    }}
                    aria-label={intl.formatMessage(SreAgentResources.selectAllRowsAriaLabel)}
                />
            );
        }, [allSelectedState, disabled, selectionLimit, intl, toggleSelectAll]);

        const columnsWithCheckbox: IColumn[] = useMemo(() => {
            const checkboxColumn: IColumn = {
                key: 'selected',
                name: '',
                fieldName: 'selected',
                minWidth: 30,
                maxWidth: 30,
                isResizable: false,
                onRenderHeader: isPicker ? (props, defaultRenderer) => defaultRenderer?.(props) ?? <></> : onRenderCheckboxHeader,
                onRender: isPicker ? onRenderAddButton : onRenderCheckbox,
                isMultiline: false,
                isSorted: false,
            };

            return [checkboxColumn, ...columns];
        }, [isPicker, columns, onRenderAddButton, onRenderCheckbox, onRenderCheckboxHeader]);

        return (
            <>
                {!!filter && (
                    <SearchBox
                        className={searchBoxClassNameMerged}
                        placeholder={intl.formatMessage(SreAgentResources.search)}
                        value={searchTerm}
                        onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchTerm(data.value ?? ''))}
                    />
                )}
                <div style={listContainerStyleMerged} data-is-scrollable="true" id={listWrapperId} ref={ref}>
                    <InfiniteScroll
                        dataLength={filteredItems.length}
                        next={() => {
                            loadMoreItems?.(false);
                        }}
                        hasMore={hasMoreItems ?? false}
                        loader={null}
                        scrollThreshold={0.1} // Trigger loading more items when scrolled to 10% of the scrollable area
                        scrollableTarget={listWrapperId}
                        style={{ overflow: 'visible' }}
                    >
                        <ShimmeredDetailsList
                            items={filteredItems ?? []}
                            columns={disallowSelection ? columns : columnsWithCheckbox}
                            selectionMode={SelectionMode.none}
                            layoutMode={DetailsListLayoutMode.justified}
                            enableShimmer={loading}
                            useReducedRowRenderer={true}
                            constrainMode={ConstrainMode.horizontalConstrained}
                            compact={true}
                            detailsListStyles={mergeStyleSets(
                                {
                                    root: { overflowX: 'visible' },
                                    headerWrapper: {
                                        '& > div': {
                                            paddingTop: '0px !important',
                                        },
                                    },
                                },
                                listStyles
                            )}
                        />
                        {hasMoreItems && (
                            <Skeleton ref={intersectionObserverRef} aria-label={intl.formatMessage(SreAgentResources.loadingMoreRows)}>
                                <SkeletonItem />
                            </Skeleton>
                        )}
                    </InfiniteScroll>
                </div>
            </>
        );
    }
) as MultipleSelectionShimmerDetailsListForwardRef;
