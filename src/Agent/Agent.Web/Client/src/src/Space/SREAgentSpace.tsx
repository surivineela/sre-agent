import { SelectTabData, Tab, TabList } from "@fluentui/react-components";
import { FC, useState } from "react";
import Activities from "./Activities/Activities.ReactView";
import KnowledgeGraph from "./KnowledgeGraph/KnowledgeGraph";

enum TabValues {
    Activities = "activities",
    KnowledgeGraph = 'knowledge-graph',
}

const SREAgentSpace: FC = () => {
    const [selectedValue, setSelectedValue] = useState<TabValues>(TabValues.Activities);

    return (
        <div>
            <TabList selectedValue={selectedValue} onTabSelect={(_, data: SelectTabData) => {
                setSelectedValue(data.value as TabValues);
            }}>
                <Tab id="Activities" value={TabValues.Activities}>
                    Activities
                </Tab>
                <Tab id="Knowledge " value={TabValues.KnowledgeGraph}>
                    Managed resources
                </Tab>
            </TabList>
            <div>
                {selectedValue === TabValues.Activities && <Activities />}
                {selectedValue === TabValues.KnowledgeGraph && <KnowledgeGraph />}
            </div>
        </div>
    )
}

export default SREAgentSpace;