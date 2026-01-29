import { Button, mergeClasses, Spinner, Text } from '@fluentui/react-components';
import { CheckmarkCircle16Filled, Info16Filled, Warning16Filled } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { RBACRoleIdToNameMap, RBACRoleNames } from '../../../Common/Contracts/Azure/Permission';
import { AgentAccessLevel } from '../../../Common/Contracts/Azure/SreAgent';
import { OnboardingWizardResources, RolesResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { useGrantPermissions } from '../Hooks/useGrantPermissions';
import { WizardFormValues } from '../OnboardingWizard';
import { usePermissionsStepStyles } from '../OnboardingWizard.styles';

/**
 * Maps RBAC role names to localized resource keys
 */
const getRoleDisplayInfo = (roleId: string, intl: ReturnType<typeof useIntl>): { name: string; description: string } => {
    const roleName = RBACRoleIdToNameMap[roleId] ?? roleId;

    const roleResourceMap: Record<string, { name: string; description: string }> = {
        [RBACRoleNames.reader]: {
            name: intl.formatMessage(RolesResources.reader),
            description: intl.formatMessage(RolesResources.readerDescription),
        },
        [RBACRoleNames.monitoringReader]: {
            name: intl.formatMessage(RolesResources.monitoringReader),
            description: intl.formatMessage(RolesResources.monitoringReaderDescription),
        },
        [RBACRoleNames.logAnalyticsReader]: {
            name: intl.formatMessage(RolesResources.logAnalyticsReader),
            description: intl.formatMessage(RolesResources.logAnalyticsReaderDescription),
        },
        [RBACRoleNames.containerAppsContributor]: {
            name: intl.formatMessage(RolesResources.containerAppsContributor),
            description: intl.formatMessage(RolesResources.containerAppsContributorDescription),
        },
        [RBACRoleNames.websitesContributor]: {
            name: intl.formatMessage(RolesResources.websitesContributor),
            description: intl.formatMessage(RolesResources.websitesContributorDescription),
        },
        [RBACRoleNames.storageBlobDataContributor]: {
            name: intl.formatMessage(RolesResources.storageBlobDataContributor),
            description: intl.formatMessage(RolesResources.storageBlobDataContributorDescription),
        },
        [RBACRoleNames.containerAppsOperator]: {
            name: intl.formatMessage(RolesResources.containerAppsOperator),
            description: intl.formatMessage(RolesResources.containerAppsOperatorDescription),
        },
    };

    return roleResourceMap[roleName] ?? { name: roleName, description: '' };
};

/**
 * Step 4: Grant Permissions
 * Grants RBAC roles to the agent's managed identity on the selected scope
 */
export const GrantPermissionsStep: FC = () => {
    const intl = useIntl();
    const styles = usePermissionsStepStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const { agentObj } = useContext(SreAgentContext);

    const { values, setFieldValue } = useFormikContext<WizardFormValues>();

    const hasSubscriptionScope = values.selectedSubscriptionIds.length > 0;
    const hasResourceGroupScope = values.selectedResourceGroupIds.length > 0;
    const primarySubscriptionId = values.selectedSubscriptionIds[0] ?? '';
    const primaryResourceGroupId = values.selectedResourceGroupIds[0] ?? '';

    const agentIdentityResourceId = useMemo(() => {
        return agentObj?.properties?.knowledgeGraphConfiguration?.identity ?? '';
    }, [agentObj]);

    const { requiredRoleIds, existingRoleIds, missingRoleIds, isLoading, isGranting, error, grantSuccess, grantPermissions } =
        useGrantPermissions({
            scopeType: hasSubscriptionScope ? 'subscription' : 'resourceGroup',
            subscriptionId: primarySubscriptionId,
            resourceGroupId: primaryResourceGroupId,
            permissionsLevel: values.permissionsLevel,
            agentIdentityResourceId,
            agentResourceId: resourceId,
            location: agentObj?.location ?? '',
        });

    const scopeDisplayName = useMemo(() => {
        if (hasSubscriptionScope) {
            return primarySubscriptionId;
        }
        if (hasResourceGroupScope) {
            const parts = primaryResourceGroupId.split('/');
            const rgIndex = parts.findIndex(p => p.toLowerCase() === 'resourcegroups');
            return rgIndex >= 0 && parts[rgIndex + 1] ? parts[rgIndex + 1] : primaryResourceGroupId;
        }
        return '';
    }, [hasSubscriptionScope, hasResourceGroupScope, primarySubscriptionId, primaryResourceGroupId]);

    const handlePermissionLevelChange = useCallback(
        (level: AgentAccessLevel) => {
            setFieldValue('permissionsLevel', level);
        },
        [setFieldValue]
    );

    const handleGrantPermissions = useCallback(async () => {
        await grantPermissions();
    }, [grantPermissions]);

    const roleItems = useMemo(() => {
        return requiredRoleIds.map(roleId => {
            const { name, description } = getRoleDisplayInfo(roleId, intl);
            const isGranted = existingRoleIds.includes(roleId);
            return { roleId, name, description, isGranted };
        });
    }, [requiredRoleIds, existingRoleIds, intl]);

    const allPermissionsGranted = missingRoleIds.length === 0 && requiredRoleIds.length > 0;

    if (isLoading) {
        return (
            <div className={styles.container}>
                <div className={styles.loadingContainer}>
                    <Spinner size="small" />
                    <Text className={styles.loadingText}>{intl.formatMessage(OnboardingWizardResources.checkingPermissions)}</Text>
                </div>
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <Text className={styles.description}>
                {hasSubscriptionScope
                    ? intl.formatMessage(OnboardingWizardResources.permissionsSubscriptionDescription)
                    : intl.formatMessage(OnboardingWizardResources.permissionsResourceGroupDescription)}
            </Text>

            {!hasSubscriptionScope && (
                <div className={styles.permissionLevelContainer}>
                    <Text className={styles.permissionLevelTitle}>{intl.formatMessage(OnboardingWizardResources.permissionsLevel)}</Text>
                    <div className={styles.permissionLevelOptions}>
                        <div
                            className={mergeClasses(
                                styles.permissionLevelCard,
                                values.permissionsLevel === AgentAccessLevel.low && styles.permissionLevelCardSelected
                            )}
                            onClick={() => handlePermissionLevelChange(AgentAccessLevel.low)}
                            role="button"
                            tabIndex={0}
                            onKeyDown={e => e.key === 'Enter' && handlePermissionLevelChange(AgentAccessLevel.low)}
                            aria-pressed={values.permissionsLevel === AgentAccessLevel.low}
                        >
                            <div className={styles.permissionLevelCardTitle}>
                                <div
                                    className={mergeClasses(
                                        styles.radioIcon,
                                        values.permissionsLevel === AgentAccessLevel.low && styles.radioIconSelected
                                    )}
                                >
                                    {values.permissionsLevel === AgentAccessLevel.low && <div className={styles.radioIconInner} />}
                                </div>
                                {intl.formatMessage(OnboardingWizardResources.readerLevel)}
                            </div>
                            <Text className={styles.permissionLevelCardDescription}>
                                {intl.formatMessage(OnboardingWizardResources.readerLevelDescription)}
                            </Text>
                        </div>

                        <div
                            className={mergeClasses(
                                styles.permissionLevelCard,
                                values.permissionsLevel === AgentAccessLevel.high && styles.permissionLevelCardSelected
                            )}
                            onClick={() => handlePermissionLevelChange(AgentAccessLevel.high)}
                            role="button"
                            tabIndex={0}
                            onKeyDown={e => e.key === 'Enter' && handlePermissionLevelChange(AgentAccessLevel.high)}
                            aria-pressed={values.permissionsLevel === AgentAccessLevel.high}
                        >
                            <div className={styles.permissionLevelCardTitle}>
                                <div
                                    className={mergeClasses(
                                        styles.radioIcon,
                                        values.permissionsLevel === AgentAccessLevel.high && styles.radioIconSelected
                                    )}
                                >
                                    {values.permissionsLevel === AgentAccessLevel.high && <div className={styles.radioIconInner} />}
                                </div>
                                {intl.formatMessage(OnboardingWizardResources.privilegedLevel)}
                            </div>
                            <Text className={styles.permissionLevelCardDescription}>
                                {intl.formatMessage(OnboardingWizardResources.privilegedLevelDescription)}
                            </Text>
                        </div>
                    </div>
                </div>
            )}

            <div className={styles.scopeSection}>
                <Text className={styles.scopeLabel}>{intl.formatMessage(OnboardingWizardResources.scopeLabel)}</Text>
                <Text className={styles.scopeValue}>
                    {hasSubscriptionScope
                        ? intl.formatMessage(OnboardingWizardResources.subscriptionScopeLabel, { name: scopeDisplayName })
                        : intl.formatMessage(OnboardingWizardResources.resourceGroupScopeLabel, { name: scopeDisplayName })}
                </Text>
            </div>

            {roleItems.length > 0 && (
                <div className={styles.roleGridContainer}>
                    <Text className={styles.roleGridTitle}>{intl.formatMessage(OnboardingWizardResources.rolesToBeGranted)}</Text>
                    <div className={styles.roleGrid}>
                        <div className={mergeClasses(styles.roleRow, styles.roleRowHeader)}>
                            <Text className={styles.roleColumn}>{intl.formatMessage(OnboardingWizardResources.roleColumn)}</Text>
                            <Text className={styles.descriptionColumn}>
                                {intl.formatMessage(OnboardingWizardResources.descriptionColumn)}
                            </Text>
                            <Text className={styles.statusColumn}>{intl.formatMessage(OnboardingWizardResources.statusColumn)}</Text>
                        </div>

                        {roleItems.map(item => (
                            <div key={item.roleId} className={styles.roleRow}>
                                <Text className={styles.roleColumn}>{item.name}</Text>
                                <Text className={styles.descriptionColumn}>{item.description}</Text>
                                <div
                                    className={mergeClasses(
                                        styles.statusColumn,
                                        item.isGranted ? styles.statusGranted : styles.statusNeeded
                                    )}
                                >
                                    {item.isGranted ? (
                                        <>
                                            <CheckmarkCircle16Filled />
                                            {intl.formatMessage(OnboardingWizardResources.roleGranted)}
                                        </>
                                    ) : (
                                        <>
                                            <Warning16Filled />
                                            {intl.formatMessage(OnboardingWizardResources.roleNeeded)}
                                        </>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>

                    <div className={styles.summarySection}>
                        <div className={mergeClasses(styles.summaryItem, styles.statusGranted)}>
                            <CheckmarkCircle16Filled />
                            {intl.formatMessage(OnboardingWizardResources.alreadyGrantedCount, {
                                count: existingRoleIds.length,
                            })}
                        </div>
                        <div className={mergeClasses(styles.summaryItem, styles.statusNeeded)}>
                            <Warning16Filled />
                            {intl.formatMessage(OnboardingWizardResources.needsAssignmentCount, {
                                count: missingRoleIds.length,
                            })}
                        </div>
                    </div>
                </div>
            )}

            <div className={styles.actionSection}>
                {isGranting ? (
                    <div className={styles.loadingContainer}>
                        <Spinner size="small" />
                        <Text className={styles.loadingText}>{intl.formatMessage(OnboardingWizardResources.grantingPermissions)}</Text>
                    </div>
                ) : allPermissionsGranted || grantSuccess ? (
                    <div className={styles.successMessage}>
                        <CheckmarkCircle16Filled />
                        {intl.formatMessage(OnboardingWizardResources.allPermissionsGranted)}
                    </div>
                ) : missingRoleIds.length > 0 ? (
                    <Button appearance="primary" onClick={handleGrantPermissions}>
                        {intl.formatMessage(OnboardingWizardResources.grantPermissionsButton, {
                            count: missingRoleIds.length,
                        })}
                    </Button>
                ) : null}

                {error && (
                    <div className={styles.errorMessage}>
                        <Warning16Filled />
                        {intl.formatMessage(OnboardingWizardResources.permissionsGrantError, { error })}
                    </div>
                )}
            </div>

            {hasSubscriptionScope && (
                <div className={styles.infoMessage}>
                    <Info16Filled />
                    {intl.formatMessage(OnboardingWizardResources.subscriptionScopeNote)}
                </div>
            )}

            <Text className={styles.optionalNote}>{intl.formatMessage(OnboardingWizardResources.permissionsOptional)}</Text>
        </div>
    );
};
