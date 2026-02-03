import { Body1Strong } from '@fluentui-copilot/react-copilot';
import { Button, Card, CardHeader, makeStyles, Skeleton, SkeletonItem, tokens } from '@fluentui/react-components';
import { ChevronDownRegular, ChevronUpRegular, DismissRegular, RocketRegular } from '@fluentui/react-icons';
import { Collapse } from '@fluentui/react-motion-components-preview';
import { Formik, FormikHelpers } from 'formik';
import { FC, memo, useCallback, useContext, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import {
    useIncidentPlatformPicker,
    UseIncidentPlatformPickerResult,
} from '../../Common/Components/IncidentPlatformPicker/useIncidentPlatformPicker';
import {
    useInfrastructureScopePicker,
    UseInfrastructureScopePickerResult,
} from '../../Common/Components/InfrastructureScopePicker/InfrastructureScopePicker';
import { useKnowledgeBasePicker, UseKnowledgeBasePickerResult } from '../../Common/Components/KnowledgeBasePicker/KnowledgeBasePicker';
import { IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';
import { AgentFormValues, getAgentFormInitialValues } from '../../Common/Utils/AgentFormUtils';
import { OverviewResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { SuggestedActionsContent } from './SuggestedActionsContent';

export interface SuggestedActionsContentProps {
    infrastructurePicker: UseInfrastructureScopePickerResult;
    incidentPlatformPicker: UseIncidentPlatformPickerResult;
    knowledgeBasePicker: UseKnowledgeBasePickerResult;
    isSubscriptionConfigured: boolean;
    isResourceGroupConfigured: boolean;
}

const useSuggestedActionsCardStyles = makeStyles({
    card: {
        overflow: 'hidden',
    },
    headerActions: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    contentWrapper: {
        padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalS}`,
    },
    actionCardsContainer: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
    },
    skeletonCard: {
        width: '140px',
        height: '56px',
        borderRadius: tokens.borderRadiusMedium,
    },
});

/**
 * Calculates the number of suggested action cards that should be visible.
 * Uses the same completion logic as useOnboardingWizard's isStepComplete function.
 */
const useSuggestedActionsCount = () => {
    const { agentObj } = useContext(SreAgentContext);
    const agent = agentObj?.properties;

    return useMemo(() => {
        const managedResources = agent?.knowledgeGraphConfiguration?.managedResources ?? [];

        // Check if any subscriptions are configured
        const hasSubscriptions = managedResources.some(
            r => r.startsWith('/subscriptions/') && !r.toLowerCase().includes('/resourcegroups/')
        );

        // Check if any resource groups are configured
        const hasResourceGroups = managedResources.some(r => r.toLowerCase().includes('/resourcegroups/'));

        // Check if incident platform is configured
        const incidentType = agent?.incidentManagementConfiguration?.type;
        const hasIncidentPlatform = incidentType !== undefined && incidentType !== IncidentManagementType.None;

        // Count incomplete items
        let count = 0;
        if (!hasSubscriptions) count++;
        if (!hasResourceGroups) count++;
        if (!hasIncidentPlatform) count++;

        // Knowledge base cards are always shown (3 cards: repository, file, web page)
        count += 3;

        return count;
    }, [agent]);
};

const SuggestedActionsCard: FC = () => {
    const [isOpen, setIsOpen] = useState<boolean>(true);
    const [isSubscriptionConfigured, setIsSubscriptionConfigured] = useState<boolean>(false);
    const [isResourceGroupConfigured, setIsResourceGroupConfigured] = useState<boolean>(false);

    const intl = useIntl();
    const styles = useSuggestedActionsCardStyles();
    const azPortalContext = useAzPortalContext();

    const { resourceId } = useContext(EnvironmentContext);
    const { agentObj, patchAgent, agentLoaded } = useContext(SreAgentContext);

    const suggestedActionsCount = useSuggestedActionsCount();
    const initialValues = useMemo<AgentFormValues>(() => getAgentFormInitialValues(agentObj, resourceId), [agentObj, resourceId]);

    // Use refs to track current values for save callbacks (avoid stale closures)
    const selectedResourceGroupIdsRef = useRef<string[]>(initialValues.selectedResourceGroupIds);
    const selectedSubscriptionIdsRef = useRef<string[]>(initialValues.selectedSubscriptionIds);

    // Save infrastructure scope to backend
    const saveInfrastructureScope = useCallback(
        async (subscriptionIds: string[], resourceGroupIds: string[]) => {
            const subscriptionResources = subscriptionIds.map(id => `/subscriptions/${id}`);
            const managedResources = [...subscriptionResources, ...resourceGroupIds];

            const response = await patchAgent({
                properties: {
                    knowledgeGraphConfiguration: {
                        ...agentObj?.properties?.knowledgeGraphConfiguration,
                        managedResources,
                    },
                },
            });

            if (response.metadata.success) {
                if (subscriptionIds.length > 0) {
                    setIsSubscriptionConfigured(true);
                }
                if (resourceGroupIds.length > 0) {
                    setIsResourceGroupConfigured(true);
                }
                azPortalContext.log({
                    action: 'suggested-actions-infrastructure',
                    actionModifier: 'saved',
                    logLevel: 'info',
                    data: { subscriptionCount: subscriptionIds.length, resourceGroupCount: resourceGroupIds.length },
                });
            } else {
                azPortalContext.log({
                    action: 'suggested-actions-infrastructure',
                    actionModifier: 'save-failed',
                    logLevel: 'error',
                });
            }
        },
        [patchAgent, agentObj, azPortalContext]
    );

    // Callback when subscriptions change from dialog
    const handleSubscriptionsChange = useCallback(
        (subscriptionIds: string[]) => {
            selectedSubscriptionIdsRef.current = subscriptionIds;
            saveInfrastructureScope(subscriptionIds, selectedResourceGroupIdsRef.current);
        },
        [saveInfrastructureScope]
    );

    // Callback when resource groups change from dialog
    const handleResourceGroupsChange = useCallback(
        (resourceGroupIds: string[]) => {
            selectedResourceGroupIdsRef.current = resourceGroupIds;
            saveInfrastructureScope(selectedSubscriptionIdsRef.current, resourceGroupIds);
        },
        [saveInfrastructureScope]
    );

    // Lift hooks outside the Collapse to prevent re-fetching on collapse/expand
    const infrastructurePicker = useInfrastructureScopePicker({
        initialSubscriptionIds: initialValues.selectedSubscriptionIds,
        initialResourceGroupIds: initialValues.selectedResourceGroupIds,
        onSubscriptionsChange: handleSubscriptionsChange,
        onResourceGroupsChange: handleResourceGroupsChange,
    });

    const incidentPlatformPicker = useIncidentPlatformPicker({
        initialPlatformType: initialValues.incidentPlatformType,
        initialPagerDutyApiKey: initialValues.pagerDutyApiKey,
        initialServiceNowEndpoint: initialValues.serviceNowEndpoint,
        initialServiceNowUsername: initialValues.serviceNowUsername,
        initialServiceNowPassword: initialValues.serviceNowPassword,
    });
    const knowledgeBasePicker = useKnowledgeBasePicker();

    const handleSubmit = useCallback((_values: AgentFormValues, _helpers: FormikHelpers<AgentFormValues>) => {}, []);

    const handleToggle = useCallback(() => {
        setIsOpen(prev => !prev);
    }, []);

    const handleGoToQuickStart = useCallback((e: React.MouseEvent) => {
        e.stopPropagation();
        // Navigate to quick start
    }, []);

    const handleDismiss = useCallback((e: React.MouseEvent) => {
        e.stopPropagation();
        // Dismiss the card
    }, []);

    // Show loading skeleton while agent data is being fetched
    if (!agentLoaded) {
        return (
            <Card size="small" className={styles.card}>
                <CardHeader
                    header={
                        <Skeleton>
                            <SkeletonItem style={{ width: '180px', height: '20px' }} />
                        </Skeleton>
                    }
                />
                <div className={styles.contentWrapper}>
                    <div className={styles.actionCardsContainer}>
                        <Skeleton>
                            <SkeletonItem className={styles.skeletonCard} />
                        </Skeleton>
                        <Skeleton>
                            <SkeletonItem className={styles.skeletonCard} />
                        </Skeleton>
                        <Skeleton>
                            <SkeletonItem className={styles.skeletonCard} />
                        </Skeleton>
                    </div>
                </div>
            </Card>
        );
    }

    return (
        <Card className={styles.card}>
            <CardHeader
                header={
                    <Body1Strong>{intl.formatMessage(OverviewResources.suggestionActions, { value: suggestedActionsCount })}</Body1Strong>
                }
                action={
                    <div className={styles.headerActions}>
                        <Button
                            size="small"
                            appearance="transparent"
                            icon={isOpen ? <ChevronUpRegular /> : <ChevronDownRegular />}
                            onClick={handleToggle}
                        >
                            {isOpen ? intl.formatMessage(SreAgentResources.collapse) : intl.formatMessage(SreAgentResources.expand)}
                        </Button>
                        <Button size="small" appearance="transparent" icon={<RocketRegular />} onClick={handleGoToQuickStart}>
                            {intl.formatMessage(OverviewResources.goToQuickStart)}
                        </Button>
                        <Button size="small" appearance="transparent" icon={<DismissRegular />} onClick={handleDismiss}>
                            {intl.formatMessage(SreAgentResources.dismiss)}
                        </Button>
                    </div>
                }
            />

            {/* Collapsible Content using Fluent UI Collapse wrapped in Formik */}
            <Collapse visible={isOpen}>
                <div className={styles.contentWrapper}>
                    <Formik<AgentFormValues> initialValues={initialValues} onSubmit={handleSubmit} enableReinitialize>
                        <SuggestedActionsContent
                            infrastructurePicker={infrastructurePicker}
                            incidentPlatformPicker={incidentPlatformPicker}
                            knowledgeBasePicker={knowledgeBasePicker}
                            isSubscriptionConfigured={isSubscriptionConfigured}
                            isResourceGroupConfigured={isResourceGroupConfigured}
                        />
                    </Formik>
                </div>
            </Collapse>
        </Card>
    );
};

export default memo(SuggestedActionsCard);
