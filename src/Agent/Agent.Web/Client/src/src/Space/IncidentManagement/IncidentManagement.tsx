import { INavLink, INavLinkGroup, initializeIcons, MessageBar, MessageBarType, Nav } from '@fluentui/react';
import { Spinner } from '@fluentui/react-components';
import { FC, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import Url from '../../Common/Helpers/Url';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { IncidentManagementPlatform } from '../Contracts/IncidentManagement';
import { getIncidentManagementPlatform } from '../Settings/Hooks/useIncidentManagementSettings';
import IncidentManagementSettings from '../Settings/IncidentManagementSettings';
import { navStyles, useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import HandlersOverview from './HandlersOverview';
import IncidentsOverview from './IncidentsOverview/IncidentsOverview';

export enum IncidentManagementKeys {
    IncidentOverview = 'incidents',
    HandlerConfiguration = 'handlers',
    IncidentPlatform = 'setup',
}

const IncidentManagement: FC = () => {
    const intl = useIntl();
    const { agentObj, agentLoading, agentLoadFailure } = useContext(SreAgentContext);
    const { logAmplitudeNavigationEvent } = useAzPortalContext();

    const [iconsInitialized, setIconsInitialized] = useState(false);

    const styles = useIncidentManagementStyles();
    const { menuItem } = useParams();
    const location = useLocation();
    const navigate = useNavigate();

    const showIncidentOverview = useMemo(
        () => Url.getFeatureValue('showIncidentOverview') === 'true' || Url.getFeatureValue('showIncidentOverviewMocked') === 'true',
        []
    );

    const selectedKey = useMemo(() => {
        return (
            Object.values(IncidentManagementKeys).find(settingsKey => settingsKey.toLocaleLowerCase() === menuItem?.toLocaleLowerCase()) ||
            (showIncidentOverview ? IncidentManagementKeys.IncidentOverview : IncidentManagementKeys.HandlerConfiguration)
        );
    }, [menuItem, showIncidentOverview]);

    const navLinkGroups = useMemo<INavLinkGroup[]>(() => {
        const links: INavLink[] = [
            {
                name: intl.formatMessage(IncidentManagementResources.handlerConfiguration),
                url: '',
                key: IncidentManagementKeys.HandlerConfiguration,
            },
            {
                name: intl.formatMessage(IncidentManagementResources.incidentPlatform),
                url: '',
                key: IncidentManagementKeys.IncidentPlatform,
            },
        ];

        if (showIncidentOverview) {
            links.unshift({
                name: intl.formatMessage(IncidentManagementResources.incidentsOverview),
                url: '',
                key: IncidentManagementKeys.IncidentOverview,
            });
        }

        return [{ links }];
    }, [intl, showIncidentOverview]);

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    useEffect(() => {
        if (agentObj) {
            const incidentManagementPlatform = getIncidentManagementPlatform(agentObj);
            if (
                incidentManagementPlatform === IncidentManagementPlatform.Disconnected ||
                incidentManagementPlatform === IncidentManagementPlatform.AzMonitor
            ) {
                navigate({ ...location, pathname: `/views/incidentmanagement/${IncidentManagementKeys.IncidentPlatform}` });
            }
        }
    }, [agentObj]);

    const [navigationHidden, setNavigationHidden] = useState<boolean>(false);

    return (
        iconsInitialized && (
            <div className={styles.root}>
                {agentLoading || !iconsInitialized ? (
                    <div className={styles.spinner}>
                        <Spinner size="huge" />
                    </div>
                ) : agentLoadFailure ? (
                    <MessageBar messageBarType={MessageBarType.error}>
                        {intl.formatMessage(IncidentManagementResources.incidentManagementLoadFailure, { errorMessage: agentLoadFailure })}
                    </MessageBar>
                ) : (
                    <>
                        {!navigationHidden && (
                            <Nav
                                groups={navLinkGroups}
                                styles={navStyles}
                                selectedKey={selectedKey}
                                onLinkClick={(_, item) => {
                                    if (
                                        item?.key &&
                                        Object.values(IncidentManagementKeys).includes(item.key as IncidentManagementKeys) &&
                                        item.key !== selectedKey
                                    ) {
                                        logAmplitudeNavigationEvent({
                                            targetType: 'tab',
                                            targetAction: 'tabItem',
                                            targetName: item.key,
                                            targetFriendlyName: item.key,
                                        });

                                        navigate({ ...location, pathname: `/views/incidentmanagement/${item.key}` });
                                    }
                                }}
                            />
                        )}
                        {selectedKey === IncidentManagementKeys.IncidentOverview && (
                            <IncidentsOverview setNavigationHidden={setNavigationHidden} />
                        )}
                        {selectedKey === IncidentManagementKeys.HandlerConfiguration && (
                            <HandlersOverview setNavigationHidden={setNavigationHidden} useConsolidatedCreate={true} />
                        )}
                        {selectedKey === IncidentManagementKeys.IncidentPlatform && <IncidentManagementSettings />}
                    </>
                )}
            </div>
        )
    );
};

export default IncidentManagement;
