import {
    Button,
    Checkbox,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    MessageBar,
    MessageBarBody,
    SearchBox,
    Switch,
    TableColumnDefinition,
    Text,
    Tooltip,
    createTableColumn,
} from '@fluentui/react-components';
import { CheckmarkStarburst16Filled, Dismiss24Regular, Info16Regular, LockClosed16Regular } from '@fluentui/react-icons';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { OnboardingWizardResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import AzPortalProxy from '../../AzPortalProxy/AzPortalProxy';
import { SpecialControlValue } from '../../AzPortalProxy/Models/IAmplitude';
import { AzPortalContext } from '../../AzPortalProxy/Providers/AzPortalProxyContext';
import { useAzureResourcePickerDialogStyles } from './AzureResourcePickerDialog.styles';
import { AzureResourcePickerSkeleton } from './AzureResourcePickerSkeleton';
import { AzureResourceWithPermission } from './Contracts';

export interface AzureResourcePickerDialogProps<T extends AzureResourceWithPermission> {
    isOpen: boolean;
    onDismiss: () => void;
    onApply: (selectedIds: string[]) => void;
    initialSelectedIds: string[];
    title: string;
    searchPlaceholder: string;
    infoMessage: string;
    noPermissionMessage: string;
    selectableItems: T[];
    disabledItems: T[];
    isLoading: boolean;
    columns: TableColumnDefinition<T>[];
    columnSizingOptions: Record<string, { minWidth?: number; defaultWidth?: number; idealWidth?: number }>;
    telemetryName: string;
    getItemId?: (item: T) => string;
    getItemName?: (item: T) => string;
    additionalSearchFilter?: (item: T, searchLower: string) => boolean;
    filterByRecommended?: (item: T) => boolean;
    applyButtonText?: string;
    showRecommendedLabel?: string;
    showRecommendedTooltip?: string;
    /** Optional filter elements to render in the header row before the search box */
    filterElements?: React.ReactNode;
}

export const AzureResourcePickerDialog = <T extends AzureResourceWithPermission>({
    isOpen,
    onDismiss,
    onApply,
    initialSelectedIds,
    title,
    searchPlaceholder,
    infoMessage,
    noPermissionMessage,
    selectableItems,
    disabledItems,
    isLoading,
    columns,
    columnSizingOptions,
    telemetryName,
    getItemId = item => item.id,
    getItemName = item => item.name,
    additionalSearchFilter,
    filterByRecommended,
    applyButtonText,
    showRecommendedLabel,
    showRecommendedTooltip,
    filterElements,
}: AzureResourcePickerDialogProps<T>): JSX.Element => {
    const intl = useIntl();
    const styles = useAzureResourcePickerDialogStyles();
    const portalContext = useContext(AzPortalContext) as AzPortalProxy;

    const [searchFilter, setSearchFilter] = useState<string>('');
    const [showRecommended, setShowRecommended] = useState<boolean>(false);
    const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set(initialSelectedIds));

    // Sync selectedIds when dialog opens to reflect current initialSelectedIds
    useEffect(() => {
        if (isOpen) {
            setSelectedIds(new Set(initialSelectedIds));
        }
    }, [isOpen, initialSelectedIds]);

    const filterItems = useCallback(
        (items: T[]): T[] => {
            let filtered = items;

            if (searchFilter.trim()) {
                const searchLower = searchFilter.toLowerCase();
                filtered = filtered.filter(item => {
                    const nameMatch = getItemName(item).toLowerCase().includes(searchLower);
                    const additionalMatch = additionalSearchFilter?.(item, searchLower) ?? false;
                    return nameMatch || additionalMatch;
                });
            }

            if (showRecommended) {
                if (filterByRecommended) {
                    filtered = filtered.filter(filterByRecommended);
                } else {
                    filtered = filtered.filter(item => item.recommended === true);
                }
            }

            return filtered;
        },
        [searchFilter, showRecommended, getItemName, additionalSearchFilter, filterByRecommended]
    );

    const filteredSelectableItems = useMemo(() => filterItems(selectableItems), [filterItems, selectableItems]);

    const filteredDisabledItems = useMemo(() => filterItems(disabledItems), [filterItems, disabledItems]);

    const allVisibleSelected = useMemo(() => {
        if (filteredSelectableItems.length === 0) return false;
        return filteredSelectableItems.every(item => selectedIds.has(getItemId(item)));
    }, [filteredSelectableItems, selectedIds, getItemId]);

    const someVisibleSelected = useMemo(() => {
        return filteredSelectableItems.some(item => selectedIds.has(getItemId(item)));
    }, [filteredSelectableItems, selectedIds, getItemId]);

    const handleToggleSelection = useCallback((itemId: string) => {
        setSelectedIds(prev => {
            const next = new Set(prev);
            if (next.has(itemId)) {
                next.delete(itemId);
            } else {
                next.add(itemId);
            }
            return next;
        });
    }, []);

    const handleSelectAll = useCallback(() => {
        if (allVisibleSelected) {
            setSelectedIds(prev => {
                const next = new Set(prev);
                filteredSelectableItems.forEach(item => next.delete(getItemId(item)));
                return next;
            });
        } else {
            setSelectedIds(prev => {
                const next = new Set(prev);
                filteredSelectableItems.forEach(item => next.add(getItemId(item)));
                return next;
            });
        }
    }, [allVisibleSelected, filteredSelectableItems, getItemId]);

    const handleApply = useCallback(() => {
        portalContext.logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: `${telemetryName}Apply`,
            targetFriendlyName: `${telemetryName} apply`,
            valueObjectName: SpecialControlValue.DoAction,
            valueObjectFriendlyName: SpecialControlValue.DoAction,
            metadata: { selectedCount: selectedIds.size },
        });
        onApply(Array.from(selectedIds));
    }, [onApply, selectedIds, portalContext, telemetryName]);

    const handleShowRecommendedToggle = useCallback(
        (checked: boolean) => {
            portalContext.logAmplitudeControlEvent({
                targetType: 'toggle',
                targetAction: 'changed',
                targetName: `${telemetryName}ShowRecommended`,
                targetFriendlyName: `${telemetryName} show recommended toggle`,
                valueObjectName: checked ? 'checked' : 'unchecked',
                valueObjectFriendlyName: checked ? 'Checked' : 'Unchecked',
            });
            setShowRecommended(checked);
        },
        [portalContext, telemetryName]
    );

    const selectableColumns: TableColumnDefinition<T>[] = useMemo(
        () => [
            createTableColumn<T>({
                columnId: 'checkbox',
                renderHeaderCell: () => (
                    <Checkbox
                        checked={allVisibleSelected ? true : someVisibleSelected ? 'mixed' : false}
                        onChange={handleSelectAll}
                        aria-label={intl.formatMessage(OnboardingWizardResources.selectAll)}
                    />
                ),
                renderCell: item => (
                    <Checkbox
                        checked={selectedIds.has(getItemId(item))}
                        onChange={() => handleToggleSelection(getItemId(item))}
                        aria-label={getItemName(item)}
                    />
                ),
            }),
            ...columns,
        ],
        [
            columns,
            selectedIds,
            allVisibleSelected,
            someVisibleSelected,
            handleSelectAll,
            handleToggleSelection,
            getItemId,
            getItemName,
            intl,
        ]
    );

    const fullSelectableColumnSizingOptions = useMemo(
        () => ({
            checkbox: { minWidth: 48, defaultWidth: 48, idealWidth: 48 },
            ...columnSizingOptions,
        }),
        [columnSizingOptions]
    );

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => !data.open && onDismiss()}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBody}>
                    <DialogTitle className={styles.dialogTitle}>
                        <Text weight="semibold" size={500}>
                            {title}
                        </Text>
                        <Button
                            appearance="subtle"
                            icon={<Dismiss24Regular />}
                            onClick={onDismiss}
                            aria-label={intl.formatMessage(OnboardingWizardResources.cancel)}
                        />
                    </DialogTitle>

                    <DialogContent className={styles.dialogContent}>
                        <div className={styles.headerRow}>
                            <SearchBox
                                placeholder={searchPlaceholder}
                                value={searchFilter}
                                onChange={(_, data) => setSearchFilter(data.value ?? '')}
                            />
                            {filterElements}
                            {showRecommendedLabel && (
                                <div className={styles.toggleSection}>
                                    <CheckmarkStarburst16Filled className={styles.recommendedIcon} />
                                    <Text>{showRecommendedLabel}</Text>
                                    {showRecommendedTooltip && (
                                        <Tooltip content={showRecommendedTooltip} relationship="label">
                                            <Info16Regular className={styles.infoIcon} />
                                        </Tooltip>
                                    )}
                                    <Switch checked={showRecommended} onChange={(_, data) => handleShowRecommendedToggle(data.checked)} />
                                </div>
                            )}
                        </div>

                        <MessageBar className={styles.infoMessageBar} intent="info" icon={<Info16Regular />}>
                            <MessageBarBody>{infoMessage}</MessageBarBody>
                        </MessageBar>

                        <Text className={styles.selectedCountText}>
                            {intl.formatMessage(OnboardingWizardResources.countSelected, { count: selectedIds.size })}
                        </Text>

                        {isLoading ? (
                            <AzureResourcePickerSkeleton />
                        ) : (
                            <div className={styles.gridContainer}>
                                <DataGrid
                                    items={filteredSelectableItems}
                                    columns={selectableColumns}
                                    getRowId={item => getItemId(item)}
                                    className={styles.dataGrid}
                                    resizableColumns
                                    columnSizingOptions={fullSelectableColumnSizingOptions}
                                >
                                    <DataGridHeader className={styles.dataGridHeader}>
                                        <DataGridRow>
                                            {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                                        </DataGridRow>
                                    </DataGridHeader>
                                    <DataGridBody<T>>
                                        {({ item, rowId }) => (
                                            <DataGridRow<T> key={rowId}>
                                                {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                            </DataGridRow>
                                        )}
                                    </DataGridBody>
                                </DataGrid>

                                {filteredDisabledItems.length > 0 && (
                                    <>
                                        <div className={styles.disabledSectionHeader}>
                                            <LockClosed16Regular />
                                            <Text>{noPermissionMessage}</Text>
                                        </div>

                                        <DataGrid
                                            items={filteredDisabledItems}
                                            columns={columns}
                                            getRowId={item => getItemId(item)}
                                            className={styles.dataGrid}
                                            resizableColumns
                                            columnSizingOptions={columnSizingOptions}
                                        >
                                            <DataGridHeader className={styles.dataGridHeader}>
                                                <DataGridRow>
                                                    {({ renderHeaderCell }) => (
                                                        <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>
                                                    )}
                                                </DataGridRow>
                                            </DataGridHeader>
                                            <DataGridBody<T>>
                                                {({ item, rowId }) => (
                                                    <DataGridRow<T> key={rowId} className={styles.disabledRow}>
                                                        {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                                    </DataGridRow>
                                                )}
                                            </DataGridBody>
                                        </DataGrid>
                                    </>
                                )}
                            </div>
                        )}
                    </DialogContent>

                    <DialogActions className={styles.dialogActions}>
                        <Button appearance="primary" onClick={handleApply}>
                            {applyButtonText ?? intl.formatMessage(SreAgentResources.apply)}
                        </Button>
                        <Button appearance="secondary" onClick={onDismiss}>
                            {intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
