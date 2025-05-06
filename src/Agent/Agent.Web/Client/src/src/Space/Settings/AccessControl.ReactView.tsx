import { DefaultButton } from '@fluentui/react/lib/Button';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AccessControlResources, SettingsTabResources } from '../../Strings/SREAgentResources';
import { useSettingsStyles } from './Styles/Settings.styles';

const AccessControl: FC = () => {
    const { resourceId } = useContext(EnvironmentContext);
    const az = useContext(AzPortalContext);
    const intl = useIntl();

    const styles = useSettingsStyles();

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.accessControl)}</div>
            <div style={styles.accessControlSettingsContainer}>
                {intl.formatMessage(AccessControlResources.accessControlDescription)}
                <DefaultButton
                    text={intl.formatMessage(AccessControlResources.openAccessControl)}
                    style={styles.accessControlSettingsButton}
                    onClick={() =>
                        az.openBlade({
                            extension: 'Microsoft_Azure_AD',
                            detailBlade: 'AccessControlBlade',
                            detailBladeInputs: {
                                scope: resourceId,
                            },
                        })
                    }
                />
            </div>
        </>
    );
};

export default AccessControl;
