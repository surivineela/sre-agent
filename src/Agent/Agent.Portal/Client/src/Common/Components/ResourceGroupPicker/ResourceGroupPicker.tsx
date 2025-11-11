import {
    Checkbox,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Image,
    Link,
    SearchBox,
    Switch,
    TableCellLayout,
    TableColumnDefinition,
    Text,
    Tooltip,
    createTableColumn,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';
import { TelemetrySource } from '../../Constants/Telemetry';
import { ResourceGroup, Subscription } from '../../Contracts/Arm';
import { getUserFriendlyLocation } from '../../Utilities/Location';
import { openResourceGroupOverviewInNewTab, openSubscriptionOverviewInNewTab } from '../../Utilities/Url';
import { PillFilter } from '../PillFilter/PillFilter';
import { useFilteredResourceGroups } from './Hooks/useFilteredResourceGroups';
import { ResourceGroupWithSelection, useResourceGroupsFromMultipleSubscriptions } from './Hooks/useResourceGroupsFromMultipleSubscriptions';
import { ResourceGroupPickerSkeleton } from './ResourceGroupPickerSkeleton';

export const MAX_RESOURCE_GROUPS = 100;

const useStyles = makeStyles({
    filtersRow: {
        display: 'flex',
        gap: '10px',
        flexWrap: 'wrap',
        alignItems: 'center',
    },
    toggleRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
    gridContainer: {
        minHeight: '420px',
        maxHeight: '420px',
        overflowY: 'auto',
        flex: 1,
    },
    nameCell: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    headerCheckbox: {
        marginTop: '12px',
    },
    checkboxColumn: {
        width: '16px',
        minWidth: '16px',
        maxWidth: '16px',
    },
});

interface ResourceGroupPickerProps {
    subscriptionId: string;
    existingResourceGroupIds?: string[];
    onChangeSelection: (selectedResourceGroups: ResourceGroup[]) => void;
    subscriptionOptions: Array<{ key: string; text: string; data: Subscription }>;
}

export const ResourceGroupPicker: FC<ResourceGroupPickerProps> = ({
    subscriptionId,
    existingResourceGroupIds = [],
    onChangeSelection,
    subscriptionOptions,
}) => {
    const styles = useStyles();
    const intl = useIntl();

    const [searchFilter, setSearchFilter] = useState<string>('');
    const [resourceGroupsWithSelection, setResourceGroupsWithSelection] = useState<ResourceGroupWithSelection[]>([]);
    const [selectedSubscriptionIds, setSelectedSubscriptionIds] = useState<string[]>([subscriptionId]);
    const [selectedLocationKeys, setSelectedLocationKeys] = useState<string[]>([]);
    const [showRecommended, setShowRecommended] = useState<boolean>(false);

    const { resourceGroupsList, resourceGroupsLoading } = useResourceGroupsFromMultipleSubscriptions({
        subscriptionIds: selectedSubscriptionIds,
        telemetrySource: TelemetrySource.SreAgentCreate,
    });

    const { filteredResourceGroupSet } = useFilteredResourceGroups({
        subscriptionIds: selectedSubscriptionIds,
        telemetrySource: TelemetrySource.SreAgentCreate,
    });

    // Initialize selected subscriptions when subscriptionId changes
    useEffect(() => {
        setSelectedSubscriptionIds([subscriptionId]);
    }, [subscriptionId]);

    // Update resourceGroupsWithSelection when resource groups or filtered set changes
    useEffect(() => {
        if (resourceGroupsList) {
            const mapped: ResourceGroupWithSelection[] = resourceGroupsList
                .map((rg: ResourceGroupWithSelection) => ({
                    ...rg,
                    recommended: filteredResourceGroupSet?.has(rg.name) ?? false,
                }))
                .sort((a: ResourceGroupWithSelection, b: ResourceGroupWithSelection) => a.name.localeCompare(b.name));
            setResourceGroupsWithSelection(mapped);
        }
    }, [resourceGroupsList, filteredResourceGroupSet]);

    // Get unique locations for filter
    const locationOptions = useMemo(() => {
        const locations = resourceGroupsWithSelection.map(rg => ({
            key: rg.location,
            text: getUserFriendlyLocation(rg.location),
        }));
        const uniqueMap = new Map(locations.map(item => [item.key, item]));
        return Array.from(uniqueMap.values()).sort((a, b) => a.text.localeCompare(b.text));
    }, [resourceGroupsWithSelection]);

    // Get subscription options with "All" option
    const subscriptionFilterOptions = useMemo(() => {
        return subscriptionOptions.map(sub => ({
            key: sub.key,
            text: sub.text,
        }));
    }, [subscriptionOptions]);

    // Filter resource groups
    const filteredResourceGroups = useMemo(() => {
        let groups = resourceGroupsWithSelection;

        // Filter out existing resource groups
        if (existingResourceGroupIds.length > 0) {
            groups = groups.filter(rg => !existingResourceGroupIds.includes(rg.id));
        }

        // Apply search filter
        if (searchFilter) {
            const lowerFilter = searchFilter.toLowerCase();
            groups = groups.filter(rg => rg.name.toLowerCase().includes(lowerFilter));
        }

        // Apply location filter
        if (selectedLocationKeys.length > 0) {
            groups = groups.filter(rg => selectedLocationKeys.includes(rg.location));
        }

        // Apply recommended filter
        if (showRecommended) {
            groups = groups.filter(rg => rg.recommended);
        }

        return groups;
    }, [resourceGroupsWithSelection, searchFilter, selectedLocationKeys, showRecommended, existingResourceGroupIds]);

    // Selected resource groups
    const selectedResourceGroups = useMemo(() => {
        return resourceGroupsWithSelection.filter(rg => rg.selected);
    }, [resourceGroupsWithSelection]);

    // Notify parent when selection changes
    useEffect(() => {
        onChangeSelection(selectedResourceGroups);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [selectedResourceGroups]);

    // Toggle individual resource group selection
    const toggleItemSelection = useCallback((id: string) => {
        setResourceGroupsWithSelection(prev => prev.map(rg => (rg.id === id ? { ...rg, selected: !rg.selected } : rg)));
    }, []);

    // Check if all filtered items are selected
    const isAllFilteredSelected = useMemo(() => {
        return filteredResourceGroups.length > 0 && filteredResourceGroups.every(rg => rg.selected);
    }, [filteredResourceGroups]);

    const isSomeFilteredSelected = useMemo(() => {
        return filteredResourceGroups.some(rg => rg.selected) && !isAllFilteredSelected;
    }, [filteredResourceGroups, isAllFilteredSelected]);

    // Toggle select all filtered items
    const toggleSelectAll = useCallback(() => {
        const filteredIds = new Set(filteredResourceGroups.map(rg => rg.id));
        const allFilteredSelected = isAllFilteredSelected;

        setResourceGroupsWithSelection(prev =>
            prev.map(rg => {
                if (filteredIds.has(rg.id)) {
                    return { ...rg, selected: !allFilteredSelected };
                }
                return rg;
            })
        );
    }, [filteredResourceGroups, isAllFilteredSelected]);

    // Define columns
    const columns: TableColumnDefinition<ResourceGroupWithSelection>[] = useMemo(
        () => [
            createTableColumn<ResourceGroupWithSelection>({
                columnId: 'selected',
                compare: () => 0,
                renderHeaderCell: () => (
                    <div className={styles.headerCheckbox}>
                        <Checkbox
                            checked={isAllFilteredSelected || isSomeFilteredSelected}
                            onChange={toggleSelectAll}
                            aria-label={intl.formatMessage(PortalResources.selectAll)}
                        />
                    </div>
                ),
                renderCell: item => (
                    <Checkbox checked={item.selected} onChange={() => toggleItemSelection(item.id)} aria-label={`Select ${item.name}`} />
                ),
            }),
            createTableColumn<ResourceGroupWithSelection>({
                columnId: 'name',
                renderHeaderCell: () => intl.formatMessage(PortalResources.resourceGroup),
                renderCell: item => {
                    return (
                        <TableCellLayout>
                            <div className={styles.nameCell}>
                                <Link onClick={() => openResourceGroupOverviewInNewTab(item.id)}>{item.name}</Link>
                                {item.recommended && (
                                    <Tooltip
                                        relationship="description"
                                        content={intl.formatMessage(PortalResources.recommendedResourceGroupTooltip)}
                                    >
                                        <Image src="/VerifiedBrand.svg" height={16} width={16} />
                                    </Tooltip>
                                )}
                            </div>
                        </TableCellLayout>
                    );
                },
            }),
            createTableColumn<ResourceGroupWithSelection>({
                columnId: 'subscription',
                renderHeaderCell: () => intl.formatMessage(PortalResources.subscription),
                renderCell: item => {
                    const subscriptionId = item.id.split('/')[2];
                    const subscription = subscriptionOptions.find(sub => sub.key === subscriptionId);
                    return (
                        <TableCellLayout>
                            <Link onClick={() => openSubscriptionOverviewInNewTab(subscriptionId)}>
                                {subscription?.text || subscriptionId}
                            </Link>
                        </TableCellLayout>
                    );
                },
            }),
            createTableColumn<ResourceGroupWithSelection>({
                columnId: 'location',
                renderHeaderCell: () => intl.formatMessage(PortalResources.region),
                renderCell: item => <TableCellLayout>{getUserFriendlyLocation(item.location)}</TableCellLayout>,
            }),
        ],
        [intl, styles, subscriptionOptions, isAllFilteredSelected, isSomeFilteredSelected, toggleSelectAll, toggleItemSelection]
    );

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <div>
                <Text>{intl.formatMessage(PortalResources.resourceGroupsMax)}</Text>
                <div aria-live="polite" aria-atomic="true">
                    {selectedResourceGroups.length === 1
                        ? intl.formatMessage(PortalResources.resourceGroupSelected, {
                              0: selectedResourceGroups.length,
                          })
                        : intl.formatMessage(PortalResources.resourceGroupsSelected, {
                              0: selectedResourceGroups.length,
                          })}
                </div>
                <div className={styles.toggleRow}>
                    <Text>{intl.formatMessage(PortalResources.showRecommended)}</Text>
                    <Switch
                        checked={showRecommended}
                        onChange={(_, data) => setShowRecommended(data.checked)}
                        aria-label={intl.formatMessage(PortalResources.toggleShowRecommendedAriaLabel)}
                    />
                </div>
            </div>

            <div className={styles.filtersRow}>
                <div>
                    <SearchBox
                        placeholder={intl.formatMessage(PortalResources.searchResourceGroups)}
                        value={searchFilter}
                        onChange={(_, data) => setSearchFilter(data.value)}
                        aria-label={intl.formatMessage(PortalResources.searchResourceGroups)}
                    />
                </div>
                <div>
                    <PillFilter
                        filterType="combobox"
                        label={intl.formatMessage(PortalResources.subscription)}
                        options={subscriptionFilterOptions.map(opt => ({ key: opt.key, label: opt.text }))}
                        onApply={setSelectedSubscriptionIds}
                        selectedKeys={selectedSubscriptionIds}
                        multiSelect
                        addAllOption
                        useInDialog
                    />
                </div>
                <div>
                    <PillFilter
                        filterType="combobox"
                        label={intl.formatMessage(PortalResources.region)}
                        options={locationOptions.map(opt => ({ key: opt.key, label: opt.text }))}
                        onApply={setSelectedLocationKeys}
                        selectedKeys={selectedLocationKeys}
                        multiSelect
                        addAllOption
                        useInDialog
                    />
                </div>
            </div>

            <div className={styles.gridContainer}>
                {resourceGroupsLoading ? (
                    <ResourceGroupPickerSkeleton />
                ) : (
                    <DataGrid
                        items={filteredResourceGroups}
                        columns={columns}
                        sortable
                        resizableColumns
                        focusMode="composite"
                        aria-label={intl.formatMessage(PortalResources.resourceGroupPickerTableAriaLabel)}
                        columnSizingOptions={{
                            selected: {
                                minWidth: 48,
                                defaultWidth: 48,
                            },
                            name: {
                                minWidth: 200,
                                defaultWidth: 250,
                                idealWidth: 300,
                            },
                        }}
                    >
                        <DataGridHeader>
                            <DataGridRow>
                                {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                            </DataGridRow>
                        </DataGridHeader>
                        <DataGridBody<ResourceGroupWithSelection>>
                            {({ item, rowId }) => (
                                <DataGridRow<ResourceGroupWithSelection> key={rowId}>
                                    {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                </DataGridRow>
                            )}
                        </DataGridBody>
                    </DataGrid>
                )}
            </div>
        </div>
    );
};
