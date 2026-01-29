import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridProps,
    DataGridRow,
    makeStyles,
    OnSelectionChangeData,
    TableCellLayout,
    TableColumnDefinition,
    Text,
    tokens,
} from '@fluentui/react-components';
import { Delete16Regular, Open16Regular } from '@fluentui/react-icons';
import React, { FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { OnboardingWizardResources, SreAgentResources } from '../../../Strings/SREAgentResources';

const useScopeDataGridStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    header: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    title: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    deleteButton: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        color: tokens.colorNeutralForeground2,
        ':hover': {
            color: tokens.colorPaletteRedForeground1,
        },
    },
    dataGridContainer: {
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        overflow: 'hidden',
        maxHeight: '220px',
    },
    emptyState: {
        padding: tokens.spacingVerticalL,
        textAlign: 'center',
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
    cellContent: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    cellIcon: {
        width: '16px',
        height: '16px',
    },
    cellLink: {
        color: tokens.colorBrandForeground1,
        textDecoration: 'none',
        ':hover': {
            textDecoration: 'underline',
        },
    },
});

export interface ScopeDataGridColumn<T> {
    columnId: string;
    headerLabel: string;
    renderCell: (item: T) => React.ReactNode;
    minWidth?: number;
    defaultWidth?: number;
}

interface ScopeDataGridProps<T> {
    title: string;
    items: T[];
    columns: ScopeDataGridColumn<T>[];
    getRowId: (item: T) => string;
    emptyMessage: string;
    ariaLabel: string;
    onDeleteSelected?: (selectedIds: string[]) => void;
}

export const ScopeDataGrid = <T,>({
    title,
    items,
    columns,
    getRowId,
    emptyMessage,
    ariaLabel,
    onDeleteSelected,
}: ScopeDataGridProps<T>): React.ReactElement => {
    const intl = useIntl();
    const styles = useScopeDataGridStyles();

    const [selectedItems, setSelectedItems] = useState<Set<string>>(new Set());

    const handleSelectionChange: DataGridProps['onSelectionChange'] = useCallback((_: unknown, data: OnSelectionChangeData) => {
        const newSet = new Set<string>();
        data.selectedItems.forEach(item => {
            if (typeof item === 'string') {
                newSet.add(item);
            }
        });
        setSelectedItems(newSet);
    }, []);

    const handleDeleteSelected = useCallback(() => {
        if (onDeleteSelected) {
            onDeleteSelected(Array.from(selectedItems));
            setSelectedItems(new Set());
        }
    }, [onDeleteSelected, selectedItems]);

    const tableColumns: TableColumnDefinition<T>[] = useMemo(
        () =>
            columns.map(col =>
                createTableColumn<T>({
                    columnId: col.columnId,
                    renderHeaderCell: () => <Text weight="semibold">{col.headerLabel}</Text>,
                    renderCell: col.renderCell,
                })
            ),
        [columns]
    );

    const columnSizingOptions = useMemo(
        () =>
            columns.reduce(
                (acc, col) => {
                    if (col.minWidth || col.defaultWidth) {
                        acc[col.columnId] = {
                            minWidth: col.minWidth,
                            defaultWidth: col.defaultWidth,
                        };
                    }
                    return acc;
                },
                {} as Record<string, { minWidth?: number; defaultWidth?: number }>
            ),
        [columns]
    );

    if (items.length === 0) {
        return (
            <div className={styles.container}>
                <div className={styles.header}>
                    <Text className={styles.title}>{title}</Text>
                </div>
                <div className={styles.dataGridContainer}>
                    <div className={styles.emptyState}>{emptyMessage}</div>
                </div>
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <Text className={styles.title}>{title}</Text>
                {selectedItems.size > 0 && onDeleteSelected && (
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<Delete16Regular />}
                        className={styles.deleteButton}
                        onClick={handleDeleteSelected}
                    >
                        {intl.formatMessage(OnboardingWizardResources.delete)}
                    </Button>
                )}
            </div>
            <div className={styles.dataGridContainer}>
                <DataGrid
                    items={items}
                    columns={tableColumns}
                    selectionMode="multiselect"
                    selectedItems={selectedItems}
                    onSelectionChange={handleSelectionChange}
                    getRowId={getRowId}
                    focusMode="composite"
                    columnSizingOptions={columnSizingOptions}
                    resizableColumns
                    aria-label={ariaLabel}
                >
                    <DataGridHeader>
                        <DataGridRow
                            selectionCell={{
                                checkboxIndicator: {
                                    'aria-label': intl.formatMessage(SreAgentResources.selectAllRowsAriaLabel),
                                },
                            }}
                        >
                            {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                        </DataGridRow>
                    </DataGridHeader>
                    <DataGridBody<T>>
                        {({ item, rowId }) => (
                            <DataGridRow<T>
                                key={rowId}
                                selectionCell={{
                                    checkboxIndicator: {
                                        'aria-label': intl.formatMessage(SreAgentResources.selectRowAriaLabel),
                                    },
                                }}
                            >
                                {({ renderCell }) => (
                                    <DataGridCell>
                                        <TableCellLayout truncate>{renderCell(item)}</TableCellLayout>
                                    </DataGridCell>
                                )}
                            </DataGridRow>
                        )}
                    </DataGridBody>
                </DataGrid>
            </div>
        </div>
    );
};

// Helper component for cells with icon + link + external link button
interface ScopeCellWithLinkProps {
    icon: React.ReactNode;
    label: string;
    onOpenExternal: () => void;
    openExternalAriaLabel: string;
}

export const ScopeCellWithLink: FC<ScopeCellWithLinkProps> = ({ icon, label, onOpenExternal, openExternalAriaLabel }) => {
    const styles = useScopeDataGridStyles();

    return (
        <div className={styles.cellContent}>
            {icon}
            <Button appearance="transparent" onClick={onOpenExternal} className={styles.cellLink}>
                {label}
            </Button>
            <Button
                appearance="transparent"
                icon={<Open16Regular />}
                size="small"
                onClick={onOpenExternal}
                aria-label={openExternalAriaLabel}
            />
        </div>
    );
};
