import { Button, TableCellLayout, TableColumnDefinition, Text, Tooltip, createTableColumn } from '@fluentui/react-components';
import { CheckmarkStarburst16Filled, Open16Regular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import ResourceGroupIcon from '../../../../assets/ResourceGroup.svg';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { AzureResourcePickerDialog } from '../../../Common/Components/AzureResourcePicker/AzureResourcePickerDialog';
import { useAzureResourcePickerDialogStyles } from '../../../Common/Components/AzureResourcePicker/AzureResourcePickerDialog.styles';
import { ResourceGroupWithPermission } from '../../../Common/Components/AzureResourcePicker/Contracts';
import { useResourceGroupsWithRoles } from '../../../Common/Components/AzureResourcePicker/Hooks/useResourceGroupsWithRoles';
import { PillFilter } from '../../../Common/Components/PillFilter/PillFilter';
import { getUserFriendlyLocation } from '../../../Common/Helpers/LocationHelper';
import { OnboardingWizardResources } from '../../../Strings/SREAgentResources';

interface SubscriptionInfo {
    subscriptionId: string;
    displayName: string;
}

interface ResourceGroupPickerDialogProps {
    isOpen: boolean;
    onDismiss: () => void;
    onApply: (selectedResourceGroupIds: string[], locations: Record<string, string>) => void;
    initialSelectedIds: string[];
    allSubscriptions: SubscriptionInfo[];
    defaultSubscriptionId: string;
}

export const ResourceGroupPickerDialog: FC<ResourceGroupPickerDialogProps> = ({
    isOpen,
    onDismiss,
    onApply,
    initialSelectedIds,
    allSubscriptions,
    defaultSubscriptionId,
}) => {
    const intl = useIntl();
    const styles = useAzureResourcePickerDialogStyles();
    const portalContext = useContext(AzPortalContext) as AzPortalProxy;

    const [selectedSubscriptionIds, setSelectedSubscriptionIds] = useState<string[]>([defaultSubscriptionId]);

    useEffect(() => {
        if (isOpen) {
            setSelectedSubscriptionIds([defaultSubscriptionId]);
        }
    }, [isOpen, defaultSubscriptionId]);

    const { selectableResourceGroups, disabledResourceGroups, isLoading } = useResourceGroupsWithRoles(
        selectedSubscriptionIds,
        portalContext
    );

    const subscriptionPillOptions = useMemo(
        () =>
            allSubscriptions.map(sub => ({
                key: sub.subscriptionId,
                label: sub.displayName,
            })),
        [allSubscriptions]
    );

    const handleSubscriptionFilterChange = useCallback((keys: string[]) => {
        setSelectedSubscriptionIds(keys);
    }, []);

    const handleApply = useCallback(
        (selectedIds: string[]) => {
            const allResourceGroups = [...selectableResourceGroups, ...disabledResourceGroups];
            const locations: Record<string, string> = {};
            selectedIds.forEach(id => {
                const rg = allResourceGroups.find(r => r.id === id);
                if (rg) {
                    locations[id] = rg.location;
                }
            });
            onApply(selectedIds, locations);
        },
        [selectableResourceGroups, disabledResourceGroups, onApply]
    );

    const getSubscriptionDisplayName = useCallback(
        (subscriptionId: string): string => {
            const subscription = allSubscriptions.find(s => s.subscriptionId === subscriptionId);
            return subscription?.displayName ?? subscriptionId;
        },
        [allSubscriptions]
    );

    const handleOpenInPortal = useCallback(
        (resourceGroupId: string, resourceGroupName: string) => {
            portalContext.logAmplitudeNavigationEvent({
                targetType: 'link',
                targetAction: 'openBlade',
                targetName: 'resourceGroupLink',
                targetFriendlyName: `Open resource group in Azure Portal: ${resourceGroupName}`,
            });
            portalContext.openBlade({
                detailBlade: 'ResourceGroupOverview',
                detailBladeInputs: { id: resourceGroupId },
                extension: 'HubsExtension',
            });
        },
        [portalContext]
    );

    const columns: TableColumnDefinition<ResourceGroupWithPermission>[] = useMemo(
        () => [
            createTableColumn<ResourceGroupWithPermission>({
                columnId: 'name',
                renderHeaderCell: () => intl.formatMessage(OnboardingWizardResources.resourceGroupNames),
                renderCell: item => (
                    <TableCellLayout>
                        <div className={styles.nameCell}>
                            <img src={ResourceGroupIcon} alt="" aria-hidden="true" width={16} height={16} />
                            <Text>{item.name}</Text>
                            {item.recommended && (
                                <Tooltip content={intl.formatMessage(OnboardingWizardResources.recommendedTooltip)} relationship="label">
                                    <CheckmarkStarburst16Filled className={styles.recommendedIcon} />
                                </Tooltip>
                            )}
                            <Tooltip content={intl.formatMessage(OnboardingWizardResources.openInAzurePortal)} relationship="label">
                                <Button
                                    appearance="transparent"
                                    icon={<Open16Regular />}
                                    size="small"
                                    className={styles.externalLinkIcon}
                                    onClick={e => {
                                        e.stopPropagation();
                                        handleOpenInPortal(item.id, item.name);
                                    }}
                                    aria-label={intl.formatMessage(OnboardingWizardResources.openInAzurePortal)}
                                />
                            </Tooltip>
                        </div>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<ResourceGroupWithPermission>({
                columnId: 'subscription',
                renderHeaderCell: () => intl.formatMessage(OnboardingWizardResources.subscriptionName),
                renderCell: item => (
                    <TableCellLayout>
                        <Text>{getSubscriptionDisplayName(item.subscriptionId)}</Text>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<ResourceGroupWithPermission>({
                columnId: 'region',
                renderHeaderCell: () => intl.formatMessage(OnboardingWizardResources.region),
                renderCell: item => (
                    <TableCellLayout>
                        <Text>{getUserFriendlyLocation(item.location)}</Text>
                    </TableCellLayout>
                ),
            }),
        ],
        [intl, styles, handleOpenInPortal, getSubscriptionDisplayName]
    );

    const columnSizingOptions = useMemo(
        () => ({
            name: { minWidth: 250, defaultWidth: 300 },
            subscription: { minWidth: 180, defaultWidth: 200 },
            region: { minWidth: 120, defaultWidth: 140 },
        }),
        []
    );

    const additionalSearchFilter = useCallback(
        (item: ResourceGroupWithPermission, searchLower: string) =>
            getSubscriptionDisplayName(item.subscriptionId).toLowerCase().includes(searchLower),
        [getSubscriptionDisplayName]
    );

    const subscriptionFilterElement = useMemo(
        () => (
            <PillFilter
                filterType="combobox"
                label={intl.formatMessage(OnboardingWizardResources.subscription)}
                labelDelimiter=""
                options={subscriptionPillOptions}
                onApply={handleSubscriptionFilterChange}
                selectedKeys={selectedSubscriptionIds}
                multiSelect
                useInDialog
            />
        ),
        [intl, subscriptionPillOptions, handleSubscriptionFilterChange, selectedSubscriptionIds]
    );

    return (
        <AzureResourcePickerDialog<ResourceGroupWithPermission>
            isOpen={isOpen}
            onDismiss={onDismiss}
            onApply={handleApply}
            initialSelectedIds={initialSelectedIds}
            title={intl.formatMessage(OnboardingWizardResources.addResourceGroups)}
            searchPlaceholder={intl.formatMessage(OnboardingWizardResources.searchResourceGroups)}
            infoMessage={intl.formatMessage(OnboardingWizardResources.agentResourceGroupReaderAccess)}
            noPermissionMessage={intl.formatMessage(OnboardingWizardResources.noRoleAssignmentPermissionResourceGroups)}
            selectableItems={selectableResourceGroups}
            disabledItems={disabledResourceGroups}
            isLoading={isLoading}
            showRecommendedLabel={intl.formatMessage(OnboardingWizardResources.showRecommended)}
            showRecommendedTooltip={intl.formatMessage(OnboardingWizardResources.showRecommendedResourceGroupsTooltip)}
            columns={columns}
            columnSizingOptions={columnSizingOptions}
            additionalSearchFilter={additionalSearchFilter}
            telemetryName="resourceGroupPicker"
            applyButtonText={intl.formatMessage(OnboardingWizardResources.addResourceGroup)}
            filterElements={subscriptionFilterElement}
        />
    );
};
