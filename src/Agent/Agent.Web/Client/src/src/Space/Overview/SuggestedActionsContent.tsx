import { makeStyles, tokens } from '@fluentui/react-components';
import { FC, useContext, useMemo } from 'react';
import { IncidentPlatformPickerCard } from '../../Common/Components/IncidentPlatformPicker/IncidentPlatformPickerCard';
import {
    InfrastructureScopeDialogs,
    ResourceGroupPickerCard,
    SubscriptionPickerCard,
} from '../../Common/Components/InfrastructureScopePicker/InfrastructureScopePicker';
import {
    AddFileCard,
    AddRepositoryCard,
    AddWebPageCard,
    KnowledgeBaseDialogs,
} from '../../Common/Components/KnowledgeBasePicker/KnowledgeBasePicker';
import { IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';
import { SreAgentContext } from '../Contracts/Context';
import { SuggestedActionsContentProps } from './SuggestedActionsCard';

const useSuggestedActionsCardContentStyles = makeStyles({
    actionCardsContainer: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
    },
});

/**
 * Determines which suggested action cards should be shown based on agent configuration.
 * Cards are hidden if their corresponding configuration is already complete.
 * Uses the same logic as useOnboardingWizard's isStepComplete function.
 */
const useVisibleCards = () => {
    const { agentObj } = useContext(SreAgentContext);
    const agent = agentObj?.properties;

    return useMemo(() => {
        const managedResources = agent?.knowledgeGraphConfiguration?.managedResources ?? [];

        // Check if any subscriptions are configured (resources starting with /subscriptions/ but not containing /resourceGroups/)
        const hasSubscriptions = managedResources.some(
            r => r.startsWith('/subscriptions/') && !r.toLowerCase().includes('/resourcegroups/')
        );

        // Check if any resource groups are configured
        const hasResourceGroups = managedResources.some(r => r.toLowerCase().includes('/resourcegroups/'));

        // Check if incident platform is configured (type is defined and not None)
        const incidentType = agent?.incidentManagementConfiguration?.type;
        const hasIncidentPlatform = incidentType !== undefined && incidentType !== IncidentManagementType.None;

        // Knowledge base cards are always shown (optional step, similar to wizard behavior)

        return {
            showSubscriptionCard: !hasSubscriptions,
            showResourceGroupCard: !hasResourceGroups,
            showIncidentPlatformCard: !hasIncidentPlatform,
            // Knowledge base cards are always visible as they're optional/additive
            showKnowledgeBaseCards: true,
        };
    }, [agent]);
};

export const SuggestedActionsContent: FC<SuggestedActionsContentProps> = ({ infrastructurePicker, knowledgeBasePicker }) => {
    const styles = useSuggestedActionsCardContentStyles();
    const { showSubscriptionCard, showResourceGroupCard, showIncidentPlatformCard, showKnowledgeBaseCards } = useVisibleCards();

    // Check if any cards are visible
    const hasVisibleCards = showSubscriptionCard || showResourceGroupCard || showIncidentPlatformCard || showKnowledgeBaseCards;

    if (!hasVisibleCards) {
        return null;
    }

    return (
        <>
            <div className={styles.actionCardsContainer}>
                {/* Infrastructure scope cards */}
                {showSubscriptionCard && <SubscriptionPickerCard picker={infrastructurePicker} />}
                {showResourceGroupCard && <ResourceGroupPickerCard picker={infrastructurePicker} />}

                {/* Incident platform card */}
                {showIncidentPlatformCard && <IncidentPlatformPickerCard />}

                {/* Knowledge base cards - always shown as they're additive */}
                {showKnowledgeBaseCards && (
                    <>
                        <AddRepositoryCard picker={knowledgeBasePicker} />
                        <AddFileCard picker={knowledgeBasePicker} />
                        <AddWebPageCard picker={knowledgeBasePicker} />
                    </>
                )}
            </div>

            {/* Dialogs (always rendered, controlled by their own open state) */}
            <InfrastructureScopeDialogs picker={infrastructurePicker} />
            <KnowledgeBaseDialogs picker={knowledgeBasePicker} />
        </>
    );
};
