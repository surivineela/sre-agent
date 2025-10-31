import { Formik, useFormikContext } from 'formik';
import { useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { WizardDialog } from '../../../Common/Components/Wizard/WizardDialog';
import { WizardStep } from '../../../Common/Components/Wizard/WizardStepper';
import { ResourceGroup } from '../../../Common/Contracts/Arm';
import { AgentAccessLevel, AgentMode } from '../../../Common/Contracts/SreAgent';
import { PortalResources } from '../../../Strings/Resources';
import { AgentPermissions } from './AgentPermissions';
import { Basics } from './Basics';
import { ManagedResourceGroups } from './ManagedResourceGroups';
import { Review } from './Review';
import { useSreAgentCreate } from './useSreAgentCreate';

// NOTE: Current flow:
// 1. Create context pane closes on Create
// 2. Notification polls on agent browse
// 3. Goes to deployment blade once deployment successfully submits

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
}

interface CreateAgentDialogProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
}

export const CreateAgentDialog = (props: CreateAgentDialogProps) => {
    const { isDialogOpen, setIsDialogOpen } = props;

    const {
        onSubmit,
        validationSchema,
        initialValues,
        isDeploying,
        setIsDeploying,
        deploymentSucceeded,
        setDeploymentSucceeded,
        permissionsLoading,
    } = useSreAgentCreate({
        showRegistrationDialog: () => Promise.resolve(true),
        agentSpaceId: '',
        agentSpaceLocation: undefined,
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
                setIsDeploying={setIsDeploying}
                permissionsLoading={permissionsLoading}
                deploymentSucceeded={deploymentSucceeded}
                setDeploymentSucceeded={setDeploymentSucceeded}
                agentSpaceLocation={undefined}
            />
        </Formik>
    );
};

interface InnerCreateAgentDialogProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
    isDeploying: boolean;
    setIsDeploying: (isDeploying: boolean) => void;
    permissionsLoading: boolean;
    deploymentSucceeded: boolean;
    setDeploymentSucceeded: (succeeded: boolean) => void;
    agentSpaceLocation?: string;
}

const InnerCreateAgentDialog = (props: InnerCreateAgentDialogProps) => {
    const {
        isDialogOpen,
        setIsDialogOpen,
        isDeploying,
        setIsDeploying,
        permissionsLoading,
        deploymentSucceeded,
        setDeploymentSucceeded,
        agentSpaceLocation,
    } = props;

    const intl = useIntl();
    const { values, errors, resetForm, submitForm } = useFormikContext<SreAgentCreateFormProps>();

    const [currentStepIndex, setCurrentStepIndex] = useState(0);

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
        ],
        [intl, isDeploying, agentSpaceLocation]
    );

    const isLastStep = useMemo(() => currentStepIndex === wizardSteps.length - 1, [currentStepIndex, wizardSteps.length]);

    useEffect(() => {
        if (deploymentSucceeded) {
            resetForm();
            setDeploymentSucceeded(false);
            setCurrentStepIndex(0);
        }
    }, [deploymentSucceeded, resetForm, setDeploymentSucceeded]);

    return (
        <WizardDialog
            isDialogOpen={isDialogOpen}
            onClose={() => {
                setCurrentStepIndex(0);
                resetForm();
                setIsDialogOpen(false);
                setIsDeploying(false);
            }}
            title={intl.formatMessage(PortalResources.createAgent)}
            steps={wizardSteps}
            currentStep={currentStepIndex}
            reviewButtonText={intl.formatMessage(PortalResources.create)}
            isNextDisabled={isLastStep ? createButtonDisabled : false}
            onNext={() => (isLastStep ? submitForm() : setCurrentStepIndex(currentStepIndex + 1))}
            onBack={() => setCurrentStepIndex(currentStepIndex - 1)}
            keepRendered
        />
    );
};
