import {
    ConstrainMode,
    DetailsListLayoutMode,
    IColumn,
    IShimmeredDetailsListProps,
    mergeStyles,
    SelectionMode,
    ShimmeredDetailsList,
} from '@fluentui/react';
import { Checkbox, InputOnChangeData, makeStyles, SearchBox, SearchBoxChangeEvent, SearchBoxProps } from '@fluentui/react-components';
import { debounce } from 'lodash';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { WithSelection } from '../../Common/Contracts/Azure/IncidentHandler';
import { SreAgentResources } from '../../Strings/SREAgentResources';

export interface MultipleSelectionShimmerDetailsListProps<T extends WithSelection<object>> {
    data: T[] | undefined;
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
}

const useSearchBoxStyles = makeStyles({
    searchBox: {
        width: '300px',
        fontSize: '13px',
        zIndex: 1,
    },
});

export const MultipleSelectionShimmerDetailsList = <T extends WithSelection<object>>(
    props: MultipleSelectionShimmerDetailsListProps<T>
) => {
    const {
        data,
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
    } = props;
    const [searchTerm, setSearchTerm] = useState<string>('');
    const intl = useIntl();

    const searchBoxClassNameMerged = mergeStyles(useSearchBoxStyles().searchBox, searchBoxClassName);
    const listContainerStyleMerged: React.CSSProperties = {
        overflowY: 'scroll',
        maxHeight: '365px',
        ...listContainerStyle,
    };

    const selectedItemCount = useMemo(() => {
        return data?.filter(item => item.selected).length ?? 0;
    }, [data]);

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
        let items = data ?? [];
        items = items.filter(item => item && Object.keys(item).length > 0);
        if (searchTerm) {
            items = items.filter(filterFunction);
        }
        return items;
    }, [data, searchTerm, filterFunction]);

    const toggleItemSelection = useCallback(
        (id: string) => {
            const selectedKeys: string[] = [];
            data?.forEach(item => {
                const itemKey = getKey(item);
                const isSelected = itemKey === id ? !item.selected : item.selected;
                if (isSelected) {
                    selectedKeys.push(itemKey);
                }
            });
            onChange(selectedKeys);
        },
        [data, onChange, getKey]
    );

    const onRenderCheckbox = useCallback(
        (item: T) => {
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
                />
            );
        },
        [toggleItemSelection, disabled, selectionLimit, selectedItemCount, getKey]
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
            data?.forEach(item => {
                const itemKey = getKey(item);
                const isSelected = filteredItems?.some(filteredItem => getKey(filteredItem) === itemKey) ? checked : item.selected;
                if (isSelected) {
                    selectedKeys.push(itemKey);
                }
            });
            onChange(selectedKeys);
        },
        [filteredItems, onChange, data, getKey]
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
            />
        );
    }, [allSelectedState, toggleSelectAll, disabled, selectionLimit]);

    const columnsWithCheckbox: IColumn[] = useMemo(() => {
        const checkboxColumn: IColumn = {
            key: 'selected',
            name: '',
            fieldName: 'selected',
            minWidth: 30,
            maxWidth: 30,
            isResizable: false,
            onRenderHeader: onRenderCheckboxHeader,
            onRender: onRenderCheckbox,
            isMultiline: false,
            isSorted: false,
        };

        return [checkboxColumn, ...columns];
    }, [columns, onRenderCheckbox, onRenderCheckboxHeader]);

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
            <div style={listContainerStyleMerged} data-is-scrollable="true">
                <ShimmeredDetailsList
                    items={filteredItems ?? []}
                    columns={columnsWithCheckbox}
                    selectionMode={SelectionMode.none}
                    layoutMode={DetailsListLayoutMode.justified}
                    enableShimmer={loading}
                    useReducedRowRenderer={true}
                    constrainMode={ConstrainMode.horizontalConstrained}
                    compact={true}
                    styles={listStyles}
                />
            </div>
        </>
    );
};
