import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { ManagedIdentityDropdownWithValidation } from '../Common/ManagedIdentityDropdownWithValidation';
import { NameInput } from '../Common/NameInput';
import { SetupConnectorFormWrapper } from '../Common/SetupConnectorFormWrapper';
import { UrlInput } from '../Common/UrlInput';

interface AzureConnectorFormProps {
    userAssignedIdentities: { id: string; name: string }[];
    agentIdentity: MsiIdentity | undefined;
    refreshAgent: () => void;
    isEditMode?: false;
}

export const AzureConnectorForm: React.FC<AzureConnectorFormProps> = props => {
    const { userAssignedIdentities, agentIdentity, refreshAgent, isEditMode = false } = props;

    return (
        <SetupConnectorFormWrapper>
            <NameInput disabled={isEditMode} />
            <UrlInput />
            <ManagedIdentityDropdownWithValidation
                userAssignedIdentities={userAssignedIdentities}
                agentIdentity={agentIdentity}
                refreshAgent={refreshAgent}
            />
        </SetupConnectorFormWrapper>
    );
};
