import { Text, Toggle } from '@fluentui/react';
import {
    Button,
    createTableColumn,
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
    makeStyles,
    SearchBox,
    SelectTabData,
    SelectTabEvent,
    Tab,
    TableCellLayout,
    TableColumnDefinition,
    TabList,
    tokens,
    Tooltip,
} from '@fluentui/react-components';
import { CheckmarkStarburst16Filled } from '@fluentui/react-icons';

import { IColumn } from '@fluentui/react/lib/DetailsList';
import { Link } from '@fluentui/react/lib/Link';
import isEqual from 'lodash/isEqual';
import { Dispatch, FC, SetStateAction, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { PillFilter } from '../../Common/Components/PillFilter/PillFilter';
import { OnUpdateSelectionArgs } from '../../Common/Components/ShimmeredDetailsListWithSelection';
import { getUserFriendlyLocation } from '../../Common/Helpers/LocationHelper';
import { ManagedResourcesStringResources, ResourcePickerTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { useFilteredResourceGroups } from './Hooks/useFilteredResourceGroups';
import { getSubscriptionId, ResourceGroup, useResourceGroups } from './Hooks/useResourceGroups';
import { Subscription } from './Hooks/useSubscriptions';
import PermissionsDetailsList from './PermissionsDetailsList';
import { ResourceGroupPickerSkeleton } from './ResourceGroupPickerSkeleton';
import ReviewTab from './ResourcePickerReviewTab';
import { useManagedResourcesStyles } from './Styles/ManagedResources.styles';

const useLocalStyles = makeStyles({
    // Global override for popovers within dialogs
    ':global(.fui-PopoverSurface)': {
        zIndex: '1000001 !important',
    },
    dialogSurface: {
        width: '850px',
        height: '80vh',
        maxHeight: '650px',
        maxWidth: '90vw',
        display: 'flex',
        flexDirection: 'column',
    },
    dialogBodyContainer: {
        display: 'flex',
        flexDirection: 'column',
        height: 'calc(100% + 10px)',
        gap: '0px',
    },
    dialogContentContainer: {
        flex: '1',
        overflowX: 'visible',
        overflowY: 'hidden',
        minHeight: '0',
        display: 'flex',
        flexDirection: 'column',
        marginBottom: '5px',
    },
    tabList: {
        marginBottom: '10px',
        marginLeft: '-12px',
    },
    tabContent: {
        display: 'flex',
        flexDirection: 'column',
        flex: '1',
        width: '100%',
        overflowY: 'hidden',
        overflowX: 'visible',
    },
    filtersRow: {
        display: 'flex',
        width: '100%',
        flexDirection: 'row',
        gap: '8px',
        paddingTop: '5px',
        flexShrink: '0',
        marginBottom: '10px',
    },
    filterItem: {
        flexShrink: '0',
    },
    fixedSection: {
        flexShrink: '0',
        marginBottom: '10px',
    },
    filterRow: {
        flexShrink: '0',
        marginBottom: '10px',
    },
    filterGap: {
        display: 'flex',
        gap: '6px',
    },
    tableContainer: {
        flex: '1',
        display: 'flex',
        flexDirection: 'column',
        minHeight: '0',
        overflow: 'hidden',
    },
    tableScrollableArea: {
        flex: '1',
        overflowY: 'auto',
        overflowX: 'auto',
        minHeight: '0',
    },
    dataGrid: {
        width: '100%',
        tableLayout: 'auto',
        paddingTop: '5px',
    },
    dataGridHeader: {
        fontWeight: '600',
        position: 'sticky',
        top: '0',
        backgroundColor: tokens.colorNeutralBackground1,
        zIndex: '1',
    },
    dataGridRow: {
        minHeight: '20px',
    },
    searchContainer: {
        marginLeft: '0px !important',
    },
    dialogActions: {
        paddingTop: '5px',
        flexShrink: '0',
        justifyContent: 'flex-end',
        display: 'flex',
    },
    resourceGroupIcon: {
        height: '16px',
        width: '16px',
    },
    linkText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flexShrink: '5',
    },
    recommendedIconContainer: {
        display: 'inline-block',
        flexShrink: '1',
    },
    recommendedIcon: {
        flexShrink: '1',
        color: '#0078D4',
    },
});

export type ISortedDetailsListColumn = IColumn & {
    sort?: (items: any[], isSortedDescending: boolean) => any[];
    disableColumnClick?: boolean;
};

enum ResourceGroupListColumnKey {
    selected = 'selected',
    name = 'name',
    location = 'location',
    subscription = 'subscription',
}

export interface ResourceGroupWithSelection extends ResourceGroup {
    selected: boolean;
    recommended: boolean;
}

export interface SubscriptionOption {
    key: string;
    text: string;
    data: Subscription;
}

export type ResourceGroupPickerProps = {
    hideResourceGroupPicker: boolean;
    subscriptionId: string;
    existingResourceGroupIds?: string[];
    onClick: (selectedResourceGroups: ResourceGroup[]) => void;
    setHideResourceGroupPicker: Dispatch<SetStateAction<boolean>>;
    subscriptionOptions: SubscriptionOption[];
};

export enum TabKeys {
    select = 'select',
    review = 'review',
    assign = 'assign',
}

const RESOURCE_GROUP_LIMIT = 100;

const ResourceGroupPicker: FC<ResourceGroupPickerProps> = (props: ResourceGroupPickerProps) => {
    const {
        subscriptionId,
        hideResourceGroupPicker,
        existingResourceGroupIds = [],
        onClick,
        setHideResourceGroupPicker,
        subscriptionOptions,
    } = props;
    const styles = useManagedResourcesStyles();
    const localStyles = useLocalStyles();

    const [filter, setFilter] = useState<string>('');
    const [resourceGroupsWithSelection, setResourceGroupsWithSelection] = useState<ResourceGroupWithSelection[]>();
    const [resourceGroupMaxError, setResourceGroupMaxError] = useState<boolean>(false);
    const [resourceGroupPermissionsError, setResourceGroupPermissionsError] = useState<boolean>(false);
    const [selectedSubscriptionKeys, setSelectedSubscriptionKeys] = useState<string[]>([]);
    const [selectedLocationKeys, setSelectedLocationKeys] = useState<string[]>([]);
    const [selectedKeys, setSelectedKeys] = useState<string[]>([]);
    const [tabKey, setTabKey] = useState<string>(TabKeys.select);
    const [showRecommended, setShowRecommended] = useState<boolean>(false);

    const portalContext = useContext(AzPortalContext);
    const { agent } = useContext(SreAgentContext);
    const { accessLevel } = agent;
    const intl = useIntl();

    const setTabKeyWrapper = useCallback(
        (key: TabKeys) => {
            setTabKey(key);
            portalContext.logAmplitudeNavigationEvent({
                targetType: 'tab',
                targetAction: 'tabItem',
                targetName: `rscGrpPickerTab${key}`,
                targetFriendlyName: `Resource group picker tab ${key}`,
            });
        },
        [portalContext]
    );

    const onUpdateSelection = useCallback(({ selectedKeys }: OnUpdateSelectionArgs<ResourceGroupWithSelection>) => {
        setSelectedKeys(selectedKeys);
        setResourceGroupsWithSelection(currentGroups => {
            if (!currentGroups) return currentGroups;
            return currentGroups.map(item => ({
                ...item,
                selected: selectedKeys.includes(item.id),
            }));
        });
    }, []);

    const subscriptionIds = useMemo(() => {
        return selectedSubscriptionKeys.filter(k => k !== 'selectAll') ?? [];
    }, [selectedSubscriptionKeys]);

    const { resourceGroupsList, resourceGroupsLoading } = useResourceGroups(subscriptionIds, portalContext);
    const { filteredResourceGroupSet } = useFilteredResourceGroups(portalContext, subscriptionIds);

    useEffect(() => {
        setSelectedSubscriptionKeys([subscriptionId]);
    }, [subscriptionId]);

    useEffect(() => {
        if (resourceGroupsList) {
            setResourceGroupsWithSelection(currentResourceGroupsWithSelection => {
                const newResourceGroupsWithSelection: ResourceGroupWithSelection[] = resourceGroupsList
                    ?.map(item => ({
                        ...item,
                        selected: selectedKeys.includes(item.id),
                        recommended: filteredResourceGroupSet?.has(item.name) ?? false,
                    }))
                    .sort((lhs, rhs) => lhs.name?.localeCompare(rhs.name));
                if (!isEqual(currentResourceGroupsWithSelection, newResourceGroupsWithSelection)) {
                    return newResourceGroupsWithSelection;
                }
                return currentResourceGroupsWithSelection;
            });
        }
    }, [filteredResourceGroupSet, resourceGroupsList, selectedKeys]);

    const dialogTitle = intl.formatMessage(ManagedResourcesStringResources.selectResourceGroupsToMonitor);

    const onNameClick = useCallback(
        (id: string) => {
            if (id) {
                portalContext.openBlade({
                    extension: 'HubsExtension',
                    detailBlade: 'ResourceGroupOverview',
                    detailBladeInputs: {
                        id,
                    },
                });

                portalContext.logAmplitudeControlEvent({
                    targetType: 'link',
                    targetAction: 'clicked',
                    targetName: 'resourceGroupLink',
                    targetFriendlyName: 'Resource group link',
                    valueObjectName: SpecialControlValue.CustomerSuppliedData,
                    valueObjectFriendlyName: SpecialControlValue.CustomerSuppliedData,
                });
            }
        },
        [portalContext]
    );

    const onRenderSubscription = useCallback(
        (item: ResourceGroupWithSelection) => {
            const subscriptionId = getSubscriptionId(item.id);
            const subscription = subscriptionOptions.find(subscription => subscription.key === subscriptionId);
            return <Text>{subscription?.text}</Text>;
        },
        [subscriptionOptions]
    );

    const filteredResourceGroups = useMemo(() => {
        let groups = resourceGroupsWithSelection ?? [];
        groups = groups.filter(item => item && Object.keys(item).length > 0);
        if (existingResourceGroupIds.length > 0) {
            groups = groups.filter(rg => !existingResourceGroupIds.includes(rg.id));
        }
        if (filter) {
            const lowerFilter = filter.toLocaleLowerCase();
            groups = groups.filter(rg => rg.name.toLocaleLowerCase().includes(lowerFilter));
        }
        if (selectedLocationKeys.length > 0) {
            groups = groups.filter(rg => selectedLocationKeys.includes(rg.location));
        }
        if (showRecommended) {
            groups = groups.filter(rg => rg.recommended);
        }
        return groups;
    }, [resourceGroupsWithSelection, existingResourceGroupIds, filter, selectedLocationKeys, showRecommended]);

    const selectedResourceGroups = useMemo(() => {
        return resourceGroupsWithSelection?.filter(item => selectedKeys.includes(item.id)) ?? [];
    }, [resourceGroupsWithSelection, selectedKeys]);

    const columns = useMemo<TableColumnDefinition<ResourceGroupWithSelection>[]>(() => {
        return [
            createTableColumn<ResourceGroupWithSelection>({
                columnId: ResourceGroupListColumnKey.name,
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(ManagedResourcesStringResources.resourceGroupName)}</span>
                ),
                renderCell: item => (
                    <TableCellLayout truncate>
                        <div className={styles.statusRow}>
                            <img src="./ResourceGroup.svg" alt="ResourceGroup" className={localStyles.resourceGroupIcon} />
                            <Link className={localStyles.linkText} onClick={_e => onNameClick(item.id)}>
                                {item.name}
                            </Link>
                            {item.recommended && (
                                <Tooltip
                                    content={intl.formatMessage(ResourcePickerTabResources.recommendedResourceGroupTooltip)}
                                    relationship="description"
                                >
                                    <div className={localStyles.recommendedIconContainer}>
                                        <CheckmarkStarburst16Filled className={localStyles.recommendedIcon} />
                                    </div>
                                </Tooltip>
                            )}
                        </div>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<ResourceGroupWithSelection>({
                columnId: ResourceGroupListColumnKey.subscription,
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(ManagedResourcesStringResources.subscription)}</span>
                ),
                renderCell: item => {
                    const subscriptionId = getSubscriptionId(item.id);
                    const subscription = subscriptionOptions.find(subscription => subscription.key === subscriptionId);
                    return <TableCellLayout>{subscription?.text}</TableCellLayout>;
                },
            }),
            createTableColumn<ResourceGroupWithSelection>({
                columnId: ResourceGroupListColumnKey.location,
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(ManagedResourcesStringResources.location)}</span>
                ),
                renderCell: item => <TableCellLayout>{getUserFriendlyLocation(item.location)}</TableCellLayout>,
            }),
        ];
    }, [intl, styles.statusRow, localStyles, onNameClick, subscriptionOptions]);

    const columnSizingOptions = useMemo(
        () => ({
            [ResourceGroupListColumnKey.name]: {
                minWidth: 200,
                idealWidth: 300,
            },
            [ResourceGroupListColumnKey.subscription]: {
                minWidth: 150,
                idealWidth: 250,
            },
            [ResourceGroupListColumnKey.location]: {
                minWidth: 100,
                idealWidth: 150,
            },
        }),
        []
    );

    const toggleItemSelection = useCallback((id: string) => {
        setSelectedKeys(currentKeys => {
            const isSelected = currentKeys.includes(id);
            return isSelected ? currentKeys.filter(k => k !== id) : [...currentKeys, id];
        });
    }, []);

    const subscriptionPillOptions = useMemo(() => {
        return subscriptionOptions.map(option => ({
            key: option.key,
            label: option.text,
        }));
    }, [subscriptionOptions]);

    const locationPillOptions = useMemo(() => {
        const locations =
            resourceGroupsWithSelection?.map(item => {
                return { key: item.location, label: getUserFriendlyLocation(item.location) };
            }) ?? [];
        return Array.from(new Map(locations.map(item => [item.key, item])).values()).sort((a, b) =>
            a.label?.localeCompare(b.label, undefined, { sensitivity: 'base' })
        );
    }, [resourceGroupsWithSelection]);

    const onTabSelect = useCallback(
        (_event: SelectTabEvent, data: SelectTabData) => {
            setTabKeyWrapper(data.value as TabKeys);
        },
        [setTabKeyWrapper]
    );

    return (
        <Dialog open={!hideResourceGroupPicker} onOpenChange={(_event, data) => setHideResourceGroupPicker(!data.open)} modalType="modal">
            <DialogSurface className={localStyles.dialogSurface} style={{ isolation: 'auto' }}>
                <DialogBody className={localStyles.dialogBodyContainer}>
                    <DialogTitle>{dialogTitle}</DialogTitle>
                    <DialogContent className={localStyles.dialogContentContainer}>
                        <TabList selectedValue={tabKey} onTabSelect={onTabSelect} className={localStyles.tabList}>
                            <Tab value={TabKeys.select}>{intl.formatMessage(ResourcePickerTabResources.selectTabTitle)}</Tab>
                            <Tab value={TabKeys.review}>{intl.formatMessage(ResourcePickerTabResources.reviewTabTitle)}</Tab>
                            <Tab value={TabKeys.assign}>{intl.formatMessage(ResourcePickerTabResources.assignTabTitle)}</Tab>
                        </TabList>
                        {tabKey === TabKeys.select && (
                            <div className={localStyles.tabContent}>
                                <div className={localStyles.fixedSection}>
                                    <div className={localStyles.filterRow}>
                                        {selectedResourceGroups.length === 1
                                            ? intl.formatMessage(ResourcePickerTabResources.resourceGroupSelected, {
                                                  max: RESOURCE_GROUP_LIMIT - existingResourceGroupIds.length,
                                                  count: selectedResourceGroups.length,
                                              })
                                            : intl.formatMessage(ResourcePickerTabResources.resourceGroupsSelected, {
                                                  max: RESOURCE_GROUP_LIMIT - existingResourceGroupIds.length,
                                                  count: selectedResourceGroups.length,
                                              })}
                                    </div>
                                    <div className={localStyles.filterGap}>
                                        <div>{intl.formatMessage(ResourcePickerTabResources.showRecommended)}</div>
                                        <Toggle
                                            checked={showRecommended}
                                            onChange={(_e, checked) => {
                                                setShowRecommended(!!checked);
                                                portalContext.logAmplitudeControlEvent({
                                                    targetType: 'toggle',
                                                    targetAction: 'changed',
                                                    targetName: 'showOnlyRecommendedRscGrpsToggle',
                                                    targetFriendlyName: 'Show only recommended resource groups toggle',
                                                    valueObjectName: checked ? 'checked' : 'unchecked',
                                                    valueObjectFriendlyName: checked ? 'Checked' : 'Unchecked',
                                                });
                                            }}
                                        />
                                    </div>
                                </div>
                                <div className={localStyles.filtersRow}>
                                    <div className={localStyles.filterItem}>
                                        <SearchBox
                                            id="resource-group-search"
                                            value={filter}
                                            onChange={(_event, data) => setFilter(data.value)}
                                            placeholder={intl.formatMessage(ManagedResourcesStringResources.search)}
                                        />
                                    </div>
                                    <div className={localStyles.filterItem}>
                                        <PillFilter
                                            filterType="combobox"
                                            label={intl.formatMessage(ManagedResourcesStringResources.subscription)}
                                            labelDelimiter={intl.formatMessage(SreAgentResources.equals)}
                                            options={subscriptionPillOptions}
                                            onApply={setSelectedSubscriptionKeys}
                                            selectedKeys={selectedSubscriptionKeys}
                                            multiSelect={true}
                                            addAllOption={true}
                                            showValueAs="count"
                                            useInDialog={true}
                                        />
                                    </div>
                                    <div className={localStyles.filterItem}>
                                        <PillFilter
                                            filterType="combobox"
                                            label={intl.formatMessage(ManagedResourcesStringResources.location)}
                                            labelDelimiter={intl.formatMessage(SreAgentResources.equals)}
                                            options={locationPillOptions}
                                            onApply={setSelectedLocationKeys}
                                            selectedKeys={selectedLocationKeys}
                                            multiSelect={true}
                                            addAllOption={true}
                                            showValueAs="count"
                                            useInDialog={true}
                                        />
                                    </div>
                                </div>
                                <div className={localStyles.tableContainer}>
                                    <div className={localStyles.tableScrollableArea} data-is-scrollable="true">
                                        {resourceGroupsLoading ? (
                                            <ResourceGroupPickerSkeleton />
                                        ) : (
                                            <DataGrid
                                                items={filteredResourceGroups ?? []}
                                                columns={columns}
                                                size="small"
                                                sortable
                                                selectionMode="multiselect"
                                                selectedItems={new Set(selectedKeys)}
                                                onSelectionChange={(_, data) => {
                                                    const newSelectedKeys = Array.from(data.selectedItems).map(String);
                                                    const selectedItems =
                                                        filteredResourceGroups?.filter(rg => newSelectedKeys.includes(rg.id)) || [];
                                                    onUpdateSelection({ selectedItems, selectedKeys: newSelectedKeys });
                                                }}
                                                getRowId={item => item.id}
                                                resizableColumns
                                                columnSizingOptions={columnSizingOptions}
                                                className={localStyles.dataGrid}
                                            >
                                                <DataGridHeader className={localStyles.dataGridHeader}>
                                                    <DataGridRow>
                                                        {({ renderHeaderCell }) => (
                                                            <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>
                                                        )}
                                                    </DataGridRow>
                                                </DataGridHeader>
                                                <DataGridBody<ResourceGroupWithSelection>>
                                                    {({ item, rowId }) => (
                                                        <DataGridRow<ResourceGroupWithSelection>
                                                            key={rowId}
                                                            className={localStyles.dataGridRow}
                                                        >
                                                            {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                                        </DataGridRow>
                                                    )}
                                                </DataGridBody>
                                            </DataGrid>
                                        )}
                                    </div>
                                </div>
                            </div>
                        )}
                        {tabKey === TabKeys.review && (
                            <ReviewTab
                                selectedResourceGroups={selectedResourceGroups}
                                toggleItemSelection={toggleItemSelection}
                                resourceGroupPermissionsError={resourceGroupPermissionsError}
                                setResourceGroupPermissionsError={setResourceGroupPermissionsError}
                                resourceGroupMaxError={resourceGroupMaxError}
                                setResourceGroupMaxError={setResourceGroupMaxError}
                                onRenderSubscription={onRenderSubscription}
                            />
                        )}
                        {tabKey === TabKeys.assign && (
                            <PermissionsDetailsList accessLevel={accessLevel} managedResourceGroups={selectedResourceGroups} />
                        )}
                    </DialogContent>
                    <DialogActions className={localStyles.dialogActions}>
                        {(tabKey == TabKeys.assign || tabKey == TabKeys.review) && (
                            <Button
                                appearance="secondary"
                                onClick={() => {
                                    if (tabKey === TabKeys.review) {
                                        setTabKeyWrapper(TabKeys.select);
                                    } else if (tabKey === TabKeys.assign) {
                                        setTabKeyWrapper(TabKeys.review);
                                    }
                                }}
                            >
                                {intl.formatMessage(ManagedResourcesStringResources.back)}
                            </Button>
                        )}
                        {(tabKey === TabKeys.select || tabKey === TabKeys.review) && (
                            <Button
                                appearance="primary"
                                onClick={() => {
                                    if (tabKey === TabKeys.review) {
                                        setTabKeyWrapper(TabKeys.assign);
                                    } else if (tabKey === TabKeys.select) {
                                        setTabKeyWrapper(TabKeys.review);
                                    }
                                }}
                            >
                                {intl.formatMessage(ManagedResourcesStringResources.next)}
                            </Button>
                        )}
                        {tabKey == TabKeys.assign && (
                            <Button
                                appearance="primary"
                                disabled={selectedResourceGroups.length === 0 || resourceGroupMaxError || resourceGroupPermissionsError}
                                onClick={() => {
                                    onClick(selectedResourceGroups);
                                    setHideResourceGroupPicker(true);
                                }}
                            >
                                {intl.formatMessage(ManagedResourcesStringResources.save)}
                            </Button>
                        )}
                        <Button
                            appearance="secondary"
                            onClick={() => {
                                setFilter('');
                                setHideResourceGroupPicker(true);
                            }}
                        >
                            {intl.formatMessage(ManagedResourcesStringResources.cancel)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

export default ResourceGroupPicker;
