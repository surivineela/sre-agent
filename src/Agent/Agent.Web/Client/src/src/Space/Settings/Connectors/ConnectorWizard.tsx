import { useFormikContext } from 'formik';
import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { WizardDialog } from '../../../Common/Components/Wizard/WizardDialogFormik';
import { StepStatus } from '../../../Common/Components/Wizard/WizardStepper';
import { MsiIdentity } from '../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../Common/Contracts/Azure/SreAgent';
import { ConnectorsResources } from '../../../Strings/SREAgentResources';
import { ConnectorPicker } from './ConnectorPicker';
import { ConnectorWithManagedIdentity } from './ConnectorWithManagedIdentity';
import { useConnectorWizardStyles } from './ConnectorWizard.styles';
import { ConnectorFormProps } from './ConnectorWizardFormik';
import { ReviewAndAdd } from './ReviewAndAdd';

interface ConnectorsWizardProps {
    isOperationInProgress: boolean;
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
    currentStep: StepKey;
    setCurrentStep: (step: StepKey) => void;
    refreshAgent: () => void;
    agentIdentity?: MsiIdentity;
    existingConnectors?: Connector[];
    selectedConnector?: Connector;
}

export enum StepKey {
    ConnectorPicker = 0,
    Setup = 1,
    ReviewAndAdd = 2,
}

export const ConnectorWizard: React.FC<ConnectorsWizardProps> = props => {
    const {
        isOperationInProgress,
        isDialogOpen,
        setIsDialogOpen,
        agentIdentity,
        existingConnectors,
        selectedConnector,
        currentStep,
        setCurrentStep,
        refreshAgent,
    } = props;
    const intl = useIntl();
    const styles = useConnectorWizardStyles();
    const { values, isValid, dirty, resetForm } = useFormikContext<ConnectorFormProps>();

    const title = useMemo(() => {
        return intl.formatMessage(ConnectorsResources.addAConnector);
    }, [intl]);

    const getStatus = useCallback(
        (step: StepKey): StepStatus => {
            if (step < currentStep) {
                return StepStatus.Completed;
            } else if (step === currentStep) {
                return StepStatus.Active;
            } else {
                return StepStatus.Pending;
            }
        },
        [currentStep]
    );

    const steps = useMemo(() => {
        return [
            {
                id: StepKey.ConnectorPicker,
                title: intl.formatMessage(ConnectorsResources.chooseAConnector),
                status: getStatus(StepKey.ConnectorPicker),
            },
            { id: StepKey.Setup, title: intl.formatMessage(ConnectorsResources.setUpConnector), status: getStatus(StepKey.Setup) },
            {
                id: StepKey.ReviewAndAdd,
                title: intl.formatMessage(ConnectorsResources.reviewAndCreate),
                status: getStatus(StepKey.ReviewAndAdd),
            },
        ];
    }, [getStatus, intl]);

    const isNextDisabled = useMemo(() => {
        if (currentStep === StepKey.ConnectorPicker) {
            return !values.connectorType;
        } else if (currentStep === StepKey.Setup) {
            return !isValid || !dirty;
        }
        return false;
    }, [currentStep, values, isValid, dirty]);

    const onCancel = useCallback(() => {
        resetForm();
        setCurrentStep(StepKey.ConnectorPicker);
    }, [resetForm, setCurrentStep]);

    const userAssignedIdentityOptions = useMemo(() => {
        const userAssignedOptions: { id: string; name: string }[] = [];

        const userAssignedIdentityRscIds = agentIdentity?.userAssignedIdentities ? Object.keys(agentIdentity.userAssignedIdentities) : [];
        if (userAssignedIdentityRscIds.length > 0) {
            userAssignedIdentityRscIds.forEach(resourceId => {
                const parts = resourceId.split('/');
                const name = parts[parts.length - 1] || resourceId;
                userAssignedOptions.push({
                    id: resourceId,
                    name: name,
                });
            });
        }

        return userAssignedOptions;
    }, [agentIdentity]);

    return (
        <WizardDialog
            title={title}
            steps={steps}
            isDialogOpen={isDialogOpen}
            setIsDialogOpen={setIsDialogOpen}
            isNextDisabled={isNextDisabled}
            currentStep={currentStep}
            setCurrentStep={setCurrentStep}
            reviewButtonText={intl.formatMessage(ConnectorsResources.addConnector)}
            onCancel={onCancel}
        >
            <div className={styles.wizardContentContainer}>
                {currentStep === StepKey.ConnectorPicker && <ConnectorPicker />}
                {currentStep === StepKey.Setup && (
                    <ConnectorWithManagedIdentity
                        isOperationInProgress={isOperationInProgress}
                        userAssignedIdentities={userAssignedIdentityOptions}
                        agentIdentity={agentIdentity}
                        existingConnectors={existingConnectors}
                        selectedConnector={selectedConnector}
                        refreshAgent={refreshAgent}
                    />
                )}
                {currentStep === StepKey.ReviewAndAdd && <ReviewAndAdd userAssignedIdentities={userAssignedIdentityOptions} />}
            </div>
        </WizardDialog>
    );
};
