import { FC, memo, useCallback, useContext } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SettingsTabResources, SupportResources } from '../../Strings/SREAgentResources';
import AzurePortalBladeLinkPage from './AzurePortalBladeLinkPage';

const Support: FC = () => {
    const { resourceId } = useContext(EnvironmentContext);
    const az = useContext(AzPortalContext);
    const intl = useIntl();

    const onClickButton = useCallback(() => {
        az.openBlade({
            extension: 'Microsoft_Azure_Support',
            detailBlade: 'Aurora.ReactView',
            detailBladeInputs: {
                resourceId: resourceId,
            },
        });
    }, [az, resourceId]);

    return (
        <AzurePortalBladeLinkPage
            title={intl.formatMessage(SettingsTabResources.support)}
            description={intl.formatMessage(SupportResources.description)}
            buttonText={intl.formatMessage(SupportResources.buttonText)}
            onClickButton={onClickButton}
        />
    );
};

export default memo(Support);
