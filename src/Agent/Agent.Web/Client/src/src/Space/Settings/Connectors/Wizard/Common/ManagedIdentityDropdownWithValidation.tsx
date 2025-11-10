import { Link } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useCallback, useContext, useEffect, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ApiVersions } from '../../../../../Common/ApiVersions';
import { AzPortalContext } from '../../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import DropdownFormik from '../../../../../Common/Components/Dropdown/DropdownFormik';
import { DropdownOptionBase, OptionType } from '../../../../../Common/Components/Dropdown/DropdownNoFormik';
import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { ConnectorsResources, SreAgentResources } from '../../../../../Strings/SREAgentResources';
import { IdentityKeys, IdentityType } from '../../../../Contracts/Identity';
import { IdentityStatus } from '../../../Identity.ReactView';
import { useConnectorWizardStyles } from '../../ConnectorWizard.styles';
import { ConnectorFormProps } from '../../ConnectorWizardFormik';

interface ManagedIdentityDropdownWithValidationProps {
    userAssignedIdentities: { id: string; name: string }[];
    agentIdentity: MsiIdentity | undefined;
    refreshAgent: () => void;
}

export const ManagedIdentityDropdownWithValidation: React.FC<ManagedIdentityDropdownWithValidationProps> = props => {
    const { userAssignedIdentities, agentIdentity, refreshAgent } = props;

    const intl = useIntl();
    const styles = useConnectorWizardStyles();

    const { values, setFieldValue } = useFormikContext<ConnectorFormProps>();
    const { openBlade } = useContext(AzPortalContext);
    const { resourceId } = useContext(EnvironmentContext);

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
    );
};
