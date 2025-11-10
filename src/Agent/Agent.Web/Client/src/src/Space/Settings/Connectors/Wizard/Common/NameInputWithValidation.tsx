import { useCallback } from 'react';
import { useIntl } from 'react-intl';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { Connector } from '../../../../../Common/Contracts/Azure/SreAgent';
import { AntUxStringComparison, equals } from '../../../../../Common/Helpers/Strings';
import { ConnectorsResources, SreAgentResources } from '../../../../../Strings/SREAgentResources';

interface NameInputWithValidationProps {
    disabled: boolean;
    existingConnectors: Connector[] | undefined;
}

export const NameInputWithValidation: React.FC<NameInputWithValidationProps> = ({ disabled, existingConnectors }) => {
    const intl = useIntl();

    const validateName = useCallback(
        (name: string) => {
            if (!name) {
                return intl.formatMessage(SreAgentResources.fieldRequired);
            }

            const isDuplicate = existingConnectors?.some(connector => equals(name, connector.name, AntUxStringComparison.IgnoreCase));
            return isDuplicate ? intl.formatMessage(ConnectorsResources.duplicateNameError) : undefined;
        },
        [existingConnectors, intl]
    );

    return (
        <InputFormik
            name="name"
            label={intl.formatMessage(SreAgentResources.name)}
            required
            orientation="vertical"
            placeholder={intl.formatMessage(ConnectorsResources.namePlaceholder)}
            disabled={disabled}
            validate={validateName}
        />
    );
};
