import { Formik, FormikHelpers } from 'formik';
import { useCallback, useMemo, useState } from 'react';
import { MsiIdentity } from '../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../Common/Contracts/Azure/SreAgent';
import { ConnectorWizard, StepKey } from './ConnectorWizard';

interface ConnectorsWizardFormikProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
    onSubmit: (dataConnector: Connector) => void;
    refreshAgent: () => void;
    selectedConnector?: Connector;
    agentName?: string;
    agentLocation?: string;
    agentIdentity?: MsiIdentity;
    existingDataConnectors?: Connector[];
}

export interface ConnectorFormProps {
    connectorType: string;
    name: string;
    url: string;
    identity: string;
    email?: string;
}

export const ConnectorWizardFormik: React.FC<ConnectorsWizardFormikProps> = props => {
    const { selectedConnector, setIsDialogOpen, onSubmit } = props;

    const [currentStep, setCurrentStep] = useState<StepKey>(StepKey.ConnectorPicker);

    const initialFormValues = useMemo((): ConnectorFormProps => {
        if (selectedConnector) {
            return {
                connectorType: selectedConnector.dataConnectorType,
                name: selectedConnector.name,
                url: selectedConnector.dataSource || '',
                identity: selectedConnector.identity,
            };
        }
        return {
            connectorType: '',
            name: '',
            url: '',
            identity: '',
        };
    }, [selectedConnector]);

    const handleSubmit = useCallback(
        async (values: ConnectorFormProps, formikHelpers: FormikHelpers<ConnectorFormProps>) => {
            const dataConnector: Connector = {
                name: values.name,
                dataConnectorType: values.connectorType?.toString() || '',
                dataSource: values.url,
                identity: values.identity,
            };
            setIsDialogOpen(false);
            formikHelpers.resetForm();
            setCurrentStep(StepKey.ConnectorPicker);
            onSubmit(dataConnector);
        },
        [onSubmit, setIsDialogOpen]
    );

    return (
        <Formik<ConnectorFormProps> initialValues={initialFormValues} enableReinitialize={true} onSubmit={handleSubmit}>
            <ConnectorWizard {...props} currentStep={currentStep} setCurrentStep={setCurrentStep} />
        </Formik>
    );
};
