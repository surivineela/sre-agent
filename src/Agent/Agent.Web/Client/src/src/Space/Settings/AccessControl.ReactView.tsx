import { DefaultButton } from '@fluentui/react/lib/Button';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AccessControlResources, SettingsTabResources } from '../../Strings/SREAgentResources';
import { useSettingsStyles } from './Styles/Settings.styles';

const AccessControl: FC = () => {
    const { resourceId } = useContext(EnvironmentContext);
    const intl = useIntl();

    const styles = useSettingsStyles();

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.accessControl)}</div>
            <div style={styles.accessControlSettingsContainer}>
                {intl.formatMessage(AccessControlResources.accessControlDescription)}
                <DefaultButton
                    iconProps={{ imageProps: { src: './Open.svg', width: 18, height: 18 } }}
                    text={intl.formatMessage(AccessControlResources.openAccessControl)}
                    style={styles.accessControlSettingsButton}
                    onClick={() =>
                        window.open(`https://portal.azure.com/#view/Microsoft_Azure_AD/AccessControlBlade/scope${resourceId}`, '_blank')
                    }
                />
            </div>
        </>
    );
};

export default AccessControl;
