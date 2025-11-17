import { Formik, useFormikContext } from 'formik';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { WizardDialog } from '../../../Common/Components/Wizard/WizardDialog';
import { WizardStep } from '../../../Common/Components/Wizard/WizardStepper';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { ResourceGroup } from '../../../Common/Contracts/Arm';
import { AgentAccessLevel, AgentMode } from '../../../Common/Contracts/SreAgent';
import { useDeployment } from '../../../Common/Hooks/useDeployment';
import { usePersistentNavigate } from '../../../Common/Hooks/usePersistentNavigate';
import { DeployResources, PortalResources } from '../../../Strings/Resources';
import { AgentPermissions } from './AgentPermissions';
import { Basics } from './Basics';
import { Deploy } from './Deploy';
import { ManagedResourceGroups } from './ManagedResourceGroups';
import { Review } from './Review';
import { useSreAgentCreate } from './useSreAgentCreate';

export enum ApplicationInsightsSetup {
    New = 'new',
    Existing = 'existing',
}

export interface SreAgentCreateFormProps {
    subscriptionId: string;
    resourceGroupId: string;
    isResourceGroupNew: boolean;
    name: string;
    location: string;
    managedResourceGroups: ResourceGroup[];
    managedResourceGroupsPermissionError: boolean;
    maxResourceGroupsError: boolean;
    managedResourceGroupsLockError: boolean;
    managedResourceGroupsDenyAssignmentError: boolean;
    managedResourceGroupsPolicyError: boolean;
    mode: AgentMode;
    permissionsLevel: AgentAccessLevel;
    agentSpaceId: string;
    createNewAppInsights: ApplicationInsightsSetup;
    existingAppInsightsId: string;
    appInsightsSubscriptionId: string;
}

interface CreateAgentDialogProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
}

export const CreateAgentDialog = (props: CreateAgentDialogProps) => {
    const { isDialogOpen, setIsDialogOpen } = props;
    const [currentStepIndex, setCurrentStepIndex] = useState(0);

    const handleDeploymentStarted = useCallback(() => {
        setCurrentStepIndex(4);
    }, []);

    const { onSubmit, validationSchema, initialValues, isDeploying, permissionsLoading, deploymentResourceId, agentResourceId } =
        useSreAgentCreate({
            showRegistrationDialog: () => Promise.resolve(true),
            agentSpaceId: '',
            agentSpaceLocation: undefined,
            onDeploymentStarted: handleDeploymentStarted,
        });

    return (
        <Formik<SreAgentCreateFormProps>
            initialValues={initialValues}
            onSubmit={onSubmit}
            validationSchema={validationSchema}
            validateOnBlur={false}
            enableReinitialize
        >
            <InnerCreateAgentDialog
                isDialogOpen={isDialogOpen}
                setIsDialogOpen={setIsDialogOpen}
                isDeploying={isDeploying}
                permissionsLoading={permissionsLoading}
                agentSpaceLocation={undefined}
                currentStepIndex={currentStepIndex}
                setCurrentStepIndex={setCurrentStepIndex}
                deploymentResourceId={deploymentResourceId}
                agentResourceId={agentResourceId}
            />
        </Formik>
    );
};

interface InnerCreateAgentDialogProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
    isDeploying: boolean;
    permissionsLoading: boolean;
    agentSpaceLocation?: string;
    currentStepIndex: number;
    setCurrentStepIndex: (index: number) => void;
    deploymentResourceId: string;
    agentResourceId: string;
}

const InnerCreateAgentDialog = (props: InnerCreateAgentDialogProps) => {
    const {
        isDialogOpen,
        setIsDialogOpen,
        isDeploying,
        permissionsLoading,
        agentSpaceLocation,
        currentStepIndex,
        setCurrentStepIndex,
        deploymentResourceId,
        agentResourceId,
    } = props;

    const intl = useIntl();
    const navigate = usePersistentNavigate();
    const { values, errors, submitForm } = useFormikContext<SreAgentCreateFormProps>();

    const { deploymentSucceeded } = useDeployment(deploymentResourceId, currentStepIndex === 4, TelemetrySource.SreAgentCreate);

    const createButtonDisabled = useMemo<boolean>(() => {
        return (
            values.managedResourceGroupsPermissionError ||
            values.maxResourceGroupsError ||
            values.managedResourceGroupsLockError ||
            !values.name ||
            !values.resourceGroupId ||
            !values.subscriptionId ||
            Object.keys(errors).length > 0 ||
            isDeploying ||
            permissionsLoading
        );
    }, [errors, isDeploying, values, permissionsLoading]);

    const wizardSteps = useMemo<WizardStep[]>(
        () => [
            {
                id: 'basics',
                title: intl.formatMessage(PortalResources.basics),
                content: <Basics isDeploying={isDeploying} agentSpaceLocation={agentSpaceLocation} />,
            },
            {
                id: 'managedResourceGroups',
                title: intl.formatMessage(PortalResources.managedResourceGroups),
                content: <ManagedResourceGroups />,
            },
            {
                id: 'agentPermissions',
                title: intl.formatMessage(PortalResources.agentPermissions),
                content: <AgentPermissions isDeploying={isDeploying} />,
            },
            {
                id: 'review',
                title: intl.formatMessage(PortalResources.review),
                content: <Review isDeploying={isDeploying} />,
            },
            {
                id: 'deploy',
                title: intl.formatMessage(DeployResources.deploy),
                content: <Deploy deploymentResourceId={deploymentResourceId} />,
            },
        ],
        [intl, isDeploying, agentSpaceLocation, deploymentResourceId]
    );

    const isBasicsStep = useMemo(() => currentStepIndex === 0, [currentStepIndex]);
    const isReviewStep = useMemo(() => currentStepIndex === 3, [currentStepIndex]);
    const isDeployStep = useMemo(() => currentStepIndex === 4, [currentStepIndex]);

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
            navigate(`/agents/${encodeURIComponent(agentResourceId)}`);
        } else if (isReviewStep) {
            submitForm();
        } else {
            setCurrentStepIndex(currentStepIndex + 1);
        }
    }, [isDeployStep, isReviewStep, currentStepIndex, navigate, agentResourceId, setCurrentStepIndex, submitForm]);

    return (
        <WizardDialog
            isDialogOpen={isDialogOpen}
            onClose={() => {
                setIsDialogOpen(false);
            }}
            title={intl.formatMessage(PortalResources.createAgent)}
            steps={wizardSteps}
            currentStep={currentStepIndex}
            reviewButtonText={intl.formatMessage(PortalResources.chatWithAgent)}
            nextButtonText={nextButtonText}
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
