import { Label } from '@fluentui/react/lib/Label';
import { FC, useContext, useMemo } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { Settings_Tabs, SreAgentResources } from '../../Strings/SREResources.resjson';
import { useSreAgent } from './Hooks/useSreAgent';
import { useSettingsStyles } from './Styles/Settings.styles';

const AgentDetails: FC = () => {
    const styles = useSettingsStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const { agent } = useSreAgent(resourceId);
    const region = useMemo(() => agent?.location, [agent?.location]);

    const { resourceGroup, subscription, resourceName } = useMemo(() => new ArmResourceDescriptor(resourceId), [resourceId]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{Settings_Tabs.agentDetails}</div>
            <div style={styles.gridStyle}>
                <Label>{SreAgentResources.name}</Label>
                {resourceName}
                <Label>{SreAgentResources.subscription}</Label>
                {subscription}
                <Label>{SreAgentResources.resourceGroup}</Label>
                {resourceGroup}
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
