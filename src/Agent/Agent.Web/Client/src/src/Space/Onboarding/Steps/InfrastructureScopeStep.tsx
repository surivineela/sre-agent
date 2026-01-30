import { Caption1, Subtitle2 } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, startTransition, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import ResourceGroupIcon from '../../../../assets/ResourceGroup.svg';
import SubscriptionIcon from '../../../../assets/Subscription.svg';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { useSubscriptionsWithRoles } from '../../../Common/Components/AzureResourcePicker/Hooks/useSubscriptionsWithRoles';
import { SelectableCard } from '../../../Common/Components/SelectableCard/SelectableCard';
import { getUserFriendlyLocation } from '../../../Common/Helpers/LocationHelper';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { AgentFormValues } from '../../../Common/Utils/AgentFormUtils';
import { openResourceGroupOverviewInNewTab, openSubscriptionOverviewInNewTab } from '../../../Common/Utils/UrlUtils';
import { OnboardingWizardResources } from '../../../Strings/SREAgentResources';
import { useSubscriptions } from '../../Settings/Hooks/useSubscriptions';
import { useInfrastructureScopeStepStyles } from '../OnboardingWizard.styles';
import { ResourceGroupPickerDialog } from './ResourceGroupPickerDialog';
import { ScopeCellWithLink, ScopeDataGrid, ScopeDataGridColumn } from './ScopeDataGrid';
import { SubscriptionPickerDialog } from './SubscriptionPickerDialog';

interface SelectedSubscriptionDisplay {
    subscriptionId: string;
    displayName: string;
    myRole: string | undefined;
}

interface SelectedResourceGroupDisplay {
    id: string;
    name: string;
    subscriptionId: string;
    subscriptionDisplayName: string;
    location: string;
}

export const InfrastructureScopeStep: FC = () => {
    const intl = useIntl();
    const styles = useInfrastructureScopeStepStyles();
    const portalContext = useContext(AzPortalContext) as AzPortalProxy;
    const { resourceId } = useContext(EnvironmentContext);

    const { values, setFieldValue } = useFormikContext<AgentFormValues>();
    const { subscriptionsList, subscriptionsLoading } = useSubscriptions();
    const {
        selectableSubscriptions,
        disabledSubscriptions,
        isLoading: subscriptionsWithRolesLoading,
    } = useSubscriptionsWithRoles(portalContext);

    const [isSubscriptionPickerOpen, setIsSubscriptionPickerOpen] = useState(false);
    const [isResourceGroupPickerOpen, setIsResourceGroupPickerOpen] = useState(false);

    const defaultSubscriptionId = useMemo(() => {
        const descriptor = new ArmResourceDescriptor(resourceId);
        return descriptor.subscription;
    }, [resourceId]);

    const allSubscriptions = useMemo(
        () =>
            subscriptionsList?.map((s: { subscriptionId: string; displayName: string }) => ({
                subscriptionId: s.subscriptionId,
                displayName: s.displayName,
            })) ?? [],
        [subscriptionsList]
    );

    const selectedSubscriptionsDisplay = useMemo((): SelectedSubscriptionDisplay[] => {
        if (!subscriptionsList) return [];
        const allSubscriptionsWithRoles = [...selectableSubscriptions, ...disabledSubscriptions];
        const result: SelectedSubscriptionDisplay[] = [];
        values.selectedSubscriptionIds.forEach(subId => {
            const sub = subscriptionsList.find((s: { subscriptionId: string }) => s.subscriptionId === subId);
            const subWithRole = allSubscriptionsWithRoles.find(s => s.subscriptionId === subId);
            if (sub) {
                result.push({
                    subscriptionId: sub.subscriptionId,
                    displayName: sub.displayName,
                    myRole: subWithRole?.myRole ?? undefined,
                });
            }
        });
        return result;
    }, [values.selectedSubscriptionIds, subscriptionsList, selectableSubscriptions, disabledSubscriptions]);

    const selectedResourceGroupsDisplay = useMemo((): SelectedResourceGroupDisplay[] => {
        return values.selectedResourceGroupIds.map(rgId => {
            const subscriptionMatch = rgId.match(/\/subscriptions\/([^/]+)/i);
            const rgNameMatch = rgId.match(/\/resourceGroups\/([^/]+)/i);
            const subscriptionId = subscriptionMatch ? subscriptionMatch[1] : '';
            const name = rgNameMatch ? rgNameMatch[1] : rgId;
            const subscription = subscriptionsList?.find((s: { subscriptionId: string }) => s.subscriptionId === subscriptionId);

            return {
                id: rgId,
                name,
                subscriptionId,
                subscriptionDisplayName: subscription?.displayName ?? subscriptionId,
                location: values.resourceGroupLocations[rgId] ?? '',
            };
        });
    }, [values.selectedResourceGroupIds, values.resourceGroupLocations, subscriptionsList]);

    const handleSubscriptionsApplied = useCallback(
        (selectedSubscriptionIds: string[]) => {
            setFieldValue('selectedSubscriptionIds', selectedSubscriptionIds);
            setIsSubscriptionPickerOpen(false);
        },
        [setFieldValue]
    );

    const handleResourceGroupsApplied = useCallback(
        (selectedResourceGroupIds: string[], locations: Record<string, string>) => {
            setFieldValue('selectedResourceGroupIds', selectedResourceGroupIds);
            setFieldValue('resourceGroupLocations', {
                ...values.resourceGroupLocations,
                ...locations,
            });
            setIsResourceGroupPickerOpen(false);
        },
        [setFieldValue, values.resourceGroupLocations]
    );

    const handleDeleteSelectedSubscriptions = useCallback(
        (selectedIds: string[]) => {
            const selectedSet = new Set(selectedIds);
            const remainingIds = values.selectedSubscriptionIds.filter(id => !selectedSet.has(id));
            setFieldValue('selectedSubscriptionIds', remainingIds);
        },
        [values.selectedSubscriptionIds, setFieldValue]
    );

    const handleDeleteSelectedResourceGroups = useCallback(
        (selectedIds: string[]) => {
            const selectedSet = new Set(selectedIds);
            const remainingIds = values.selectedResourceGroupIds.filter(id => !selectedSet.has(id));
            setFieldValue('selectedResourceGroupIds', remainingIds);
        },
        [values.selectedResourceGroupIds, setFieldValue]
    );

    const handleOpenSubscriptionInPortal = useCallback(
        (subscriptionId: string, subscriptionName: string) => {
            portalContext.logAmplitudeNavigationEvent({
                targetType: 'link',
                targetAction: 'openBlade',
                targetName: 'subscriptionLink',
                targetFriendlyName: `Open subscription in Azure Portal: ${subscriptionName}`,
            });
            openSubscriptionOverviewInNewTab(subscriptionId);
        },
        [portalContext]
    );

    const handleOpenResourceGroupInPortal = useCallback(
        (resourceGroupId: string, resourceGroupName: string) => {
            portalContext.logAmplitudeNavigationEvent({
                targetType: 'link',
                targetAction: 'openBlade',
                targetName: 'resourceGroupLink',
                targetFriendlyName: `Open resource group in Azure Portal: ${resourceGroupName}`,
            });
            openResourceGroupOverviewInNewTab(resourceGroupId);
        },
        [portalContext]
    );

    const subscriptionColumns: ScopeDataGridColumn<SelectedSubscriptionDisplay>[] = useMemo(
        () => [
            {
                columnId: 'name',
                headerLabel: intl.formatMessage(OnboardingWizardResources.subscriptionName),
                minWidth: 200,
                defaultWidth: 280,
                renderCell: (item: SelectedSubscriptionDisplay) => (
                    <ScopeCellWithLink
                        icon={<img src={SubscriptionIcon} alt="" aria-hidden="true" style={{ width: 16, height: 16 }} />}
                        label={item.displayName}
                        onOpenExternal={() => handleOpenSubscriptionInPortal(item.subscriptionId, item.displayName)}
                        openExternalAriaLabel={intl.formatMessage(OnboardingWizardResources.openInAzurePortal)}
                    />
                ),
            },
            {
                columnId: 'role',
                headerLabel: intl.formatMessage(OnboardingWizardResources.myRole),
                minWidth: 100,
                defaultWidth: 150,
                renderCell: (item: SelectedSubscriptionDisplay) => item.myRole ?? '—',
            },
        ],
        [intl, handleOpenSubscriptionInPortal]
    );

    const resourceGroupColumns: ScopeDataGridColumn<SelectedResourceGroupDisplay>[] = useMemo(
        () => [
            {
                columnId: 'name',
                headerLabel: intl.formatMessage(OnboardingWizardResources.resourceGroup),
                minWidth: 150,
                defaultWidth: 200,
                renderCell: (item: SelectedResourceGroupDisplay) => (
                    <ScopeCellWithLink
                        icon={<img src={ResourceGroupIcon} alt="" aria-hidden="true" style={{ width: 16, height: 16 }} />}
                        label={item.name}
                        onOpenExternal={() => handleOpenResourceGroupInPortal(item.id, item.name)}
                        openExternalAriaLabel={intl.formatMessage(OnboardingWizardResources.openInAzurePortal)}
                    />
                ),
            },
            {
                columnId: 'subscription',
                headerLabel: intl.formatMessage(OnboardingWizardResources.subscription),
                minWidth: 150,
                defaultWidth: 200,
                renderCell: (item: SelectedResourceGroupDisplay) => item.subscriptionDisplayName,
            },
            {
                columnId: 'region',
                headerLabel: intl.formatMessage(OnboardingWizardResources.region),
                minWidth: 100,
                defaultWidth: 120,
                renderCell: (item: SelectedResourceGroupDisplay) => (item.location ? getUserFriendlyLocation(item.location) : '—'),
            },
        ],
        [intl, handleOpenResourceGroupInPortal]
    );

    return (
        <div className={styles.container}>
            <div className={styles.headerSection}>
                <Subtitle2>{intl.formatMessage(OnboardingWizardResources.agentScope)}</Subtitle2>
                <Caption1>{intl.formatMessage(OnboardingWizardResources.agentScopeDescription)}</Caption1>
            </div>

            <div className={styles.addButtonsContainer}>
                <SelectableCard
                    onSelect={() => startTransition(() => setIsSubscriptionPickerOpen(true))}
                    disabled={subscriptionsLoading}
                    icon={<img src={SubscriptionIcon} alt="" aria-hidden="true" />}
                    title={intl.formatMessage(OnboardingWizardResources.addSubscription)}
                />
                <SelectableCard
                    onSelect={() => setIsResourceGroupPickerOpen(true)}
                    icon={<img src={ResourceGroupIcon} alt="" aria-hidden="true" />}
                    title={intl.formatMessage(OnboardingWizardResources.addResourceGroup)}
                />
            </div>

            <ScopeDataGrid
                title={intl.formatMessage(OnboardingWizardResources.subscriptionScope)}
                items={selectedSubscriptionsDisplay}
                columns={subscriptionColumns}
                getRowId={item => item.subscriptionId}
                emptyMessage={intl.formatMessage(OnboardingWizardResources.noSubscriptionsSelected)}
                ariaLabel={intl.formatMessage(OnboardingWizardResources.subscriptionScope)}
                onDeleteSelected={handleDeleteSelectedSubscriptions}
            />

            <ScopeDataGrid
                title={intl.formatMessage(OnboardingWizardResources.resourceGroupScope)}
                items={selectedResourceGroupsDisplay}
                columns={resourceGroupColumns}
                getRowId={item => item.id}
                emptyMessage={intl.formatMessage(OnboardingWizardResources.noResourceGroupsSelected)}
                ariaLabel={intl.formatMessage(OnboardingWizardResources.resourceGroupScope)}
                onDeleteSelected={handleDeleteSelectedResourceGroups}
            />

            <SubscriptionPickerDialog
                isOpen={isSubscriptionPickerOpen}
                onDismiss={() => setIsSubscriptionPickerOpen(false)}
                onApply={handleSubscriptionsApplied}
                initialSelectedIds={values.selectedSubscriptionIds}
                selectableSubscriptions={selectableSubscriptions}
                disabledSubscriptions={disabledSubscriptions}
                isLoading={subscriptionsWithRolesLoading}
            />

            <ResourceGroupPickerDialog
                isOpen={isResourceGroupPickerOpen}
                onDismiss={() => setIsResourceGroupPickerOpen(false)}
                onApply={handleResourceGroupsApplied}
                initialSelectedIds={values.selectedResourceGroupIds}
                allSubscriptions={allSubscriptions}
                defaultSubscriptionId={defaultSubscriptionId}
            />
        </div>
    );
};
