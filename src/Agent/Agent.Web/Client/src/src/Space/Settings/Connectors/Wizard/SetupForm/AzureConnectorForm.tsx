import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { ManagedIdentityDropdownWithValidation } from '../Common/ManagedIdentityDropdownWithValidation';
import { NameInputWithValidation } from '../Common/NameInputWithValidation';
import { SetupConnectorFormWrapper } from '../Common/SetupConnectorFormWrapper';
import { UrlInputWithValidation } from '../Common/UrlInputWithValidation';

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
            <NameInputWithValidation disabled={isEditMode} />
            <UrlInputWithValidation />
            <ManagedIdentityDropdownWithValidation
                userAssignedIdentities={userAssignedIdentities}
                agentIdentity={agentIdentity}
                refreshAgent={refreshAgent}
            />
        </SetupConnectorFormWrapper>
    );
};
