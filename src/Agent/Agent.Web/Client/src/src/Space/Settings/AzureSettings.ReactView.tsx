import { CopilotProvider, CopilotTheme } from '@fluentui-copilot/react-copilot';
import { ThemeContext } from '@fluentui/react';
import { Subtitle1, tokens, webDarkTheme, webLightTheme } from '@fluentui/react-components';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ApiVersions } from '../../Common/ApiVersions';
import AzPortalProxy from '../../Common/AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AccessControlResources, IdentityResources, SettingsTabResources, SupportResources } from '../../Strings/SREAgentResources';
import AzurePortalBladeLinkPage from './AzurePortalBladeLinkPage';

export enum IdentityStatus {
    NotSupported,
    Preview,
    Supported,
}

export const openSupportBlade = (az: AzPortalProxy, resourceId: string) => {
    az.openBlade({
        extension: 'Microsoft_Azure_Support',
        detailBlade: 'Aurora.ReactView',
        detailBladeInputs: {
            resourceId: resourceId,
        },
    });
};

const AzureSettings: FC = () => {
    const { resourceId } = useContext(EnvironmentContext);
    const az = useContext(AzPortalContext);
    const theme = useContext(ThemeContext);
    const intl = useIntl();

    const items = useMemo(() => {
        return [
            {
                title: intl.formatMessage(SettingsTabResources.accessControl),
                description: intl.formatMessage(AccessControlResources.accessControlDescription),
                buttonText: intl.formatMessage(AccessControlResources.openAccessControl),
                onClickButton: () => {
                    az.openBlade({
                        extension: 'Microsoft_Azure_AD',
                        detailBlade: 'AccessControlBlade',
                        detailBladeInputs: {
                            scope: resourceId,
                        },
                    });
                },
            },
            {
                title: intl.formatMessage(SettingsTabResources.identity),
                description: intl.formatMessage(IdentityResources.identityDescription),
                buttonText: intl.formatMessage(IdentityResources.goToIdentity),
                onClickButton: () => {
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
                },
            },
            {
                title: intl.formatMessage(SettingsTabResources.support),
                description: intl.formatMessage(SupportResources.description),
                buttonText: intl.formatMessage(SupportResources.buttonText),
                onClickButton: () => {
                    openSupportBlade(az, resourceId);
                },
            },
        ];
    }, [az, resourceId, intl]);

    return (
        <CopilotProvider
            {...CopilotTheme}
            mode={'canvas'}
            theme={theme?.isInverted ? webDarkTheme : webLightTheme}
            style={{
                display: 'flex',
                flexDirection: 'column',
                gap: tokens.spacingVerticalXXL,
                justifyContent: 'center',
                alignItems: 'center',
                height: '100%',
            }}
        >
            <Subtitle1>{'Configure Azure settings'}</Subtitle1>

            <div
                style={{
                    display: 'flex',
                    gap: tokens.spacingHorizontalM,
                    flexWrap: 'wrap',
                    alignItems: 'center',
                    justifyContent: 'center',
                }}
            >
                {items.map((item, index) => (
                    <AzurePortalBladeLinkPage key={index} {...item} />
                ))}
            </div>
        </CopilotProvider>
    );
};

export default AzureSettings;
