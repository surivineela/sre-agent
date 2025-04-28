import { Link } from '@fluentui/react';
import { Label } from '@fluentui/react/lib/Label';
import { FC, useCallback, useContext, useMemo } from 'react';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { Settings_Tabs, SreAgentResources } from '../../Strings/SREResources.resjson';
import { useSreAgent } from './Hooks/useSreAgent';
import { useSettingsStyles } from './Styles/Settings.styles';

const AgentDetails: FC = () => {
    const styles = useSettingsStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const az = useContext(AzPortalContext);
    const { agent } = useSreAgent(resourceId);
    const region = useMemo(() => agent?.location, [agent?.location]);

    const { resourceGroup, subscription, resourceName } = useMemo(() => new ArmResourceDescriptor(resourceId), [resourceId]);

    const openSubscription = useCallback(() => {
        az.openBlade({
            detailBlade: 'ResourceMenuBlade',
            detailBladeInputs: { id: `/subscriptions/${subscription}` },
            extension: 'HubsExtension',
        });
    }, [az, subscription]);

    const openResourceGroup = useCallback(() => {
        az.openBlade({
            detailBlade: 'ResourceMenuBlade',
            detailBladeInputs: { id: `/subscriptions/${subscription}/resourcegroups/${resourceGroup}` },
            extension: 'HubsExtension',
        });
    }, [az, subscription]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{Settings_Tabs.agentDetails}</div>
            <div style={styles.gridStyle}>
                <Label>{SreAgentResources.name}</Label>
                {resourceName}
                <Label>{SreAgentResources.subscription}</Label>
                <Link onClick={openSubscription}>{subscription}</Link>
                <Label>{SreAgentResources.resourceGroup}</Label>
                <Link onClick={openResourceGroup}>{resourceGroup}</Link>
                {region && (
                    <>
                        <Label>{SreAgentResources.region}</Label>
                        {region}
                    </>
                )}
            </div>
        </>
    );
};

export default AgentDetails;
