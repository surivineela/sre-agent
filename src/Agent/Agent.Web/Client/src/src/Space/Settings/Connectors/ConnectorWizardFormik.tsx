import { Formik, FormikHelpers } from 'formik';
import { useCallback, useMemo, useState } from 'react';
import { MsiIdentity } from '../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../Common/Contracts/Azure/SreAgent';
import { ConnectorWizard, StepKey } from './ConnectorWizard';
import { ConnectorType } from './Wizard/Common/ConnectorType';
import { getBearerTokenConnectionString, getCustomHeadersConnectionString } from './Wizard/Common/CustomConnector';

interface ConnectorsWizardFormikProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
    onSubmit: (dataConnector: Connector) => void;
    refreshAgent: () => void;
    selectedConnector?: Connector;
    agentName?: string;
    agentLocation?: string;
    agentIdentity?: MsiIdentity;
    existingConnectors?: Connector[];
}

export enum AuthType {
    BearerToken = 'BearerToken',
    CustomHeaders = 'CustomHeaders',
}

export interface CustomHeader {
    key: string;
    value: string;
}

export interface ConnectorFormProps {
    connectorType: string;
    name: string;
    url: string;
    identity: string;
    email?: string;
    authType?: AuthType;
    patOrApiKey?: string;
    customHeaders?: CustomHeader[];
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
                customHeaders: [{ key: '', value: '' }],
            };
        }
        return {
            connectorType: '',
            name: '',
            url: '',
            identity: '',
            customHeaders: [{ key: '', value: '' }],
        };
    }, [selectedConnector]);

    const handleSubmit = useCallback(
        async (values: ConnectorFormProps, formikHelpers: FormikHelpers<ConnectorFormProps>) => {
            const connectorType = values.connectorType as ConnectorType;

            let dataSource: string;
            if (connectorType !== ConnectorType.McpServer) {
                dataSource = values.url;
            } else {
                if (values.authType === AuthType.BearerToken) {
                    dataSource = getBearerTokenConnectionString(values.url, values.patOrApiKey || '');
                } else {
                    dataSource = getCustomHeadersConnectionString(values.url, values.customHeaders || []);
                }
            }

            const dataConnector: Connector = {
                name: values.name,
                dataConnectorType: values.connectorType?.toString() || '',
                dataSource: dataSource,
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
