import { FC } from "react";
import { Settings_Tabs, SreAgentResources } from "../../Strings/SREResources.resjson";
import { Label } from '@fluentui/react/lib/Label';
import { useSettingsStyles } from "./Styles/Settings.styles";
import { useAgentDetails } from "./Hooks/useAgentDetails";

interface AgentDetailsProps {
    parameters: {
        resourceId: string;
        region: string;
    };
}

const AgentDetails: FC<AgentDetailsProps> = ({ parameters }) => {
    const { resourceId, region } = parameters;

    const styles = useSettingsStyles();

    const { resourceGroup, subscription, resourceName } = useAgentDetails(resourceId);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{Settings_Tabs.agentDetails}</div>
            <div style={styles.gridStyle}>
                <Label>{SreAgentResources.name}</Label>
                {resourceName}
                <Label>{SreAgentResources.subscription}</Label>
                {resourceGroup}
                <Label>{SreAgentResources.resourceGroup}</Label>
                {subscription}
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