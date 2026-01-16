import { Formik, useFormikContext } from 'formik';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { WizardDialog } from '../../../Common/Components/Wizard/WizardDialog';
import { WizardStep } from '../../../Common/Components/Wizard/WizardStepper';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { AmplitudeContextProvider } from '../../../Common/Contexts/AmplitudeContext';
import { AgentSpaceCreateFormValues } from '../../../Common/Contracts/AgentSpace';
import { ProductName } from '../../../Common/Contracts/Amplitude';
import { useDeployment } from '../../../Common/Hooks/useDeployment';
import { usePersistentNavigate } from '../../../Common/Hooks/usePersistentNavigate';
import { DeployResources, PortalResources } from '../../../Strings/Resources';
import { AgentSpaceBasics } from './AgentSpaceBasics';
import { AgentSpaceDeploy } from './AgentSpaceDeploy';
import { AgentSpaceReview } from './AgentSpaceReview';
import { GenevaActionPoliciesStep } from './GenevaActionPoliciesStep';
import { useAgentSpaceCreate } from './useAgentSpaceCreate';

interface CreateAgentSpaceDialogProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
    onCreated?: () => void;
}

export const CreateAgentSpaceDialog = ({ isDialogOpen, setIsDialogOpen, onCreated }: CreateAgentSpaceDialogProps) => {
    const [currentStepIndex, setCurrentStepIndex] = useState(0);

    const handleDeploymentStarted = useCallback(() => {
        setCurrentStepIndex(3); // Go to deploy step
    }, []);

    const { onSubmit, validationSchema, initialValues, isDeploying, deploymentResourceId, agentSpaceResourceId } = useAgentSpaceCreate({
        onDeploymentStarted: handleDeploymentStarted,
    });

    return (
        <AmplitudeContextProvider resourceId="" productName={ProductName.SreAgent} telemetrySource={TelemetrySource.AgentSpaceCreate}>
            <Formik<AgentSpaceCreateFormValues>
                initialValues={initialValues}
                onSubmit={onSubmit}
                validationSchema={validationSchema}
                validateOnBlur={false}
                enableReinitialize
            >
                <InnerCreateAgentSpaceDialog
                    isDialogOpen={isDialogOpen}
                    setIsDialogOpen={setIsDialogOpen}
                    isDeploying={isDeploying}
                    currentStepIndex={currentStepIndex}
                    setCurrentStepIndex={setCurrentStepIndex}
                    deploymentResourceId={deploymentResourceId}
                    agentSpaceResourceId={agentSpaceResourceId}
                    onCreated={onCreated}
                />
            </Formik>
        </AmplitudeContextProvider>
    );
};

interface InnerCreateAgentSpaceDialogProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
    isDeploying: boolean;
    currentStepIndex: number;
    setCurrentStepIndex: (index: number) => void;
    deploymentResourceId: string;
    agentSpaceResourceId: string;
    onCreated?: () => void;
}

const InnerCreateAgentSpaceDialog = ({
    isDialogOpen,
    setIsDialogOpen,
    isDeploying,
    currentStepIndex,
    setCurrentStepIndex,
    deploymentResourceId,
    agentSpaceResourceId,
    onCreated,
}: InnerCreateAgentSpaceDialogProps) => {
    const intl = useIntl();
    const navigate = usePersistentNavigate();
    const { values, errors, submitForm } = useFormikContext<AgentSpaceCreateFormValues>();

    const { deploymentSucceeded } = useDeployment(deploymentResourceId, currentStepIndex === 3, TelemetrySource.AgentSpaceCreate);

    const wizardSteps = useMemo<WizardStep[]>(
        () => [
            {
                id: 'basics',
                title: intl.formatMessage(PortalResources.basics),
                content: <AgentSpaceBasics isDeploying={isDeploying} />,
            },
            {
                id: 'genevaActionPolicies',
                title: intl.formatMessage(PortalResources.genevaActionPolicies),
                content: <GenevaActionPoliciesStep isDeploying={isDeploying} />,
            },
            {
                id: 'review',
                title: intl.formatMessage(PortalResources.reviewAndCreate),
                content: <AgentSpaceReview isDeploying={isDeploying} />,
            },
            {
                id: 'deploy',
                title: intl.formatMessage(DeployResources.deploy),
                content: <AgentSpaceDeploy deploymentResourceId={deploymentResourceId} />,
            },
        ],
        [intl, isDeploying, deploymentResourceId]
    );

    const isBasicsStep = currentStepIndex === 0;
    const isReviewStep = currentStepIndex === 2;
    const isDeployStep = currentStepIndex === 3;

    const nextButtonText = useMemo(() => {
        if (isReviewStep) {
            return intl.formatMessage(PortalResources.create);
        }
        return intl.formatMessage(PortalResources.next);
    }, [isReviewStep, intl]);

    const cancelButtonText = useMemo(() => {
        if (isDeployStep) {
            return intl.formatMessage(PortalResources.close);
        }
        return undefined;
    }, [intl, isDeployStep]);

    const createButtonDisabled = useMemo(() => {
        return (
            !values.name ||
            !values.resourceGroupId ||
            !values.subscriptionId ||
            !values.location ||
            Object.keys(errors).length > 0 ||
            isDeploying
        );
    }, [errors, isDeploying, values]);

    const isNextDisabled = useMemo(() => {
        if (isBasicsStep) {
            const basicsFieldErrors = ['subscriptionId', 'resourceGroupId', 'name', 'location'].some(
                field => errors[field as keyof typeof errors]
            );
            const basicsRequiredFieldsMissing = !values.subscriptionId || !values.resourceGroupId || !values.name || !values.location;
            return basicsFieldErrors || basicsRequiredFieldsMissing;
        }

        if (isReviewStep) {
            return createButtonDisabled;
        }

        if (isDeployStep) {
            return !deploymentSucceeded;
        }

        return false;
    }, [isBasicsStep, isDeployStep, isReviewStep, deploymentSucceeded, createButtonDisabled, errors, values]);

    const handleNext = useCallback(() => {
        if (isDeployStep) {
            onCreated?.();
            navigate(`/spaces/${encodeURIComponent(agentSpaceResourceId)}`);
            setIsDialogOpen(false);
        } else if (isReviewStep) {
            submitForm();
        } else {
            setCurrentStepIndex(currentStepIndex + 1);
        }
    }, [
        isDeployStep,
        isReviewStep,
        currentStepIndex,
        navigate,
        agentSpaceResourceId,
        setCurrentStepIndex,
        submitForm,
        setIsDialogOpen,
        onCreated,
    ]);

    const handleClose = useCallback(() => {
        setIsDialogOpen(false);
    }, [setIsDialogOpen]);

    return (
        <WizardDialog
            isDialogOpen={isDialogOpen}
            onClose={handleClose}
            title={intl.formatMessage(PortalResources.createAgentSpace)}
            steps={wizardSteps}
            currentStep={currentStepIndex}
            nextButtonText={nextButtonText}
            reviewButtonText={isDeployStep ? intl.formatMessage(PortalResources.goToAgentSpace) : undefined}
            isNextDisabled={isNextDisabled}
            onNext={handleNext}
            cancelButtonText={cancelButtonText}
            isBackDisabled={isDeployStep}
            isCancelDisabled={isDeploying}
            onBack={() => setCurrentStepIndex(currentStepIndex - 1)}
            keepRendered
        />
    );
};
