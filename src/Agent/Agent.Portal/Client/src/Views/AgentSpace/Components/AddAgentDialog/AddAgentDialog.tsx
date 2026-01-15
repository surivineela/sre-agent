import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentClient } from '../../../../Common/Clients/SreAgentClient';
import { WizardDialog } from '../../../../Common/Components/Wizard/WizardDialog';
import { WizardStep } from '../../../../Common/Components/Wizard/WizardStepper';
import { TelemetrySource } from '../../../../Common/Constants/Telemetry';
import { useNotifications } from '../../../../Common/Contexts/NotificationContext';
import { useSubscriptions } from '../../../../Common/Contexts/SubscriptionsContext';
import { getArmErrorMessage } from '../../../../Common/Utilities/Client';
import { PortalResources } from '../../../../Strings/Resources';
import { useAvailableAgents } from '../../Hooks/useAvailableAgents';
import { AgentPickerGrid } from './AgentPickerGrid';
import { AgentReviewTable } from './AgentReviewTable';

interface AddAgentDialogProps {
    isOpen: boolean;
    onClose: () => void;
    spaceResourceId: string;
    spaceLocation: string;
    spaceName: string;
    maxAgentCount: number;
    currentAgentCount: number;
    onAgentsAdded: () => Promise<void>;
}

export const AddAgentDialog = ({
    isOpen,
    onClose,
    spaceResourceId,
    spaceLocation,
    spaceName,
    maxAgentCount,
    currentAgentCount,
    onAgentsAdded,
}: AddAgentDialogProps) => {
    const intl = useIntl();
    const { start, succeed, fail } = useNotifications();
    const { subscriptions } = useSubscriptions();

    const [currentStep, setCurrentStep] = useState(0);
    const [selectedAgentIds, setSelectedAgentIds] = useState<Set<string>>(new Set());
    const [isSubmitting, setIsSubmitting] = useState(false);

    const subscriptionIds = useMemo(() => subscriptions.map(sub => sub.subscriptionId), [subscriptions]);

    const subscriptionList = useMemo(
        () => subscriptions.map(sub => ({ subscriptionId: sub.subscriptionId, displayName: sub.displayName })),
        [subscriptions]
    );

    const { availableAgents, isLoading } = useAvailableAgents({
        spaceLocation,
        subscriptionIds,
        isOpen,
    });

    const sreAgentClient = useMemo(() => SreAgentClient.getInstance(TelemetrySource.AgentSpaceView), []);

    const selectedAgents = useMemo(
        () => availableAgents.filter(agent => selectedAgentIds.has(agent.id)),
        [availableAgents, selectedAgentIds]
    );

    const handleSelectionChange = useCallback((newSelection: Set<string>) => {
        setSelectedAgentIds(newSelection);
    }, []);

    const handleNext = useCallback(() => {
        setCurrentStep(prev => prev + 1);
    }, []);

    const handleBack = useCallback(() => {
        setCurrentStep(prev => prev - 1);
    }, []);

    const handleClose = useCallback(() => {
        setCurrentStep(0);
        setSelectedAgentIds(new Set());
        setIsSubmitting(false);
        onClose();
    }, [onClose]);

    const handleSubmit = useCallback(async () => {
        if (selectedAgents.length === 0) {
            return;
        }

        setIsSubmitting(true);

        const notificationId = start(
            intl.formatMessage(PortalResources.addingAgentToSpace),
            intl.formatMessage(PortalResources.addingAgentToSpaceInProgress)
        );

        let successCount = 0;
        let failedCount = 0;
        let lastError = '';

        for (const agent of selectedAgents) {
            const response = await sreAgentClient.updateAgent(agent.id, { agentSpaceId: spaceResourceId });

            if (response.isSuccessful) {
                successCount++;
            } else {
                failedCount++;
                lastError = getArmErrorMessage(response.error);
            }
        }

        if (failedCount === 0) {
            succeed(
                notificationId,
                intl.formatMessage(PortalResources.addingAgentToSpace),
                successCount === 1
                    ? intl.formatMessage(PortalResources.addAgentToSpaceSuccess, {
                          name: selectedAgents[0].name,
                          space: spaceName,
                      })
                    : intl.formatMessage(PortalResources.addAgentToSpaceSuccess, {
                          name: `${successCount} agents`,
                          space: spaceName,
                      })
            );
            await onAgentsAdded();
            handleClose();
        } else if (successCount === 0) {
            fail(notificationId, intl.formatMessage(PortalResources.addAgentToSpaceError), lastError);
        } else {
            fail(
                notificationId,
                intl.formatMessage(PortalResources.addAgentToSpaceError),
                `${successCount} succeeded, ${failedCount} failed. ${lastError}`
            );
            await onAgentsAdded();
            handleClose();
        }

        setIsSubmitting(false);
    }, [selectedAgents, sreAgentClient, spaceResourceId, spaceName, start, succeed, fail, intl, onAgentsAdded, handleClose]);

    const wizardSteps = useMemo<WizardStep[]>(
        () => [
            {
                id: 'select',
                title: intl.formatMessage(PortalResources.selectAgentsStep),
                content: (
                    <AgentPickerGrid
                        availableAgents={availableAgents}
                        selectedAgentIds={selectedAgentIds}
                        onSelectionChange={handleSelectionChange}
                        isLoading={isLoading}
                        maxAgents={maxAgentCount}
                        currentAgentCount={currentAgentCount}
                        subscriptions={subscriptionList}
                    />
                ),
            },
            {
                id: 'review',
                title: intl.formatMessage(PortalResources.reviewAgentSelection),
                content: <AgentReviewTable selectedAgents={selectedAgents} subscriptions={subscriptionList} />,
            },
        ],
        [
            intl,
            availableAgents,
            selectedAgentIds,
            handleSelectionChange,
            isLoading,
            maxAgentCount,
            currentAgentCount,
            subscriptionList,
            selectedAgents,
        ]
    );

    const isNextDisabled = useMemo(() => {
        if (currentStep === 0) {
            return selectedAgentIds.size === 0;
        }
        return isSubmitting;
    }, [currentStep, selectedAgentIds.size, isSubmitting]);

    const handleStepAction = useCallback(() => {
        if (currentStep === wizardSteps.length - 1) {
            handleSubmit();
        } else {
            handleNext();
        }
    }, [currentStep, wizardSteps.length, handleSubmit, handleNext]);

    return (
        <WizardDialog
            title={intl.formatMessage(PortalResources.addAgent)}
            steps={wizardSteps}
            isDialogOpen={isOpen}
            onClose={handleClose}
            keepRendered={true}
            currentStep={currentStep}
            onNext={handleStepAction}
            onBack={handleBack}
            isNextDisabled={isNextDisabled}
            isBackDisabled={currentStep === 0 || isSubmitting}
            isCancelDisabled={isSubmitting}
            reviewButtonText={intl.formatMessage(PortalResources.submit)}
        />
    );
};
