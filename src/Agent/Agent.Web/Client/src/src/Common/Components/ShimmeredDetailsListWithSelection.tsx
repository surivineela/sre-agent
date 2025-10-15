import { Checkbox } from '@fluentui/react';
import { CheckboxVisibility, ConstrainMode, DetailsListLayoutMode, IColumn } from '@fluentui/react/lib/DetailsList';
import { IShimmeredDetailsListStyles, ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { useCallback, useEffect, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

export interface OnUpdateSelectionArgs<T> {
    selectedItems: T[];
    selectedKeys: string[];
}

interface ShimmeredDetailsListWithSelectionProps<T> {
    items: T[];
    getKey: (item: T) => string;
    columns: IColumn[];
    selectedKeys: string[];
    onUpdateSelection: (args: OnUpdateSelectionArgs<T>) => void;
    /** Default is `true` */
    multiSelect?: boolean;
    /** Default is `false` (ignored if `!multiSelect`) */
    hideSelectAll?: boolean;
    enableShimmer?: boolean;
    layoutMode?: DetailsListLayoutMode;
    constrainMode?: ConstrainMode;
    compact?: boolean;
    selectionColumnWidth?: number;
    className?: string;
    detailsListStyles?: IShimmeredDetailsListStyles;
}

/** Standard Fluent v8 `<ShimmeredDetailsList />`, but implements a custom selection logic/column as the default is...well. */
const ShimmeredDetailsListWithSelection = <T,>(props: ShimmeredDetailsListWithSelectionProps<T>) => {
    const {
        items,
        getKey,
        columns,
        selectedKeys,
        onUpdateSelection,
        multiSelect = true,
        hideSelectAll = false,
        enableShimmer,
        layoutMode = DetailsListLayoutMode.justified,
        constrainMode = ConstrainMode.horizontalConstrained,
        compact = true,
        selectionColumnWidth = 30,
        className,
        detailsListStyles,
    } = props;

    const intl = useIntl();

    const activeKeys = useMemo(() => selectedKeys ?? [], [selectedKeys]);

    const emitSelection = useCallback(
        (next: string[]) => {
            const selectedItems = items.filter(i => next.includes(getKey(i)));
            onUpdateSelection({ selectedItems, selectedKeys: next });
        },
        [items, getKey, onUpdateSelection]
    );

    const toggleItem = useCallback(
        (key: string) => {
            const isSelected = activeKeys.includes(key);
            let next: string[];
            if (multiSelect) {
                next = isSelected ? activeKeys.filter(k => k !== key) : [...activeKeys, key];
            } else {
                next = isSelected ? [] : [key];
            }
            emitSelection(next);
        },
        [multiSelect, activeKeys, emitSelection]
    );

    const allKeys = useMemo(() => items.map(getKey), [items, getKey]);
    const allSelected = useMemo(() => allKeys.length > 0 && allKeys.every(k => activeKeys.includes(k)), [allKeys, activeKeys]);

    const toggleAll = useCallback(
        (checked: boolean) => {
            emitSelection(checked ? [...allKeys] : []);
        },
        [allKeys, emitSelection]
    );

    const selectionColumn: IColumn = useMemo(
        () => ({
            key: '__selection__',
            name: '',
            fieldName: '__selection__',
            minWidth: selectionColumnWidth,
            maxWidth: selectionColumnWidth,
            isResizable: false,
            onRender: (item: T) => {
                const key = getKey(item);
                return (
                    <Checkbox
                        checked={activeKeys.includes(key)}
                        onChange={() => toggleItem(key)}
                        ariaLabel={intl.formatMessage(SreAgentResources.select)}
                    />
                );
            },
            onRenderHeader: () => {
                if (!multiSelect || hideSelectAll) return <div style={{ height: 32 }} />;
                return (
                    <div style={{ display: 'flex', alignItems: 'center', marginTop: 12 }}>
                        <Checkbox
                            checked={allSelected}
                            onChange={(_, c) => toggleAll(!!c)}
                            ariaLabel={intl.formatMessage(SreAgentResources.selectAll)}
                        />
                    </div>
                );
            },
        }),
        [intl, selectionColumnWidth, getKey, activeKeys, toggleItem, multiSelect, hideSelectAll, allSelected, toggleAll]
    );

    const columnsWithSelection = useMemo(() => [selectionColumn, ...columns], [selectionColumn, columns]);
    const itemsWithSelected = useMemo(
        () => items.map(i => ({ ...i, selected: activeKeys.includes(getKey(i)) })),
        [items, activeKeys, getKey]
    );

    // Prune keys that no longer exist when items change (inform parent)
    useEffect(() => {
        const keySet = new Set(items.map(getKey));
        const pruned = activeKeys.filter(k => keySet.has(k));
        if (pruned.length !== activeKeys.length) {
            const selectedItems = items.filter(i => pruned.includes(getKey(i)));
            onUpdateSelection({ selectedItems, selectedKeys: pruned });
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [items]);

    return (
        <ShimmeredDetailsList
            className={className}
            items={itemsWithSelected}
            columns={columnsWithSelection}
            enableShimmer={enableShimmer}
            layoutMode={layoutMode}
            constrainMode={constrainMode}
            compact={compact}
            checkboxVisibility={CheckboxVisibility.hidden}
            styles={detailsListStyles}
        />
    );
};

export default ShimmeredDetailsListWithSelection;
