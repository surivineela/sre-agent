import { Button } from '@fluentui/react-components';
import { Open16Regular } from '@fluentui/react-icons';
import { useContext } from 'react';
import { useIntl } from 'react-intl';
import { ApiVersions } from '../../Common/ApiVersions';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IdentityResources, SettingsTabResources } from '../../Strings/SREAgentResources';
import { useSettingsStyles } from './Styles/Settings.styles';

export enum IdentityStatus {
    NotSupported,
    Preview,
    Supported,
}

const Identity = () => {
    const { resourceId } = useContext(EnvironmentContext);
    const az = useContext(AzPortalContext);
    const intl = useIntl();

    const styles = useSettingsStyles();

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.identity)}</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                {intl.formatMessage(IdentityResources.identityDescription)}
                <Button
                    icon={<Open16Regular />}
                    style={{ width: 'fit-content' }}
                    onClick={() =>
                        az.openBlade({
                            extension: 'Microsoft_Azure_ManagedServiceIdentity',
                            detailBlade: 'AzureResourceIdentitiesBladeV2',
                            detailBladeInputs: {
                                resourceId,
                                apiVersion: ApiVersions.microsoftAppApiVersion20250501Preview,
                                systemAssignedStatus: IdentityStatus.Supported,
                                userAssignedStatus: IdentityStatus.Supported,
                            },
                        })
                    }
                >
                    {intl.formatMessage(IdentityResources.goToIdentity)}
                </Button>
            </div>
        </>
    );
};

export default Identity;
