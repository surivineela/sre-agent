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
    Spinner,
    TableCellLayout,
    TableColumnDefinition,
    Text,
    createTableColumn,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PillFilter } from '../../../../Common/Components/PillFilter/PillFilter';
import { SreAgentArgItem } from '../../../../Common/Contracts/SreAgent';
import { safeCompare } from '../../../../Common/Utilities/String';
import { openResourceGroupOverviewInNewTab, openSubscriptionOverviewInNewTab } from '../../../../Common/Utilities/Url';
import { PortalResources } from '../../../../Strings/Resources';

export interface AgentPickerGridProps {
    availableAgents: SreAgentArgItem[];
    selectedAgentIds: Set<string>;
    onSelectionChange: (selectedIds: Set<string>) => void;
    isLoading: boolean;
    maxAgents: number;
    currentAgentCount: number;
    subscriptions: Array<{ subscriptionId: string; displayName: string }>;
}

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    description: {
        color: tokens.colorNeutralForeground2,
    },
    filtersRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
        alignItems: 'center',
    },
    gridContainer: {
        minHeight: '350px',
        maxHeight: '350px',
        overflowY: 'auto',
    },
    headerCheckbox: {
        marginTop: '12px',
    },
    nameCell: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    selectionCounter: {
        marginTop: tokens.spacingVerticalS,
        fontWeight: tokens.fontWeightSemibold,
    },
    loadingContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        height: '350px',
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        height: '350px',
        gap: tokens.spacingVerticalS,
    },
});

export const AgentPickerGrid: FC<AgentPickerGridProps> = ({
    availableAgents,
    selectedAgentIds,
    onSelectionChange,
    isLoading,
    maxAgents,
    currentAgentCount,
    subscriptions,
}) => {
    const styles = useStyles();
    const intl = useIntl();

    const [searchFilter, setSearchFilter] = useState<string>('');
    const [selectedSubscriptionIds, setSelectedSubscriptionIds] = useState<string[]>([]);
    const [selectedResourceGroupNames, setSelectedResourceGroupNames] = useState<string[]>([]);

    // Create subscription display name map
    const subscriptionDisplayNameMap = useMemo(() => {
        return new Map(subscriptions.map(sub => [sub.subscriptionId, sub.displayName]));
    }, [subscriptions]);

    // Only show agents that are not already in an agent space
    const eligibleAgents = useMemo(() => {
        return availableAgents.filter(agent => !agent.agentSpaceId);
    }, [availableAgents]);

    // Get unique subscription options for filter
    const subscriptionFilterOptions = useMemo(() => {
        const uniqueSubscriptionIds = [...new Set(eligibleAgents.map(agent => agent.subscriptionId))];
        return uniqueSubscriptionIds
            .map(subId => ({
                key: subId,
                label: subscriptionDisplayNameMap.get(subId) || subId,
            }))
            .sort((a, b) => a.label.localeCompare(b.label));
    }, [eligibleAgents, subscriptionDisplayNameMap]);

    // Get unique resource group options for filter
    const resourceGroupFilterOptions = useMemo(() => {
        const uniqueResourceGroups = [...new Set(eligibleAgents.map(agent => agent.resourceGroup))];
        return uniqueResourceGroups
            .map(rg => ({
                key: rg,
                label: rg,
            }))
            .sort((a, b) => a.label.localeCompare(b.label));
    }, [eligibleAgents]);

    // Filter agents based on search and filter selections
    const filteredAgents = useMemo(() => {
        let agents = eligibleAgents;

        // Apply search filter
        if (searchFilter) {
            const lowerFilter = searchFilter.toLowerCase();
            agents = agents.filter(agent => agent.name.toLowerCase().includes(lowerFilter));
        }

        // Apply subscription filter
        if (selectedSubscriptionIds.length > 0) {
            agents = agents.filter(agent => selectedSubscriptionIds.includes(agent.subscriptionId));
        }

        // Apply resource group filter
        if (selectedResourceGroupNames.length > 0) {
            agents = agents.filter(agent => selectedResourceGroupNames.includes(agent.resourceGroup));
        }

        return agents;
    }, [eligibleAgents, searchFilter, selectedSubscriptionIds, selectedResourceGroupNames]);

    // Toggle individual agent selection
    const toggleItemSelection = useCallback(
        (id: string) => {
            const newSelection = new Set(selectedAgentIds);
            if (newSelection.has(id)) {
                newSelection.delete(id);
            } else {
                // Check if we've reached the maximum
                const remainingSlots = maxAgents - currentAgentCount;
                if (newSelection.size < remainingSlots) {
                    newSelection.add(id);
                }
            }
            onSelectionChange(newSelection);
        },
        [selectedAgentIds, onSelectionChange, maxAgents, currentAgentCount]
    );

    // Check if all filtered items are selected
    const isAllFilteredSelected = useMemo(() => {
        return filteredAgents.length > 0 && filteredAgents.every(agent => selectedAgentIds.has(agent.id));
    }, [filteredAgents, selectedAgentIds]);

    const isSomeFilteredSelected = useMemo(() => {
        return filteredAgents.some(agent => selectedAgentIds.has(agent.id)) && !isAllFilteredSelected;
    }, [filteredAgents, selectedAgentIds, isAllFilteredSelected]);

    // Toggle select all filtered items
    const toggleSelectAll = useCallback(() => {
        const newSelection = new Set(selectedAgentIds);
        const filteredIds = filteredAgents.map(agent => agent.id);

        if (isAllFilteredSelected) {
            // Deselect all filtered agents
            filteredIds.forEach(id => newSelection.delete(id));
        } else {
            // Select all filtered agents (up to max)
            const remainingSlots = maxAgents - currentAgentCount;
            for (const id of filteredIds) {
                if (newSelection.size >= remainingSlots) break;
                newSelection.add(id);
            }
        }
        onSelectionChange(newSelection);
    }, [selectedAgentIds, filteredAgents, isAllFilteredSelected, onSelectionChange, maxAgents, currentAgentCount]);

    // Define columns
    const columns: TableColumnDefinition<SreAgentArgItem>[] = useMemo(
        () => [
            createTableColumn<SreAgentArgItem>({
                columnId: 'selected',
                compare: () => 0,
                renderHeaderCell: () => (
                    <div className={styles.headerCheckbox}>
                        <Checkbox
                            checked={isAllFilteredSelected ? true : isSomeFilteredSelected ? 'mixed' : false}
                            onChange={toggleSelectAll}
                            aria-label={intl.formatMessage(PortalResources.selectAllAgents)}
                        />
                    </div>
                ),
                renderCell: item => (
                    <Checkbox
                        checked={selectedAgentIds.has(item.id)}
                        onChange={() => toggleItemSelection(item.id)}
                        aria-label={intl.formatMessage(PortalResources.selectAgent, { name: item.name })}
                    />
                ),
            }),
            createTableColumn<SreAgentArgItem>({
                columnId: 'name',
                compare: (a, b) => safeCompare(a.name, b.name),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.name)}</Text>,
                renderCell: item => (
                    <TableCellLayout
                        media={<Image src="/SreAgent.svg" width={16} height={16} alt={intl.formatMessage(PortalResources.agent)} />}
                    >
                        {item.name}
                    </TableCellLayout>
                ),
            }),
            createTableColumn<SreAgentArgItem>({
                columnId: 'subscription',
                compare: (a, b) => safeCompare(a.subscriptionId, b.subscriptionId),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.subscription)}</Text>,
                renderCell: item => (
                    <TableCellLayout>
                        <Link onClick={() => openSubscriptionOverviewInNewTab(item.subscriptionId)}>
                            {subscriptionDisplayNameMap.get(item.subscriptionId) || item.subscriptionId}
                        </Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<SreAgentArgItem>({
                columnId: 'resourceGroup',
                compare: (a, b) => safeCompare(a.resourceGroup, b.resourceGroup),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.resourceGroupColumn)}</Text>,
                renderCell: item => (
                    <TableCellLayout>
                        <Link onClick={() => openResourceGroupOverviewInNewTab(item.id)}>{item.resourceGroup}</Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<SreAgentArgItem>({
                columnId: 'powerState',
                compare: (a, b) => safeCompare(a.powerState, b.powerState),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.powerState)}</Text>,
                renderCell: item => (
                    <TableCellLayout>
                        {item.powerState === 'Running'
                            ? intl.formatMessage(PortalResources.running)
                            : item.powerState === 'Stopped'
                              ? intl.formatMessage(PortalResources.stopped)
                              : '-'}
                    </TableCellLayout>
                ),
            }),
        ],
        [
            intl,
            styles.headerCheckbox,
            selectedAgentIds,
            subscriptionDisplayNameMap,
            isAllFilteredSelected,
            isSomeFilteredSelected,
            toggleSelectAll,
            toggleItemSelection,
        ]
    );

    return (
        <div className={styles.container}>
            <Text className={styles.description}>
                {intl.formatMessage(PortalResources.agentPickerDescription, { max: maxAgents, remaining: maxAgents - currentAgentCount })}
            </Text>

            <div className={styles.filtersRow}>
                <SearchBox
                    placeholder={intl.formatMessage(PortalResources.searchAgents)}
                    value={searchFilter}
                    onChange={(_, data) => setSearchFilter(data.value)}
                    aria-label={intl.formatMessage(PortalResources.searchAgents)}
                />
                <PillFilter
                    filterType="combobox"
                    label={intl.formatMessage(PortalResources.subscription)}
                    options={subscriptionFilterOptions}
                    onApply={setSelectedSubscriptionIds}
                    selectedKeys={selectedSubscriptionIds}
                    multiSelect
                    addAllOption
                    useInDialog
                />
                <PillFilter
                    filterType="combobox"
                    label={intl.formatMessage(PortalResources.resourceGroup)}
                    options={resourceGroupFilterOptions}
                    onApply={setSelectedResourceGroupNames}
                    selectedKeys={selectedResourceGroupNames}
                    multiSelect
                    addAllOption
                    useInDialog
                />
            </div>

            <div className={styles.gridContainer}>
                {isLoading ? (
                    <div className={styles.loadingContainer}>
                        <Spinner size="medium" />
                    </div>
                ) : filteredAgents.length === 0 ? (
                    <div className={styles.emptyState}>
                        <Text weight="semibold">{intl.formatMessage(PortalResources.noAvailableAgents)}</Text>
                        <Text>{intl.formatMessage(PortalResources.noAvailableAgentsDescription)}</Text>
                    </div>
                ) : (
                    <DataGrid
                        items={filteredAgents}
                        columns={columns}
                        sortable
                        resizableColumns
                        focusMode="composite"
                        aria-label={intl.formatMessage(PortalResources.agents)}
                        columnSizingOptions={{
                            selected: {
                                minWidth: 48,
                                defaultWidth: 48,
                            },
                            name: {
                                minWidth: 150,
                                defaultWidth: 180,
                            },
                            subscription: {
                                minWidth: 150,
                                defaultWidth: 180,
                            },
                            resourceGroup: {
                                minWidth: 120,
                                defaultWidth: 150,
                            },
                            agentSpace: {
                                minWidth: 80,
                                defaultWidth: 100,
                            },
                            location: {
                                minWidth: 100,
                                defaultWidth: 120,
                            },
                            powerState: {
                                minWidth: 80,
                                defaultWidth: 100,
                            },
                        }}
                    >
                        <DataGridHeader>
                            <DataGridRow>
                                {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                            </DataGridRow>
                        </DataGridHeader>
                        <DataGridBody<SreAgentArgItem>>
                            {({ item, rowId }) => (
                                <DataGridRow<SreAgentArgItem> key={rowId}>
                                    {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                </DataGridRow>
                            )}
                        </DataGridBody>
                    </DataGrid>
                )}
            </div>

            <Text className={styles.selectionCounter} aria-live="polite" aria-atomic="true">
                {intl.formatMessage(PortalResources.nSelected, { count: selectedAgentIds.size })}
            </Text>
        </div>
    );
};
