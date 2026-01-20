import { Dropdown, Label, mergeClasses, Option, Skeleton, SkeletonItem, Text } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import ResourceGroupIcon from '../../../../assets/ResourceGroup.svg';
import SubscriptionIcon from '../../../../assets/Subscription.svg';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { OnboardingWizardResources } from '../../../Strings/SREAgentResources';
import { useResourceGroups } from '../../Settings/Hooks/useResourceGroups';
import { useSubscriptions } from '../../Settings/Hooks/useSubscriptions';
import { WizardFormValues } from '../OnboardingWizard';
import { useInfrastructureScopeStepStyles } from '../OnboardingWizard.styles';

export type ScopeType = 'subscription' | 'resourceGroup';

/**
 * Step 1: Infrastructure Scope
 * Pure presentation component - reads/writes from Formik context
 */
export const InfrastructureScopeStep: FC = () => {
    const intl = useIntl();
    const styles = useInfrastructureScopeStepStyles();
    const portalContext = useContext(AzPortalContext) as AzPortalProxy;

    const { values, setFieldValue } = useFormikContext<WizardFormValues>();
    const { subscriptionsList, subscriptionsLoading, subscriptionOptions } = useSubscriptions();

    const subscriptionIdsArray = useMemo(
        () => (values.selectedSubscriptionId ? [values.selectedSubscriptionId] : []),
        [values.selectedSubscriptionId]
    );
    const { resourceGroupOptions, resourceGroupsLoading } = useResourceGroups(subscriptionIdsArray, portalContext);

    const selectedSubscription = useMemo(() => {
        return subscriptionsList?.find((s: { subscriptionId: string }) => s.subscriptionId === values.selectedSubscriptionId);
    }, [subscriptionsList, values.selectedSubscriptionId]);

    const handleScopeTypeChange = useCallback(
        (type: ScopeType) => {
            setFieldValue('scopeType', type);
            if (type === 'subscription') {
                setFieldValue('selectedResourceGroupId', '');
            }
        },
        [setFieldValue]
    );

    const handleSubscriptionChange = useCallback(
        (_: unknown, data: { optionValue?: string }) => {
            if (data.optionValue) {
                setFieldValue('selectedSubscriptionId', data.optionValue);
                setFieldValue('selectedResourceGroupId', '');
            }
        },
        [setFieldValue]
    );

    const handleResourceGroupChange = useCallback(
        (_: unknown, data: { optionValue?: string }) => {
            if (data.optionValue) {
                setFieldValue('selectedResourceGroupId', data.optionValue);
            }
        },
        [setFieldValue]
    );

    return (
        <div className={styles.container}>
            <div className={styles.scopeTypeContainer}>
                <div
                    className={mergeClasses(styles.scopeCard, values.scopeType === 'subscription' && styles.scopeCardSelected)}
                    onClick={() => handleScopeTypeChange('subscription')}
                    role="button"
                    tabIndex={0}
                    onKeyDown={e => e.key === 'Enter' && handleScopeTypeChange('subscription')}
                    aria-pressed={values.scopeType === 'subscription'}
                >
                    <img src={SubscriptionIcon} alt="" className={styles.scopeCardIcon} aria-hidden="true" />
                    <Text className={styles.scopeCardTitle}>{intl.formatMessage(OnboardingWizardResources.subscription)}</Text>
                </div>
                <div
                    className={mergeClasses(styles.scopeCard, values.scopeType === 'resourceGroup' && styles.scopeCardSelected)}
                    onClick={() => handleScopeTypeChange('resourceGroup')}
                    role="button"
                    tabIndex={0}
                    onKeyDown={e => e.key === 'Enter' && handleScopeTypeChange('resourceGroup')}
                    aria-pressed={values.scopeType === 'resourceGroup'}
                >
                    <img src={ResourceGroupIcon} alt="" className={styles.scopeCardIcon} aria-hidden="true" />
                    <Text className={styles.scopeCardTitle}>{intl.formatMessage(OnboardingWizardResources.resourceGroup)}</Text>
                </div>
            </div>

            <Text className={styles.recommendedText}>
                {values.scopeType === 'subscription'
                    ? intl.formatMessage(OnboardingWizardResources.subscriptionScopeDescription)
                    : intl.formatMessage(OnboardingWizardResources.resourceGroupScopeDescription)}
            </Text>

            <div className={styles.formField}>
                <Label required className={styles.formFieldLabel}>
                    {intl.formatMessage(OnboardingWizardResources.subscriptionName)}
                </Label>
                {subscriptionsLoading ? (
                    <Skeleton className={styles.formFieldDropdown}>
                        <SkeletonItem className={styles.skeletonDropdown} />
                    </Skeleton>
                ) : (
                    <Dropdown
                        className={styles.formFieldDropdown}
                        placeholder={intl.formatMessage(OnboardingWizardResources.selectSubscription)}
                        value={subscriptionOptions?.find(o => o.key === values.selectedSubscriptionId)?.text?.toString() ?? ''}
                        selectedOptions={values.selectedSubscriptionId ? [values.selectedSubscriptionId] : []}
                        onOptionSelect={handleSubscriptionChange}
                    >
                        {(subscriptionOptions ?? []).map(option => (
                            <Option key={String(option.key)} value={String(option.key)}>
                                {option.text}
                            </Option>
                        ))}
                    </Dropdown>
                )}
            </div>

            {values.scopeType === 'resourceGroup' && (
                <div className={styles.formField}>
                    <Label required className={styles.formFieldLabel}>
                        {intl.formatMessage(OnboardingWizardResources.resourceGroupName)}
                    </Label>
                    {resourceGroupsLoading ? (
                        <Skeleton className={styles.formFieldDropdown}>
                            <SkeletonItem className={styles.skeletonDropdown} />
                        </Skeleton>
                    ) : (
                        <Dropdown
                            className={styles.formFieldDropdown}
                            placeholder={intl.formatMessage(OnboardingWizardResources.selectResourceGroup)}
                            value={resourceGroupOptions?.find(o => o.key === values.selectedResourceGroupId)?.text?.toString() ?? ''}
                            selectedOptions={values.selectedResourceGroupId ? [values.selectedResourceGroupId] : []}
                            onOptionSelect={handleResourceGroupChange}
                            disabled={!values.selectedSubscriptionId}
                        >
                            {(resourceGroupOptions ?? []).map(option => (
                                <Option key={String(option.key)} value={String(option.key)}>
                                    {option.text}
                                </Option>
                            ))}
                        </Dropdown>
                    )}
                </div>
            )}

            {selectedSubscription && values.scopeType === 'subscription' && (
                <div className={styles.detailsSection}>
                    <Text className={styles.detailsTitle}>{intl.formatMessage(OnboardingWizardResources.subscriptionDetails)}</Text>
                    <div className={styles.detailsGrid}>
                        <Text className={styles.detailsLabel}>{intl.formatMessage(OnboardingWizardResources.subscriptionId)}</Text>
                        <Text className={styles.detailsValue}>{selectedSubscription.subscriptionId}</Text>
                    </div>
                </div>
            )}
        </div>
    );
};
