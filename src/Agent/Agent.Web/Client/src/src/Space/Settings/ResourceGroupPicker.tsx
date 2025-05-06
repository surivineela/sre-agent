import { Checkbox, IDropdownOption, ResponsiveMode, Text } from '@fluentui/react';
import { DefaultButton, PrimaryButton } from '@fluentui/react/lib/Button';
import { CheckboxVisibility, ConstrainMode, DetailsListLayoutMode, IColumn } from '@fluentui/react/lib/DetailsList';
import { Dialog, DialogFooter, DialogType } from '@fluentui/react/lib/Dialog';
import { Link } from '@fluentui/react/lib/Link';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import isEqual from 'lodash/isEqual';
import { Dispatch, FC, FormEvent, SetStateAction, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { DropdownWithFilter, IDropdownOptionForFilter } from '../../Common/Components/DropdownWithFilterNoFormik';
import { SearchFilterWithResultAnnouncement } from '../../Common/Components/SearchFilterWithResultAnnouncement';
import { getUserFriendlyLocation } from '../../Common/Helpers/LocationHelper';
import { ManagedResourcesStringResources } from '../../Strings/SREAgentResources';
import { getSubscriptionId, ResourceGroup, useResourceGroups } from './Hooks/useResourceGroups';
import { Subscription } from './Hooks/useSubscriptions';
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
}

export type ResourceGroupPickerProps = {
    hideResourceGroupPicker: boolean;
    subscriptionId: string;
    existingResourceGroupIds?: string[];
    onClick: (selectedResourceGroups: ResourceGroup[]) => void;
    setHideResourceGroupPicker: Dispatch<SetStateAction<boolean>>;
    subscriptionOptions: IDropdownOptionForFilter<Subscription>[];
};

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
    const [selectedSubscriptionKeys, setSelectedSubscriptionKeys] = useState<string[]>([]);
    const [selectedLocationKeys, setSelectedLocationKeys] = useState<string[]>([]);
    const [selectedMap, setSelectedMap] = useState<Map<string, boolean>>(new Map<string, boolean>());

    const portalContext = useContext(AzPortalContext);
    const intl = useIntl();

    useEffect(() => {
        setSelectedMap(currentSelected => {
            const newSelected = new Map<string, boolean>();
            resourceGroupsWithSelection?.forEach(item => {
                newSelected.set(item.id, item.selected);
            });
            if (!isEqual(currentSelected, newSelected)) {
                return newSelected;
            }
            return currentSelected;
        });
    }, [resourceGroupsWithSelection]);

    const subscriptionIds = useMemo(() => {
        return selectedSubscriptionKeys.filter(k => k !== 'selectAll') ?? [];
    }, [selectedSubscriptionKeys]);

    const { resourceGroupsList, resourceGroupsLoading } = useResourceGroups(subscriptionIds, portalContext);

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
                        selected: selectedMap.get(item.id) === true,
                    }))
                    .sort((lhs, rhs) => lhs.name?.localeCompare(rhs.name));
                if (!isEqual(currentResourceGroupsWithSelection, newResourceGroupsWithSelection)) {
                    return newResourceGroupsWithSelection;
                }
                return currentResourceGroupsWithSelection;
            });
        }
    }, [resourceGroupsList, selectedMap]);

    const modelProps = {
        isBlocking: false,
        styles: {
            main: {
                display: 'flex',
                flexDirection: 'column',
                width: '800px',
                overflowX: 'hidden',
                height: '560px',
                maxWidth: '800px',
                maxHeight: '90vh',
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
                maxHeight: 300,
                overflowY: 'auto',
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
                    <Link onClick={_e => onNameClick(item.id)}>{item.name}</Link>
                </div>
            );
        },
        [styles.statusRow, onNameClick]
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

    const toggleItemSelection = useCallback(
        (id: string) => {
            const toggledItems = resourceGroupsWithSelection?.map(item => (item.id === id ? { ...item, selected: !item.selected } : item));
            setResourceGroupsWithSelection(toggledItems);
        },
        [resourceGroupsWithSelection]
    );

    const onRenderCheckbox = useCallback(
        (item: ResourceGroupWithSelection) => {
            return <Checkbox checked={item.selected} onChange={() => toggleItemSelection(item.id)} />;
        },
        [toggleItemSelection]
    );

    const allSelected = useMemo(() => {
        return (resourceGroupsWithSelection?.length ?? 0) > 0 && resourceGroupsWithSelection?.every(item => item.selected);
    }, [resourceGroupsWithSelection]);

    const toggleSelectAll = useCallback(
        (checked: boolean) => {
            const allSelected = resourceGroupsWithSelection?.map(item => ({ ...item, selected: checked }));
            setResourceGroupsWithSelection(allSelected);
        },
        [resourceGroupsWithSelection]
    );

    const onRenderCheckboxHeader = useCallback(() => {
        return (
            <div style={{ display: 'flex', alignItems: 'center', marginTop: '12px' }}>
                <Checkbox checked={allSelected} onChange={(_, checked) => toggleSelectAll(!!checked)} />
            </div>
        );
    }, [allSelected, toggleSelectAll]);

    const columns = useMemo<ISortedDetailsListColumn[]>(() => {
        return [
            {
                key: ResourceGroupListColumnKey.selected,
                name: '',
                fieldName: ResourceGroupListColumnKey.selected,
                minWidth: 30,
                maxWidth: 30,
                onRender: onRenderCheckbox,
                onRenderHeader: onRenderCheckboxHeader,
                isMultiline: false,
            },
            {
                key: ResourceGroupListColumnKey.name,
                name: intl.formatMessage(ManagedResourcesStringResources.resourceGroupName),
                fieldName: ResourceGroupListColumnKey.name,
                minWidth: 300,
                maxWidth: 500,
                isResizable: true,
                onRender: onRenderName,
            },
            {
                key: ResourceGroupListColumnKey.subscription,
                name: intl.formatMessage(ManagedResourcesStringResources.subscription),
                fieldName: ResourceGroupListColumnKey.subscription,
                minWidth: 225,
                maxWidth: 400,
                isResizable: true,
                onRender: onRenderSubscription,
            },
            {
                key: ResourceGroupListColumnKey.location,
                name: intl.formatMessage(ManagedResourcesStringResources.location),
                fieldName: ResourceGroupListColumnKey.location,
                minWidth: 150,
                maxWidth: 150,
                isResizable: true,
                onRender: onRenderLocation,
            },
        ];
    }, [onRenderName, onRenderLocation, onRenderCheckbox, onRenderCheckboxHeader, onRenderSubscription, intl]);

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
        return groups;
    }, [selectedLocationKeys, resourceGroupsWithSelection, filter, existingResourceGroupIds]);

    const selectedResourceGroups = useMemo(() => {
        return resourceGroupsWithSelection?.filter(item => item.selected) ?? [];
    }, [resourceGroupsWithSelection]);

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

    return (
        <Dialog
            hidden={hideResourceGroupPicker}
            onDismiss={_e => setHideResourceGroupPicker(true)}
            dialogContentProps={dialogContentProps}
            modalProps={modelProps}
            minWidth={800}
            maxWidth={800}
        >
            <div className={styles.dialogContent}>
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
                <div style={{ display: 'flex', maxHeight: '380px', overflowY: 'scroll', width: '100%' }} data-is-scrollable="true">
                    <ShimmeredDetailsList
                        columns={columns}
                        constrainMode={ConstrainMode.horizontalConstrained}
                        items={filteredResourceGroups ?? []}
                        layoutMode={DetailsListLayoutMode.justified}
                        compact={true}
                        enableShimmer={resourceGroupsLoading}
                        checkboxVisibility={CheckboxVisibility.hidden}
                    />
                </div>
            </div>

            <div className={styles.dialogFooter}>
                <DialogFooter>
                    <PrimaryButton
                        className={styles.footerButtonDiv}
                        disabled={selectedResourceGroups.length === 0}
                        onClick={() => {
                            onClick(selectedResourceGroups);
                            setHideResourceGroupPicker(true);
                        }}
                        text={intl.formatMessage(ManagedResourcesStringResources.save)}
                    />
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
