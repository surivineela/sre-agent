import { SelectTabData, SelectTabEvent, Tab, TabList } from "@fluentui/react-components";
import { FC, useState, useCallback } from "react";
import Activities from "./Activities/Activities.ReactView";
import KnowledgeGraph from "./KnowledgeGraph/KnowledgeGraph";

enum TabValues {
    Activities = "activities",
    KnowledgeGraph = 'knowledge-graph',
}

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

    return (
        <div>
            <TabList selectedValue={selectedValue} onTabSelect={onTabSelect}>
                <Tab id="Activities" value={TabValues.Activities}>
                    Activities
                </Tab>
                <Tab id="Knowledge " value={TabValues.KnowledgeGraph}>
                    Managed resources
                </Tab>
            </TabList>
            <div>
                {selectedValue === TabValues.Activities && <Activities initialThreadId={initialThreadId} />}
                {selectedValue === TabValues.KnowledgeGraph && <KnowledgeGraph transferDataToActivities={transferDataToActivities} />}
            </div>
        </div>
    )
}

export default SREAgentSpace;