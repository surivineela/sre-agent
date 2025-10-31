import { Link } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useCallback, useContext, useEffect, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ApiVersions } from '../../../Common/ApiVersions';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import DropdownFormik from '../../../Common/Components/Dropdown/DropdownFormik';
import { DropdownOptionBase, OptionType } from '../../../Common/Components/Dropdown/DropdownNoFormik';
import InputFormik from '../../../Common/Components/Input/InputFormik';
import { MsiIdentity } from '../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../Common/Contracts/Azure/SreAgent';
import { AntUxStringComparison, equals } from '../../../Common/Helpers/Strings';
import { ConnectorsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { IdentityKeys, IdentityType } from '../../Contracts/Identity';
import { IdentityStatus } from '../Identity.ReactView';
import { ConnectorType, getConnectorService } from './ConnectorType';
import { useConnectorWizardStyles } from './ConnectorWizard.styles';
import { ConnectorFormProps } from './ConnectorWizardFormik';

interface ConnectorWithManagedIdentityProps {
    isOperationInProgress: boolean;
    userAssignedIdentities: { id: string; name: string }[];
    refreshAgent: () => void;
    selectedConnector?: Connector;
    agentIdentity?: MsiIdentity;
    existingConnectors?: Connector[];
}

const kustoDataSourceExample = 'https://cluster-url/database-name';

export const ConnectorWithManagedIdentity: React.FC<ConnectorWithManagedIdentityProps> = props => {
    const { isOperationInProgress, userAssignedIdentities, selectedConnector, agentIdentity, existingConnectors, refreshAgent } = props;

    const intl = useIntl();
    const styles = useConnectorWizardStyles();

    const { values, setFieldValue } = useFormikContext<ConnectorFormProps>();
    const { openBlade } = useContext(AzPortalContext);
    const { resourceId } = useContext(EnvironmentContext);

    const connectorType = useMemo(() => {
        return selectedConnector ? (selectedConnector.dataConnectorType as ConnectorType) : (values.connectorType as ConnectorType);
    }, [selectedConnector, values.connectorType]);

    const validateName = useCallback(
        (name: string) => {
            if (!name) {
                return undefined;
            }

            const isDuplicate = existingConnectors?.some(connector => equals(name, connector.name, AntUxStringComparison.IgnoreCase));
            return isDuplicate ? intl.formatMessage(ConnectorsResources.duplicateNameError) : undefined;
        },
        [existingConnectors, intl]
    );

    const validateUrl = useCallback(
        (url: string, dataConnectorType: ConnectorType | undefined) => {
            if (!url || dataConnectorType !== ConnectorType.AzureDataExplorerQuery) {
                return undefined;
            }

            let isValidUri = false;
            try {
                const urlFormat = new URL(url);
                isValidUri =
                    urlFormat.protocol === 'https:' && !!urlFormat.host.trim() && !!urlFormat.pathname && urlFormat.pathname.trim() !== '/';
            } catch {
                isValidUri = false;
            }

            return !isValidUri
                ? intl.formatMessage(ConnectorsResources.urlKustoFormatError, { format: kustoDataSourceExample })
                : undefined;
        },
        [intl]
    );

    const getDataSourcePlaceholder = useCallback(
        (dataConnectorType: ConnectorType | undefined) => {
            if (dataConnectorType === ConnectorType.AzureDataExplorerQuery) {
                return kustoDataSourceExample;
            }

            return intl.formatMessage(ConnectorsResources.urlPlaceholder);
        },
        [intl]
    );

    const isSystemAssignedIdentityEnabled = useMemo(() => {
        return agentIdentity?.type.toLowerCase().includes(IdentityType.systemAssigned.toLowerCase());
    }, [agentIdentity]);

    const identityOptions = useMemo(() => {
        const options: DropdownOptionBase[] = [];
        if (isSystemAssignedIdentityEnabled) {
            options.push({ id: IdentityKeys.system, text: intl.formatMessage(SreAgentResources.systemAssigned), type: OptionType.Option });
        }

        if (userAssignedIdentities.length > 0) {
            const userAssignedIdentityChildren = userAssignedIdentities.map(option => ({
                id: option.id,
                text: option.name,
                type: OptionType.Option,
            }));
            options.push({
                id: IdentityKeys.userAssigned,
                text: intl.formatMessage(SreAgentResources.userAssigned),
                type: OptionType.OptionGroup,
                children: userAssignedIdentityChildren,
            });
        }

        return options;
    }, [intl, isSystemAssignedIdentityEnabled, userAssignedIdentities]);

    useEffect(() => {
        // Auto-select the first identity if there's only one option and no current selection
        const allSelectableOptions = [...userAssignedIdentities];
        if (isSystemAssignedIdentityEnabled) {
            allSelectableOptions.unshift({
                id: IdentityKeys.system,
                name: 'filler',
            });
        }

        if (allSelectableOptions.length === 1 && !values.identity) {
            setFieldValue('identity', allSelectableOptions[0].id);
        }
    }, [isSystemAssignedIdentityEnabled, userAssignedIdentities, values.identity, setFieldValue]);

    const openIdentityBlade = useCallback(async () => {
        const bladeClosedPromise = openBlade({
            extension: 'Microsoft_Azure_ManagedServiceIdentity',
            detailBlade: 'AzureResourceIdentitiesBladeV2',
            detailBladeInputs: {
                resourceId,
                apiVersion: ApiVersions.microsoftAppApiVersion20250501Preview,
                systemAssignedStatus: IdentityStatus.Supported,
                userAssignedStatus: IdentityStatus.Supported,
            },
        });

        await bladeClosedPromise;

        refreshAgent();
    }, [openBlade, refreshAgent, resourceId]);

    return (
        <form className={styles.form}>
            <h2 className={styles.title}>
                {intl.formatMessage(ConnectorsResources.setupTitle, { service: getConnectorService(connectorType, intl) })}
            </h2>
            <InputFormik
                name="name"
                label={intl.formatMessage(SreAgentResources.name)}
                required
                orientation="vertical"
                placeholder={intl.formatMessage(ConnectorsResources.namePlaceholder)}
                disabled={isOperationInProgress || !!selectedConnector}
                validate={validateName}
            />
            <InputFormik
                name="url"
                label={
                    connectorType
                        ? `${getConnectorService(connectorType, intl)} ${intl.formatMessage(ConnectorsResources.repositoryUrl)}`
                        : intl.formatMessage(ConnectorsResources.repositoryUrl)
                }
                required
                orientation="vertical"
                placeholder={getDataSourcePlaceholder(connectorType)}
                disabled={isOperationInProgress}
                validate={url => validateUrl(url, connectorType)}
            />
            <DropdownFormik
                name="identity"
                label={intl.formatMessage(ConnectorsResources.managedIdentity)}
                required
                value={
                    values.identity === IdentityKeys.system
                        ? intl.formatMessage(SreAgentResources.systemAssigned)
                        : userAssignedIdentities.find(option => option.id === values.identity)?.name || ''
                }
                orientation="vertical"
                options={identityOptions}
                placeholder={intl.formatMessage(ConnectorsResources.identityPlaceholder)}
                disabled={isOperationInProgress}
                sublabel={
                    <Link onClick={openIdentityBlade} className={styles.identityLink}>
                        {intl.formatMessage(SreAgentResources.addIdentity)}
                    </Link>
                }
            />
        </form>
    );
};
