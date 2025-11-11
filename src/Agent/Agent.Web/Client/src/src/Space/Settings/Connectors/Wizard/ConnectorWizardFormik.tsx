import { Formik, FormikHelpers } from 'formik';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { array, object, string } from 'yup';
import { MsiIdentity } from '../../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../../Common/Contracts/Azure/SreAgent';
import { AntUxStringComparison, equals } from '../../../../Common/Helpers/Strings';
import { ConnectorsResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ConnectorType } from './Common/ConnectorType';
import { getBearerTokenConnectionString, getCustomHeadersConnectionString } from './Common/CustomConnectorHelper';
import { parseTeamsChannelLink } from './Common/TeamsConnectorHelper';
import { kustoDataSourceExample } from './Common/UrlInputWithValidation';
import { ConnectorWizard, StepKey } from './ConnectorWizard';

const ENTITY_NAME_MAX_LENGTH = 64;

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
    teamsChannelLink?: string;
    authType?: AuthType;
    patOrApiKey?: string;
    customHeaders?: CustomHeader[];
}

export const ConnectorWizardFormik: React.FC<ConnectorsWizardFormikProps> = props => {
    const { selectedConnector, existingConnectors, setIsDialogOpen, onSubmit } = props;

    const intl = useIntl();

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
                if (connectorType === ConnectorType.TeamsSendNotification) {
                    const teamsInfo = parseTeamsChannelLink(values.teamsChannelLink || '');
                    dataSource = `${values.url};${teamsInfo?.teamsGroupId};${teamsInfo?.channelId}`;
                } else {
                    dataSource = values.url;
                }
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

    const validationSchema = useMemo(
        () =>
            object({
                connectorType: string().ensure().required(),
                name: string()
                    .ensure()
                    .required(intl.formatMessage(SreAgentResources.fieldRequired))
                    .test(
                        'validateNameFormat',
                        intl.formatMessage(ConnectorsResources.connectorNameValidationMessage, {
                            maxLength: ENTITY_NAME_MAX_LENGTH,
                        }),
                        function (name: string | undefined) {
                            if (!name) return true;
                            // Name must start with a letter and can only contain letters, numbers, and hyphens
                            // Must be non-empty and less than 64 characters
                            return /^[a-zA-Z][a-zA-Z0-9-]{3,63}$/.test(name);
                        }
                    )
                    .test(
                        'validateDuplicateName',
                        intl.formatMessage(ConnectorsResources.duplicateNameError),
                        function (name: string | undefined) {
                            if (!name) return true;
                            const isDuplicate = existingConnectors?.some(connector =>
                                equals(name, connector.name, AntUxStringComparison.IgnoreCase)
                            );
                            return !isDuplicate;
                        }
                    ),
                url: string()
                    .ensure()
                    .required(intl.formatMessage(SreAgentResources.fieldRequired))
                    .test(
                        'validateUrlFormat',
                        intl.formatMessage(ConnectorsResources.urlKustoFormatError, { format: kustoDataSourceExample }),
                        function (url: string | undefined) {
                            if (!url) return true;

                            let isValidUri = false;
                            try {
                                const urlFormat = new URL(url);
                                isValidUri =
                                    urlFormat.protocol === 'https:' &&
                                    !!urlFormat.host.trim() &&
                                    !!urlFormat.pathname &&
                                    urlFormat.pathname.trim() !== '/';
                            } catch {
                                isValidUri = false;
                            }

                            return isValidUri;
                        }
                    ),
                email: string()
                    .ensure()
                    .when('connectorType', {
                        is: (connectorType: string) =>
                            connectorType === ConnectorType.OutlookSendEmail || connectorType === ConnectorType.TeamsSendNotification,
                        then: schema => schema.required(intl.formatMessage(SreAgentResources.fieldRequired)),
                        otherwise: schema => schema.notRequired(),
                    }),
                teamsChannelLink: string()
                    .ensure()
                    .when('connectorType', {
                        is: (connectorType: string) => connectorType === ConnectorType.TeamsSendNotification,
                        then: schema =>
                            schema
                                .required(intl.formatMessage(SreAgentResources.fieldRequired))
                                .test(
                                    'validateChannelLink',
                                    intl.formatMessage(ConnectorsResources.provideChannelLinkError),
                                    function (link: string | undefined) {
                                        if (!link) return true;
                                        return link.includes('/channel/');
                                    }
                                ),
                        otherwise: schema => schema.notRequired(),
                    }),
                authType: string()
                    .ensure()
                    .when('connectorType', {
                        is: (connectorType: string) => connectorType === ConnectorType.McpServer,
                        then: schema => schema.required(intl.formatMessage(SreAgentResources.fieldRequired)),
                        otherwise: schema => schema.notRequired(),
                    }),
                patOrApiKey: string()
                    .ensure()
                    .when('connectorType', {
                        is: (connectorType: string) => connectorType === ConnectorType.McpServer,
                        then: schema =>
                            schema.when('authType', {
                                is: (authType: string) => authType === AuthType.BearerToken,
                                then: schema => schema.required(intl.formatMessage(SreAgentResources.fieldRequired)),
                                otherwise: schema => schema.notRequired(),
                            }),
                        otherwise: schema => schema.notRequired(),
                    }),
                customHeaders: array()
                    .ensure()
                    .when('connectorType', {
                        is: (connectorType: string) => connectorType === ConnectorType.McpServer,
                        then: schema =>
                            schema.when('authType', {
                                is: (authType: string) => authType === AuthType.CustomHeaders,
                                then: schema => schema.required(intl.formatMessage(SreAgentResources.fieldRequired)),
                                otherwise: schema => schema.notRequired(),
                            }),
                        otherwise: schema => schema.notRequired(),
                    }),
                identity: string().ensure().required(intl.formatMessage(SreAgentResources.fieldRequired)),
            }),
        [intl, existingConnectors]
    );

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
