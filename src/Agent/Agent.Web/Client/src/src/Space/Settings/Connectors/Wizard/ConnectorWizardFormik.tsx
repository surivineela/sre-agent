import { Formik, FormikHelpers } from 'formik';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { MsiIdentity } from '../../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../../Common/Contracts/Azure/SreAgent';
import { handleConnectorSubmit } from './Common/DialogHelper';
import { getValidationSchema } from './Common/ValidationHelper';
import { ConnectorWizard, StepKey } from './ConnectorWizard';

interface ConnectorsWizardFormikProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
    onSubmit: (dataConnector: Connector) => void;
    refreshAgent: () => void;
    agentName?: string;
    agentLocation?: string;
    agentIdentity?: MsiIdentity;
    existingConnectors?: Connector[];
}

export enum AuthType {
    BearerToken = 'BearerToken',
    CustomHeaders = 'CustomHeaders',
}

export enum McpConnectionType {
    Remote = 'Remote',
    Local = 'Local',
}

export interface KeyValuePair {
    key: string;
    value: string;
}

export interface ConnectorFormProps {
    connectorType: string;
    name: string;
    url: string;
    identity: string;
    email?: string;
    teamsChannelLink?: string;
    channelId?: string;
    teamsGroupId?: string;
    authType?: AuthType;
    patOrApiKey?: string;
    customHeaders?: KeyValuePair[];
    mcpConnectionType?: McpConnectionType;
    command?: string;
    args?: { value: string }[];
    env?: KeyValuePair[];
    // Managed Identity as FIC properties
    useManagedIdentityAsFic?: boolean;
    federatedClientId?: string;
    federatedTenantId?: string;
}

export const ConnectorWizardFormik: React.FC<ConnectorsWizardFormikProps> = props => {
    const { existingConnectors, setIsDialogOpen, onSubmit } = props;

    const intl = useIntl();

    const [currentStep, setCurrentStep] = useState<StepKey>(StepKey.ConnectorPicker);

    const initialFormValues = useMemo((): ConnectorFormProps => {
        return {
            connectorType: '',
            name: '',
            url: '',
            identity: '',
            email: '',
            teamsChannelLink: '',
            channelId: '',
            teamsGroupId: '',
            authType: undefined,
            patOrApiKey: '',
            customHeaders: [{ key: '', value: '' }],
            mcpConnectionType: McpConnectionType.Remote,
            args: [{ value: '' }],
            env: [{ key: '', value: '' }],
            useManagedIdentityAsFic: false,
            federatedClientId: '',
            federatedTenantId: '',
        };
    }, []);

    const handleSubmit = useCallback(
        async (values: ConnectorFormProps, formikHelpers: FormikHelpers<ConnectorFormProps>) => {
            await handleConnectorSubmit({
                values,
                formikHelpers,
                onSubmit,
                onClose: () => setIsDialogOpen(false),
                resetStep: () => setCurrentStep(StepKey.ConnectorPicker),
            });
        },
        [onSubmit, setIsDialogOpen]
    );

    const validationSchema = useMemo(() => getValidationSchema(existingConnectors || [], intl), [existingConnectors, intl]);

    return (
        <Formik<ConnectorFormProps>
            initialValues={initialFormValues}
            enableReinitialize={true}
            onSubmit={handleSubmit}
            validationSchema={validationSchema}
            validateOnChange={true}
        >
            <ConnectorWizard {...props} currentStep={currentStep} setCurrentStep={setCurrentStep} />
        </Formik>
    );
};
