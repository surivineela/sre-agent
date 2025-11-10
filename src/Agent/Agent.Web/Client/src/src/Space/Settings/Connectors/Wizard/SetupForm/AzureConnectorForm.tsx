import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../../../Common/Contracts/Azure/SreAgent';
import { ManagedIdentityDropdownWithValidation } from '../Common/ManagedIdentityDropdownWithValidation';
import { NameInputWithValidation } from '../Common/NameInputWithValidation';
import { SetupConnectorFormWrapper } from '../Common/SetupConnectorFormWrapper';
import { UrlInputWithValidation } from '../Common/UrlInputWithValidation';

interface AzureConnectorFormProps {
    userAssignedIdentities: { id: string; name: string }[];
    agentIdentity: MsiIdentity | undefined;
    existingConnectors: Connector[] | undefined;
    refreshAgent: () => void;
    isEditMode?: false;
}

export const AzureConnectorForm: React.FC<AzureConnectorFormProps> = props => {
    const { userAssignedIdentities, agentIdentity, existingConnectors, refreshAgent, isEditMode = false } = props;

    return (
        <SetupConnectorFormWrapper>
            <NameInputWithValidation disabled={isEditMode} existingConnectors={existingConnectors} />
            <UrlInputWithValidation />
            <ManagedIdentityDropdownWithValidation
                userAssignedIdentities={userAssignedIdentities}
                agentIdentity={agentIdentity}
                refreshAgent={refreshAgent}
            />
        </SetupConnectorFormWrapper>
    );
};
