import { INavLink, INavLinkGroup, initializeIcons, MessageBar, MessageBarType, Nav } from '@fluentui/react';
import { Spinner } from '@fluentui/react-components';
import { FC, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { IncidentManagementPlatform } from '../Contracts/IncidentManagement';
import { getIncidentManagementPlatform } from '../Settings/Hooks/useIncidentManagementSettings';
import IncidentManagementSettings from '../Settings/IncidentManagementSettings';
import { navStyles, useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { IncidentManagementMenuKeys } from './CreateIncidentHandler/Contracts';
import HandlersOverview from './HandlersOverview';
import IncidentsOverview from './IncidentsOverview/IncidentsOverview';

const IncidentManagement: FC = () => {
    const intl = useIntl();
    const { agentObj, agentLoading, agentLoadFailure } = useContext(SreAgentContext);
    const { logAmplitudeNavigationEvent } = useAzPortalContext();

    const [iconsInitialized, setIconsInitialized] = useState(false);

    const styles = useIncidentManagementStyles();
    const { menuItem } = useParams();
    const location = useLocation();
    const navigate = useNavigate();

    const selectedKey = useMemo(() => {
        return (
            Object.values(IncidentManagementMenuKeys).find(
                settingsKey => settingsKey.toLocaleLowerCase() === menuItem?.toLocaleLowerCase()
            ) || IncidentManagementMenuKeys.IncidentOverview
        );
    }, [menuItem]);

    const navLinkGroups = useMemo<INavLinkGroup[]>(() => {
        const links: INavLink[] = [
            {
                name: intl.formatMessage(IncidentManagementResources.incidentsOverview),
                url: '',
                key: IncidentManagementMenuKeys.IncidentOverview,
            },
            {
                name: intl.formatMessage(IncidentManagementResources.handlerConfiguration),
                url: '',
                key: IncidentManagementMenuKeys.HandlerConfiguration,
            },
            {
                name: intl.formatMessage(IncidentManagementResources.incidentPlatform),
                url: '',
                key: IncidentManagementMenuKeys.IncidentPlatform,
            },
        ];

        return [{ links }];
    }, [intl]);

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
                navigate({ ...location, pathname: `/views/incidentmanagement/${IncidentManagementMenuKeys.IncidentPlatform}` });
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
                                        Object.values(IncidentManagementMenuKeys).includes(item.key as IncidentManagementMenuKeys) &&
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
                        {selectedKey === IncidentManagementMenuKeys.IncidentOverview && <IncidentsOverview />}
                        {selectedKey === IncidentManagementMenuKeys.HandlerConfiguration && (
                            <HandlersOverview setNavigationHidden={setNavigationHidden} useConsolidatedCreate={true} />
                        )}
                        {selectedKey === IncidentManagementMenuKeys.IncidentPlatform && <IncidentManagementSettings />}
                    </>
                )}
            </div>
        )
    );
};

export default IncidentManagement;
