import { SelectTabData, SelectTabEvent, Tab, TabList } from "@fluentui/react-components";
import { FC, useEffect, useState, useCallback } from "react";
import { SreAgentTabs } from "../Strings/SREResources.resjson";
import Activities from "./Activities/Activities.ReactView";
import KnowledgeGraph from "./KnowledgeGraph/KnowledgeGraph";
import Settings from "./Settings/Settings.ReactView";

enum TabValues {
    Activities = "activities",
    Settings = 'settings',
    KnowledgeGraph = 'knowledge-graph',
}

const placeholderResourceId = 'subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/PlaceholderResourceGroup/providers/Microsoft.SRE/agents/PlaceholderAgentName';
const placeholderRegion = 'PlaceholderRegion';

const SREAgentSpace: FC = () => {
    const [selectedValue, setSelectedValue] = useState<TabValues>(TabValues.Activities);
    const [initialThreadId, setInitialThreadId] = useState<string | null | undefined>(null);

    const onTabSelect = useCallback((_: SelectTabEvent, data: SelectTabData) => {
        setInitialThreadId(null);
        setSelectedValue(data.value as TabValues);
    }, []);

    const transferDataToActivities = useCallback((threadId: string | null | undefined) => {
        setInitialThreadId(threadId);
        setSelectedValue(TabValues.Activities);
    }, []);

    const [resourceId, setResourceId] = useState<string>(placeholderResourceId);
    const [region, setRegion] = useState<string>(placeholderRegion);

    useEffect(() => {
        const urlParams = new URLSearchParams(window.location.search);
        const resourceIdValue = urlParams.get('resourceId');
        const regionValue = urlParams.get('region');
        setResourceId(resourceIdValue ?? placeholderResourceId);
        setRegion(regionValue ?? placeholderRegion);
    }, []);

    return (
        <div>
            <TabList selectedValue={selectedValue} onTabSelect={onTabSelect}>
                <Tab id="Activities" value={TabValues.Activities}>
                    {SreAgentTabs.activities}
                </Tab>
                <Tab id="Knowledge" value={TabValues.KnowledgeGraph}>
                    {SreAgentTabs.managedResources}
                </Tab>
                <Tab id="Settings" value={TabValues.Settings}>
                    {SreAgentTabs.settings}
                </Tab>
            </TabList>
            <div>
                {selectedValue === TabValues.Activities && <Activities initialThreadId={initialThreadId} />}
                {selectedValue === TabValues.KnowledgeGraph && <KnowledgeGraph transferDataToActivities={transferDataToActivities} />}
                {selectedValue === TabValues.Settings && <Settings parameters={{ resourceId, region }}/>}
            </div>
        </div>
    );
}

export default SREAgentSpace;