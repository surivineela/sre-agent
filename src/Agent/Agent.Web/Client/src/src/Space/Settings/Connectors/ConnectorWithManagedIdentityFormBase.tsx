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

export interface ConnectorWithManagedIdentityFormBaseProps {
    userAssignedIdentities: { id: string; name: string }[];
    refreshAgent: () => void;
    selectedConnector?: Connector;
    agentIdentity?: MsiIdentity;
    existingConnectors?: Connector[];
    children?: React.ReactNode;
}

export const ConnectorWithManagedIdentityFormBase: React.FC<ConnectorWithManagedIdentityFormBaseProps> = props => {
    const { userAssignedIdentities, selectedConnector, agentIdentity, existingConnectors, children, refreshAgent } = props;

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
                disabled={!!selectedConnector}
                validate={validateName}
            />
            {children}
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
                sublabel={
                    <Link onClick={openIdentityBlade} className={styles.identityLink}>
                        {intl.formatMessage(SreAgentResources.addIdentity)}
                    </Link>
                }
            />
        </form>
    );
};
