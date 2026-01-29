import { Button, TableCellLayout, TableColumnDefinition, Text, Tooltip, createTableColumn } from '@fluentui/react-components';
import { CheckmarkStarburst16Filled, Open16Regular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import SubscriptionIcon from '../../../../assets/Subscription.svg';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { AzureResourcePickerDialog } from '../../../Common/Components/AzureResourcePicker/AzureResourcePickerDialog';
import { useAzureResourcePickerDialogStyles } from '../../../Common/Components/AzureResourcePicker/AzureResourcePickerDialog.styles';
import { SubscriptionWithPermission } from '../../../Common/Components/AzureResourcePicker/Contracts';
import { openSubscriptionOverviewInNewTab } from '../../../Common/Utils/UrlUtils';
import { OnboardingWizardResources } from '../../../Strings/SREAgentResources';

interface SubscriptionPickerDialogProps {
    isOpen: boolean;
    onDismiss: () => void;
    onApply: (selectedSubscriptionIds: string[]) => void;
    initialSelectedIds: string[];
    selectableSubscriptions: SubscriptionWithPermission[];
    disabledSubscriptions: SubscriptionWithPermission[];
    isLoading: boolean;
}

export const SubscriptionPickerDialog: FC<SubscriptionPickerDialogProps> = ({
    isOpen,
    onDismiss,
    onApply,
    initialSelectedIds,
    selectableSubscriptions,
    disabledSubscriptions,
    isLoading,
}) => {
    const intl = useIntl();
    const styles = useAzureResourcePickerDialogStyles();
    const portalContext = useContext(AzPortalContext) as AzPortalProxy;

    const handleOpenInPortal = useCallback(
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

    const columns: TableColumnDefinition<SubscriptionWithPermission>[] = useMemo(
        () => [
            createTableColumn<SubscriptionWithPermission>({
                columnId: 'name',
                renderHeaderCell: () => intl.formatMessage(OnboardingWizardResources.subscriptionName),
                renderCell: item => (
                    <TableCellLayout>
                        <div className={styles.nameCell}>
                            <img src={SubscriptionIcon} alt="" aria-hidden="true" width={16} height={16} />
                            <Text>{item.displayName}</Text>
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
                                        handleOpenInPortal(item.subscriptionId, item.displayName);
                                    }}
                                    aria-label={intl.formatMessage(OnboardingWizardResources.openInAzurePortal)}
                                />
                            </Tooltip>
                        </div>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<SubscriptionWithPermission>({
                columnId: 'role',
                renderHeaderCell: () => intl.formatMessage(OnboardingWizardResources.myRole),
                renderCell: item => (
                    <TableCellLayout>
                        <Text>{item.myRole ?? '—'}</Text>
                    </TableCellLayout>
                ),
            }),
        ],
        [intl, styles, handleOpenInPortal]
    );

    const columnSizingOptions = useMemo(
        () => ({
            name: { minWidth: 350, defaultWidth: 450 },
            role: { minWidth: 140, defaultWidth: 140, idealWidth: 140 },
        }),
        []
    );

    const getItemId = useCallback((item: SubscriptionWithPermission) => item.subscriptionId, []);
    const getItemName = useCallback((item: SubscriptionWithPermission) => item.displayName, []);
    const additionalSearchFilter = useCallback(
        (item: SubscriptionWithPermission, searchLower: string) => item.name.toLowerCase().includes(searchLower),
        []
    );

    return (
        <AzureResourcePickerDialog<SubscriptionWithPermission>
            isOpen={isOpen}
            onDismiss={onDismiss}
            onApply={onApply}
            initialSelectedIds={initialSelectedIds}
            title={intl.formatMessage(OnboardingWizardResources.addSubscriptions)}
            searchPlaceholder={intl.formatMessage(OnboardingWizardResources.searchSubscriptions)}
            infoMessage={intl.formatMessage(OnboardingWizardResources.agentSubscriptionReaderAccess)}
            noPermissionMessage={intl.formatMessage(OnboardingWizardResources.noRoleAssignmentPermission)}
            selectableItems={selectableSubscriptions}
            disabledItems={disabledSubscriptions}
            isLoading={isLoading}
            showRecommendedLabel={intl.formatMessage(OnboardingWizardResources.showRecommended)}
            showRecommendedTooltip={intl.formatMessage(OnboardingWizardResources.showRecommendedTooltip)}
            columns={columns}
            columnSizingOptions={columnSizingOptions}
            getItemId={getItemId}
            getItemName={getItemName}
            additionalSearchFilter={additionalSearchFilter}
            telemetryName="subscriptionPicker"
            applyButtonText={intl.formatMessage(OnboardingWizardResources.addSubscription)}
        />
    );
};
