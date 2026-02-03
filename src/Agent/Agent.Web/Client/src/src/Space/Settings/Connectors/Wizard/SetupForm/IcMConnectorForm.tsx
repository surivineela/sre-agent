import { useFormikContext } from 'formik';
import { FC, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { KeyVault, KeyVaultClient } from '../../../../../Common/Clients/KeyVaultClient';
import { ComboboxWithFilterFormik } from '../../../../../Common/Components/Combobox/ComboboxWithFilterFormik';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { sortIgnoreCase } from '../../../../../Common/Utils/SortUtilities';
import { ConnectorsResources, ManagedResourcesStringResources } from '../../../../../Strings/SREAgentResources';
import { useSubscriptions } from '../../../Hooks/useSubscriptions';
import { ManagedIdentityDropdownWithValidation } from '../Common/ManagedIdentityDropdownWithValidation';
import { NameInput } from '../Common/NameInput';
import { ConnectorFormProps } from '../ConnectorWizardFormik';

interface ICMConnectorFormProps {
    userAssignedIdentities: { id: string; name: string }[];
    agentIdentity: MsiIdentity | undefined;
    refreshAgent: () => void;
    isEditMode?: boolean;
}

export const ICMConnectorForm: FC<ICMConnectorFormProps> = ({
    userAssignedIdentities,
    agentIdentity,
    refreshAgent,
    isEditMode = false,
}) => {
    const intl = useIntl();
    const [keyVaults, setKeyVaults] = useState<KeyVault[]>([]);
    const [keyVaultsLoading, setKeyVaultsLoading] = useState(false);
    const { subscriptionsList, subscriptionsLoading } = useSubscriptions();
    const { setFieldValue } = useFormikContext<ConnectorFormProps>();

    useEffect(() => {
        const fetchKeyVaults = async () => {
            if (!subscriptionsList || subscriptionsList.length === 0) {
                return;
            }

            setKeyVaultsLoading(true);
            try {
                const subscriptionIds = subscriptionsList.map(sub => sub.subscriptionId);
                const vaults = await KeyVaultClient.getKeyVaultsFromArg(subscriptionIds);
                setKeyVaults(vaults);
            } finally {
                setKeyVaultsLoading(false);
            }
        };

        fetchKeyVaults();
    }, [subscriptionsList]);

    const isLoading = subscriptionsLoading || keyVaultsLoading;

    const keyVaultOptions = useMemo(() => {
        return keyVaults
            .sort((a, b) => sortIgnoreCase(a.name, b.name))
            .map(kv => ({
                value: kv.id,
                text: kv.name,
            }));
    }, [keyVaults]);

    return (
        <>
            <NameInput disabled={isEditMode} />

            <ComboboxWithFilterFormik
                name="keyVaultId"
                label={intl.formatMessage(ConnectorsResources.keyVault)}
                placeholder={
                    isLoading
                        ? intl.formatMessage(ManagedResourcesStringResources.loading)
                        : intl.formatMessage(ConnectorsResources.selectKeyVault)
                }
                options={keyVaultOptions}
                onSelectionChange={() => {
                    setFieldValue('url', '');
                }}
                disabled={isLoading}
                required
                orientation="vertical"
            />

            <InputFormik
                name="url"
                label={intl.formatMessage(ConnectorsResources.certificateUri)}
                required
                orientation="vertical"
                disabled={isLoading}
                fieldProps={{
                    hint: intl.formatMessage(ConnectorsResources.certificateUriHelperText),
                }}
            />

            <ManagedIdentityDropdownWithValidation
                userAssignedIdentities={userAssignedIdentities}
                agentIdentity={agentIdentity}
                refreshAgent={refreshAgent}
            />
        </>
    );
};
