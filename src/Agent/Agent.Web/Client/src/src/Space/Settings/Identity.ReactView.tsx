import { useCallback, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ApiVersions } from '../../Common/ApiVersions';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IdentityResources, SettingsTabResources } from '../../Strings/SREAgentResources';
import AzurePortalBladeLinkPage from './AzurePortalBladeLinkPage';

export enum IdentityStatus {
    NotSupported,
    Preview,
    Supported,
}

const Identity = () => {
    const { resourceId } = useContext(EnvironmentContext);
    const az = useContext(AzPortalContext);
    const intl = useIntl();

    const onClickButton = useCallback(() => {
        az.openBlade({
            extension: 'Microsoft_Azure_ManagedServiceIdentity',
            detailBlade: 'AzureResourceIdentitiesBladeV2',
            detailBladeInputs: {
                resourceId,
                apiVersion: ApiVersions.microsoftAppApiVersion20250501Preview,
                systemAssignedStatus: IdentityStatus.Supported,
                userAssignedStatus: IdentityStatus.Supported,
            },
        });
    }, [az, resourceId]);

    return (
        <AzurePortalBladeLinkPage
            title={intl.formatMessage(SettingsTabResources.identity)}
            description={intl.formatMessage(IdentityResources.identityDescription)}
            buttonText={intl.formatMessage(IdentityResources.goToIdentity)}
            onClickButton={onClickButton}
        />
    );
};

export default Identity;
