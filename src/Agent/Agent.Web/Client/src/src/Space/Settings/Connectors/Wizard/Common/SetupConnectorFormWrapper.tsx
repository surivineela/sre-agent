import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ConnectorsResources } from '../../../../../Strings/SREAgentResources';
import { useConnectorWizardStyles } from '../ConnectorWizard.styles';
import { ConnectorFormProps } from '../ConnectorWizardFormik';
import { ConnectorType, getConnectorService } from './ConnectorType';

export interface SetupConnectorFormWrapperProps {
    children: React.ReactNode;
}

export const SetupConnectorFormWrapper: React.FC<SetupConnectorFormWrapperProps> = props => {
    const { children } = props;

    const intl = useIntl();
    const styles = useConnectorWizardStyles();

    const { values } = useFormikContext<ConnectorFormProps>();

    const connectorType = useMemo(() => values.connectorType as ConnectorType, [values.connectorType]);

    return (
        <form className={styles.form}>
            <h2 className={styles.title}>
                {intl.formatMessage(ConnectorsResources.setupTitle, { service: getConnectorService(connectorType, intl) })}
            </h2>
            {children}
        </form>
    );
};
