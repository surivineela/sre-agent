import { useIntl } from 'react-intl';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { ConnectorsResources, SreAgentResources } from '../../../../../Strings/SREAgentResources';

interface NameInputWithValidationProps {
    disabled: boolean;
}

export const NameInputWithValidation: React.FC<NameInputWithValidationProps> = ({ disabled }) => {
    const intl = useIntl();

    return (
        <InputFormik
            name="name"
            label={intl.formatMessage(SreAgentResources.name)}
            required
            orientation="vertical"
            placeholder={intl.formatMessage(ConnectorsResources.namePlaceholder)}
            disabled={disabled}
        />
    );
};
