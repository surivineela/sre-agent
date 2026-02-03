import { makeStyles, tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useMemo } from 'react';
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
import { AgentFormValues } from '../../Common/Utils/AgentFormUtils';
import { SuggestedActionsContentProps } from './SuggestedActionsCard';

const useSuggestedActionsCardContentStyles = makeStyles({
    actionCardsContainer: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
    },
});

export const SuggestedActionsContent: FC<SuggestedActionsContentProps> = ({
    infrastructurePicker,
    incidentPlatformPicker,
    knowledgeBasePicker,
    isSubscriptionConfigured,
    isResourceGroupConfigured,
}) => {
    const styles = useSuggestedActionsCardContentStyles();

    const { initialValues } = useFormikContext<AgentFormValues>();

    const { showSubscriptionCard, showResourceGroupCard, showIncidentPlatformCard, showKnowledgeBaseCards } = useMemo(() => {
        return {
            showSubscriptionCard: initialValues.selectedSubscriptionIds.length === 0 && !isSubscriptionConfigured,
            showResourceGroupCard: initialValues.selectedResourceGroupIds.length === 0 && !isResourceGroupConfigured,
            showIncidentPlatformCard:
                (initialValues.incidentPlatformType === undefined || initialValues.incidentPlatformType === IncidentManagementType.None) &&
                !incidentPlatformPicker.isIncidentPlatformConfigured,
            showKnowledgeBaseCards: true,
        };
    }, [initialValues, isSubscriptionConfigured, isResourceGroupConfigured, incidentPlatformPicker.isIncidentPlatformConfigured]);

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
                {showIncidentPlatformCard && <IncidentPlatformPickerCard picker={incidentPlatformPicker} />}

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
