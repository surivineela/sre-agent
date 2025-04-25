import { ThemeContext } from '@fluentui/react';
import { SelectTabData, SelectTabEvent, Tab, TabList } from '@fluentui/react-components';
import type { Theme } from '@fluentui/theme';
import { FC, useCallback, useContext, useState } from 'react';
import AzPortalProxy from '../Common/AzPortalProxy/AzPortalProxy';
import { SreAgentTabs } from '../Strings/SREResources.resjson';
import Activities from './Activities/Activities.ReactView';
import Graph from './Graph/Graph';
import Settings from './Settings/Settings.ReactView';

const getTabListStyle = (theme: Theme) => {
    return {
        backgroundColor: theme.semanticColors.bodyBackground,
    };
};

enum TabValues {
    Activities = 'activities',
    Settings = 'settings',
    Graph = 'graph',
}

const inStandaloneMode = AzPortalProxy.inStandaloneMode;

const SREAgentSpace: FC = () => {
    const [selectedValue, setSelectedValue] = useState<TabValues>(TabValues.Activities);
    const [initialThreadId, setInitialThreadId] = useState<string | null | undefined>(null);
    const theme = useContext(ThemeContext);

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
            <TabList selectedValue={selectedValue} onTabSelect={onTabSelect} style={getTabListStyle(theme as Theme)}>
                <Tab id="Activities" value={TabValues.Activities}>
                    {SreAgentTabs.activities}
                </Tab>
                <Tab id="Knowledge" value={TabValues.Graph}>
                    {SreAgentTabs.managedResources}
                </Tab>
                {!inStandaloneMode && (
                    <Tab id="Settings" value={TabValues.Settings}>
                        {SreAgentTabs.settings}
                    </Tab>
                )}
            </TabList>
            <div>
                {selectedValue === TabValues.Activities && <Activities initialThreadId={initialThreadId} />}
                {selectedValue === TabValues.Graph && <Graph transferDataToActivities={transferDataToActivities} />}
                {selectedValue === TabValues.Settings && <Settings />}
            </div>
        </div>
    );
};

export default SREAgentSpace;
