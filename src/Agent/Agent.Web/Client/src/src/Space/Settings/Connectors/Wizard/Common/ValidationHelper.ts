import { array, object, string } from 'yup';
import { Connector } from '../../../../../Common/Contracts/Azure/SreAgent';
import { AntUxStringComparison, equals } from '../../../../../Common/Helpers/Strings';
import { ConnectorsResources, SreAgentResources } from '../../../../../Strings/SREAgentResources';
import { AuthType } from '../ConnectorWizardFormik';
import { ConnectorType } from './ConnectorType';
import { kustoDataSourceExample } from './UrlInput';

const ENTITY_NAME_MAX_LENGTH = 64;

export const getValidationSchema = (existingConnectors: Connector[], intl: any, isEditMode?: boolean) =>
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
            .test('validateDuplicateName', intl.formatMessage(ConnectorsResources.duplicateNameError), function (name: string | undefined) {
                if (!name) return true;
                const isDuplicate = existingConnectors?.some(connector => equals(name, connector.name, AntUxStringComparison.IgnoreCase));
                return !isDuplicate;
            }),
        url: string()
            .ensure()
            .required(intl.formatMessage(SreAgentResources.fieldRequired))
            .when('connectorType', {
                is: (connectorType: string) =>
                    connectorType === ConnectorType.AzureDataExplorerQuery || connectorType === ConnectorType.AzureDataExplorerIndexing,
                then: schema =>
                    schema.test(
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
            }),
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
        teamsGroupId: string()
            .ensure()
            .when('connectorType', {
                is: (connectorType: string) => connectorType === ConnectorType.TeamsSendNotification,
                then: schema =>
                    schema.test(
                        'validateItExistsInEditMode',
                        intl.formatMessage(SreAgentResources.fieldRequired),
                        function (teamsGroupId: string | undefined) {
                            if (isEditMode) {
                                return !!teamsGroupId;
                            }
                            return true;
                        }
                    ),
                otherwise: schema => schema.notRequired(),
            }),
        channelId: string()
            .ensure()
            .when('connectorType', {
                is: (connectorType: string) => connectorType === ConnectorType.TeamsSendNotification,
                then: schema =>
                    schema.test(
                        'validateItExistsInEditMode',
                        intl.formatMessage(SreAgentResources.fieldRequired),
                        function (channelId: string | undefined) {
                            if (isEditMode) {
                                return !!channelId;
                            }
                            return true;
                        }
                    ),
                otherwise: schema => schema.notRequired(),
            }),
        authType: string()
            .ensure()
            .when('connectorType', {
                is: (connectorType: string) => connectorType === ConnectorType.McpServer || connectorType === ConnectorType.GitHub,
                then: schema => schema.required(intl.formatMessage(SreAgentResources.fieldRequired)),
                otherwise: schema => schema.notRequired(),
            }),
        patOrApiKey: string()
            .ensure()
            .when('connectorType', {
                is: (connectorType: string) => connectorType === ConnectorType.McpServer || connectorType === ConnectorType.GitHub,
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
        identity: string()
            .ensure()
            .when('connectorType', {
                is: (connectorType: string) => connectorType !== ConnectorType.McpServer && connectorType !== ConnectorType.GitHub,
                then: schema => schema.required(intl.formatMessage(SreAgentResources.fieldRequired)),
                otherwise: schema => schema.notRequired(),
            }),
    });
