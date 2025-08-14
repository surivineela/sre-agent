import { IDropdownOption, Pivot, PivotItem, ResponsiveMode, Text, Toggle } from '@fluentui/react';
import { Tooltip } from '@fluentui/react-components';
import { CheckmarkStarburst16Filled } from '@fluentui/react-icons';
import { DefaultButton, PrimaryButton } from '@fluentui/react/lib/Button';
import { ConstrainMode, DetailsListLayoutMode, IColumn } from '@fluentui/react/lib/DetailsList';
import { Dialog, DialogFooter, DialogType, IDialogStyles } from '@fluentui/react/lib/Dialog';
import { Link } from '@fluentui/react/lib/Link';
import isEqual from 'lodash/isEqual';
import { Dispatch, FC, FormEvent, SetStateAction, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { DropdownWithFilter, IDropdownOptionForFilter } from '../../Common/Components/DropdownWithFilterNoFormik';
import { SearchFilterWithResultAnnouncement } from '../../Common/Components/SearchFilterWithResultAnnouncement';
import ShimmeredDetailsListWithSelection, { OnUpdateSelectionArgs } from '../../Common/Components/ShimmeredDetailsListWithSelection';
import { getUserFriendlyLocation } from '../../Common/Helpers/LocationHelper';
import { ManagedResourcesStringResources, ResourcePickerTabResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { useFilteredResourceGroups } from './Hooks/useFilteredResourceGroups';
import { getSubscriptionId, ResourceGroup, useResourceGroups } from './Hooks/useResourceGroups';
import { Subscription } from './Hooks/useSubscriptions';
import PermissionsDetailsList from './PermissionsDetailsList';
import ReviewTab from './ResourcePickerReviewTab';
import { useManagedResourcesStyles } from './Styles/ManagedResources.styles';

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

export type ResourceGroupPickerProps = {
    hideResourceGroupPicker: boolean;
    subscriptionId: string;
    existingResourceGroupIds?: string[];
    onClick: (selectedResourceGroups: ResourceGroup[]) => void;
    setHideResourceGroupPicker: Dispatch<SetStateAction<boolean>>;
    subscriptionOptions: IDropdownOptionForFilter<Subscription>[];
};

export enum TabKeys {
    select = 'select',
    review = 'review',
    assign = 'assign',
}

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

    const subscriptionDropdownOptions = useMemo(() => {
        const selectAllOption = {
            key: 'selectAll',
            text: intl.formatMessage(ManagedResourcesStringResources.selectAll),
            data: {
                id: '',
                tenantId: '',
                uniqueDisplayName: '',
                displayName: '',
                subscriptionId: '',
                state: '',
                subscriptionPolicies: { locationPlacementId: '', quotaId: '' },
                authorizationSource: '',
            },
        };

        return [selectAllOption, ...subscriptionOptions];
    }, [subscriptionOptions, intl]);

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

    const modelProps = {
        isBlocking: false,
        styles: {
            main: {
                display: 'flex',
                flexDirection: 'column',
                width: '850px',
                overflowX: 'hidden',
                height: '675px',
                maxWidth: '850px',
                maxHeight: '90vh',
                overflowY: 'hidden',
            },
        },
        className: styles.dialog,
    };

    const dialogContentProps = {
        type: DialogType.normal,
        title: intl.formatMessage(ManagedResourcesStringResources.selectResourceGroupsToMonitor),
    };

    const calloutProps = {
        styles: {
            root: {
                maxHeight: '675px',
                maxWidth: '850px',
                overflowY: 'hidden',
            },
        },
    };

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
            }
        },
        [portalContext]
    );

    const onRenderName = useCallback(
        (item: ResourceGroupWithSelection) => {
            return (
                <div className={styles.statusRow}>
                    <img src="./ResourceGroup.svg" alt="ResourceGroup" style={{ height: 16, width: 16 }} />
                    <Link style={{ overflow: 'hidden', textOverflow: 'ellipsis', flexShrink: 5 }} onClick={_e => onNameClick(item.id)}>
                        {item.name}
                    </Link>
                    {item.recommended && (
                        <Tooltip
                            content={intl.formatMessage(ResourcePickerTabResources.recommendedResourceGroupTooltip)}
                            relationship="description"
                        >
                            <div style={{ display: 'inline-block', flexShrink: 1 }}>
                                <CheckmarkStarburst16Filled style={{ flexShrink: 1, color: '#0078D4' }} />
                            </div>
                        </Tooltip>
                    )}
                </div>
            );
        },
        [styles.statusRow, intl, onNameClick]
    );

    const onRenderSubscription = useCallback(
        (item: ResourceGroupWithSelection) => {
            const subscriptionId = getSubscriptionId(item.id);
            const subscription = subscriptionOptions.find(subscription => subscription.key === subscriptionId);
            return <Text>{subscription?.text}</Text>;
        },
        [subscriptionOptions]
    );

    const onRenderLocation = useCallback((item: ResourceGroupWithSelection) => {
        return <>{getUserFriendlyLocation(item.location)}</>;
    }, []);

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

    const columns = useMemo<ISortedDetailsListColumn[]>(() => {
        return [
            {
                key: ResourceGroupListColumnKey.name,
                name: intl.formatMessage(ManagedResourcesStringResources.resourceGroupName),
                fieldName: ResourceGroupListColumnKey.name,
                minWidth: 300,
                maxWidth: 300,
                isResizable: true,
                onRender: onRenderName,
            },
            {
                key: ResourceGroupListColumnKey.subscription,
                name: intl.formatMessage(ManagedResourcesStringResources.subscription),
                fieldName: ResourceGroupListColumnKey.subscription,
                minWidth: 250,
                maxWidth: 250,
                isResizable: true,
                onRender: onRenderSubscription,
            },
            {
                key: ResourceGroupListColumnKey.location,
                name: intl.formatMessage(ManagedResourcesStringResources.location),
                fieldName: ResourceGroupListColumnKey.location,
                minWidth: 100,
                maxWidth: 100,
                isResizable: true,
                onRender: onRenderLocation,
            },
        ];
    }, [onRenderName, onRenderLocation, onRenderSubscription, intl]);

    const toggleItemSelection = useCallback((id: string) => {
        setSelectedKeys(currentKeys => {
            const isSelected = currentKeys.includes(id);
            return isSelected ? currentKeys.filter(k => k !== id) : [...currentKeys, id];
        });
    }, []);

    const onSubscriptionChange = useCallback(
        (_event: FormEvent<HTMLDivElement>, option?: IDropdownOptionForFilter<Subscription>): void => {
            if (!option) return;

            if (option.key === 'selectAll') {
                const isSelectingAll = selectedSubscriptionKeys.length < subscriptionDropdownOptions.length;
                setSelectedSubscriptionKeys(isSelectingAll ? subscriptionDropdownOptions.map(item => item.key as string) : []);
            } else {
                const newSelectedKeys = option.selected
                    ? [...selectedSubscriptionKeys, option.key as string]
                    : selectedSubscriptionKeys.filter(k => k !== option.key);
                setSelectedSubscriptionKeys(newSelectedKeys);
            }
        },
        [selectedSubscriptionKeys, subscriptionDropdownOptions]
    );

    const onRenderSubscriptionTitle = useCallback(() => {
        if (selectedSubscriptionKeys.length === subscriptionDropdownOptions.length) {
            return <span>{intl.formatMessage(ManagedResourcesStringResources.allSubscriptions)}</span>;
        }
        const filteredOptions = subscriptionOptions.filter(option => selectedSubscriptionKeys.includes(option.key as string));
        return <span>{filteredOptions?.map((option: IDropdownOptionForFilter<Subscription>) => option.text).join(', ')}</span>;
    }, [selectedSubscriptionKeys, subscriptionOptions, subscriptionDropdownOptions.length, intl]);

    const locationDropdownItems = useMemo(() => {
        const locations =
            resourceGroupsWithSelection?.map(item => {
                return { key: item.location, text: getUserFriendlyLocation(item.location), data: item.location };
            }) ?? [];
        return Array.from(new Map(locations.map(item => [item.key, item])).values()).sort((a, b) =>
            a.text?.localeCompare(b.text, undefined, { sensitivity: 'base' })
        );
    }, [resourceGroupsWithSelection]);

    const onLocationChange = useCallback(
        (_event: FormEvent<HTMLDivElement>, option?: IDropdownOptionForFilter<string>): void => {
            if (!option) return;

            const newSelectedKeys = option.selected
                ? [...selectedLocationKeys, option.key as string]
                : selectedLocationKeys.filter(k => k !== option.key);
            setSelectedLocationKeys(newSelectedKeys);
        },
        [selectedLocationKeys]
    );

    const onRenderLocationTitle = useCallback(() => {
        if (!selectedLocationKeys || selectedLocationKeys.length === 0) {
            return <span>{intl.formatMessage(ManagedResourcesStringResources.allRegions)}</span>;
        }
        const filteredOptions = locationDropdownItems.filter(option => selectedLocationKeys.includes(option.key as string));
        return <span>{filteredOptions?.map((option: IDropdownOptionForFilter<string>) => option.text).join(', ')}</span>;
    }, [selectedLocationKeys, locationDropdownItems, intl]);

    const onTabLinkClick = useCallback((item?: PivotItem) => {
        if (item?.props?.itemKey) {
            setTabKey(item.props.itemKey);
        }
    }, []);

    const dialogStyles: Partial<IDialogStyles> = {
        main: {
            overflowY: 'hidden',
        },
    };

    return (
        <Dialog
            styles={dialogStyles}
            hidden={hideResourceGroupPicker}
            onDismiss={_e => setHideResourceGroupPicker(true)}
            dialogContentProps={dialogContentProps}
            modalProps={modelProps}
            minWidth={850}
            maxWidth={850}
        >
            <Pivot selectedKey={tabKey} onLinkClick={onTabLinkClick}>
                <PivotItem itemKey={TabKeys.select} headerText={intl.formatMessage(ResourcePickerTabResources.selectTabTitle)} alwaysRender>
                    <div className={styles.dialogContent}>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                            <div style={{ paddingTop: '10px' }}>
                                {selectedResourceGroups.length === 1
                                    ? intl.formatMessage(ResourcePickerTabResources.resourceGroupSelected, {
                                          max: 20 - existingResourceGroupIds.length,
                                          count: selectedResourceGroups.length,
                                      })
                                    : intl.formatMessage(ResourcePickerTabResources.resourceGroupsSelected, {
                                          max: 20 - existingResourceGroupIds.length,
                                          count: selectedResourceGroups.length,
                                      })}
                            </div>
                            <div style={{ display: 'flex', gap: '6px' }}>
                                <div>{intl.formatMessage(ResourcePickerTabResources.showRecommended)}</div>
                                <Toggle checked={showRecommended} onChange={(_e, checked) => setShowRecommended(!!checked)} />
                            </div>
                        </div>
                        <div className={styles.pickerRow}>
                            <div className={styles.pickerItem}>
                                <SearchFilterWithResultAnnouncement
                                    id="resource-group-search"
                                    setFilterValue={setFilter}
                                    filter={filter}
                                    gridItemsCount={filteredResourceGroups.length}
                                    placeHolder={intl.formatMessage(ManagedResourcesStringResources.search)}
                                />
                            </div>
                            <div className={styles.pickerItem}>
                                <DropdownWithFilter
                                    multiSelect
                                    selectedKeys={selectedSubscriptionKeys}
                                    id="subscription-search"
                                    options={subscriptionDropdownOptions}
                                    filterFields={['displayName', 'subscriptionId']}
                                    onChange={(event: React.FormEvent<HTMLDivElement>, option?: IDropdownOption) =>
                                        onSubscriptionChange(event, option as IDropdownOptionForFilter<Subscription>)
                                    }
                                    responsiveMode={ResponsiveMode.large}
                                    onRenderTitle={onRenderSubscriptionTitle}
                                    calloutProps={calloutProps}
                                />
                            </div>
                            <div className={styles.pickerItem}>
                                <DropdownWithFilter
                                    multiSelect
                                    selectedKeys={selectedLocationKeys}
                                    id="location-search"
                                    options={locationDropdownItems}
                                    onChange={(event: React.FormEvent<HTMLDivElement>, option?: IDropdownOption) =>
                                        onLocationChange(event, option as IDropdownOptionForFilter<string>)
                                    }
                                    responsiveMode={ResponsiveMode.large}
                                    onRenderTitle={onRenderLocationTitle}
                                    placeholder={intl.formatMessage(ManagedResourcesStringResources.allRegions)}
                                    calloutProps={calloutProps}
                                />
                            </div>
                        </div>
                        <div style={{ minHeight: '405px', maxHeight: '405px', overflowY: 'scroll' }} data-is-scrollable="true">
                            <ShimmeredDetailsListWithSelection<ResourceGroupWithSelection>
                                items={filteredResourceGroups ?? []}
                                getKey={rg => rg.id}
                                columns={columns}
                                selectedKeys={selectedKeys}
                                onUpdateSelection={onUpdateSelection}
                                constrainMode={ConstrainMode.horizontalConstrained}
                                layoutMode={DetailsListLayoutMode.justified}
                                compact={true}
                                enableShimmer={resourceGroupsLoading}
                            />
                        </div>
                    </div>
                </PivotItem>
                <PivotItem itemKey={TabKeys.review} headerText={intl.formatMessage(ResourcePickerTabResources.reviewTabTitle)} alwaysRender>
                    <ReviewTab
                        selectedResourceGroups={selectedResourceGroups}
                        toggleItemSelection={toggleItemSelection}
                        resourceGroupPermissionsError={resourceGroupPermissionsError}
                        setResourceGroupPermissionsError={setResourceGroupPermissionsError}
                        resourceGroupMaxError={resourceGroupMaxError}
                        setResourceGroupMaxError={setResourceGroupMaxError}
                        onRenderSubscription={onRenderSubscription}
                    />
                </PivotItem>
                <PivotItem itemKey={TabKeys.assign} headerText={intl.formatMessage(ResourcePickerTabResources.assignTabTitle)} alwaysRender>
                    <PermissionsDetailsList accessLevel={accessLevel} managedResourceGroups={selectedResourceGroups} />
                </PivotItem>
            </Pivot>
            <div className={styles.dialogFooter}>
                <DialogFooter>
                    {(tabKey == TabKeys.assign || tabKey == TabKeys.review) && (
                        <DefaultButton
                            className={styles.footerButtonDiv}
                            onClick={() => {
                                if (tabKey === TabKeys.review) {
                                    setTabKey(TabKeys.select);
                                } else if (tabKey === TabKeys.assign) {
                                    setTabKey(TabKeys.review);
                                }
                            }}
                            text={intl.formatMessage(ManagedResourcesStringResources.back)}
                        />
                    )}
                    {(tabKey === TabKeys.select || tabKey === TabKeys.review) && (
                        <PrimaryButton
                            className={styles.footerButtonDiv}
                            onClick={() => {
                                if (tabKey === TabKeys.review) {
                                    setTabKey(TabKeys.assign);
                                } else if (tabKey === TabKeys.select) {
                                    setTabKey(TabKeys.review);
                                }
                            }}
                            text={intl.formatMessage(ManagedResourcesStringResources.next)}
                        />
                    )}
                    {tabKey == TabKeys.assign && (
                        <PrimaryButton
                            className={styles.footerButtonDiv}
                            disabled={selectedResourceGroups.length === 0 || resourceGroupMaxError || resourceGroupPermissionsError}
                            onClick={() => {
                                onClick(selectedResourceGroups);
                                setHideResourceGroupPicker(true);
                            }}
                            text={intl.formatMessage(ManagedResourcesStringResources.save)}
                        />
                    )}
                    <DefaultButton
                        onClick={() => {
                            setFilter('');
                            setHideResourceGroupPicker(true);
                        }}
                        text={intl.formatMessage(ManagedResourcesStringResources.cancel)}
                        className={styles.footerButtonDiv}
                    />
                </DialogFooter>
            </div>
        </Dialog>
    );
};

export default ResourceGroupPicker;
