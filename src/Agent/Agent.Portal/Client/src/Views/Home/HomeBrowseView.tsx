import { makeStyles, Subtitle1, Tab, TabList } from '@fluentui/react-components';
import { useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { AmplitudeContextProvider } from '../../Common/Contexts/AmplitudeContext';
import { ProductName } from '../../Common/Contracts/Amplitude';
import { useAmplitudeTelemetry } from '../../Common/Hooks/useAmplitudeTelemetry';
import { useIsInternal } from '../../Common/Hooks/useIsInternal';
import { PortalResources } from '../../Strings/Resources';
import { AgentsGrid } from './AgentsGrid';
import { InternalAgentLinksGrid } from './ExternalAgents/InternalAgentLinksGrid';

enum GridTabKey {
    agents = 'agents',
    external = 'external',
}

const useStyles = makeStyles({
    container: {
        display: 'flex',
        minHeight: '600px',
        flex: 'auto',
        flexDirection: 'column',
        gap: '40px',
        alignItems: 'center',
        padding: '32px',
    },
    title: {
        margin: 0,
    },
    agentListContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
        width: '100%',
        maxWidth: '1200px',
        flex: '1',
        minHeight: '0',
    },
});

export const HomeBrowseView = () => {
    return (
        <AmplitudeContextProvider resourceId="" productName={ProductName.SreAgent} telemetrySource={TelemetrySource.HomeBrowseView}>
            <HomeBrowseViewContent />
        </AmplitudeContextProvider>
    );
};

const HomeBrowseViewContent = () => {
    const intl = useIntl();
    const styles = useStyles();
    const { isInternalTenant, isInternalDevTenant } = useIsInternal();
    const { logNavigationEvent } = useAmplitudeTelemetry();

    const [selectedTabKey, setSelectedTabKey] = useState<GridTabKey>(GridTabKey.agents);

    const showExternalAgents = useMemo(() => isInternalTenant || isInternalDevTenant, [isInternalTenant, isInternalDevTenant]);

    useEffect(() => {
        logNavigationEvent({
            targetType: 'link',
            targetAction: 'openBlade',
            targetName: 'sreAgentHome',
            targetFriendlyName: 'SRE Agent Home',
        });
    }, [logNavigationEvent]);

    return (
        <div className={styles.container}>
            <div className={styles.agentListContainer}>
                {showExternalAgents ? (
                    <TabList selectedValue={selectedTabKey} onTabSelect={(_, data) => setSelectedTabKey(data.value as GridTabKey)}>
                        <Tab value={GridTabKey.agents}>{intl.formatMessage(PortalResources.agents)}</Tab>
                        <Tab value={GridTabKey.external}>{intl.formatMessage(PortalResources.externalAgents)}</Tab>
                    </TabList>
                ) : (
                    <Subtitle1 as="h1" block className={styles.title}>
                        {intl.formatMessage(PortalResources.agents)}
                    </Subtitle1>
                )}

                {selectedTabKey === GridTabKey.agents && <AgentsGrid />}

                {selectedTabKey === GridTabKey.external && <InternalAgentLinksGrid />}
            </div>
        </div>
    );
};
