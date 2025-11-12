import { useIntl } from 'react-intl';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { ConnectorsResources, SreAgentResources } from '../../../../../Strings/SREAgentResources';

interface NameInputProps {
    disabled: boolean;
}

export const NameInput: React.FC<NameInputProps> = ({ disabled }) => {
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
