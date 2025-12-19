import { useFormikContext } from 'formik';
import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { ConnectorType } from '../Common/ConnectorType';
import { ManagedIdentityDropdownWithValidation } from '../Common/ManagedIdentityDropdownWithValidation';
import { NameInput } from '../Common/NameInput';
import { UrlInput } from '../Common/UrlInput';
import { ConnectorFormProps } from '../ConnectorWizardFormik';

interface AzureConnectorFormProps {
    userAssignedIdentities: { id: string; name: string }[];
    agentIdentity: MsiIdentity | undefined;
    refreshAgent: () => void;
    isEditMode?: boolean;
}

export const AzureConnectorForm: React.FC<AzureConnectorFormProps> = props => {
    const { userAssignedIdentities, agentIdentity, refreshAgent, isEditMode = false } = props;
    const { values } = useFormikContext<ConnectorFormProps>();

    // Only show FIC fields for Azure DevOps Documentation connector
    const showFicFields = values.connectorType === ConnectorType.AzureDevOpsDocumentation;

    return (
        <>
            <NameInput disabled={isEditMode} />
            <UrlInput />
            <ManagedIdentityDropdownWithValidation
                userAssignedIdentities={userAssignedIdentities}
                agentIdentity={agentIdentity}
                refreshAgent={refreshAgent}
                showFicFields={showFicFields}
            />
        </>
    );
};
