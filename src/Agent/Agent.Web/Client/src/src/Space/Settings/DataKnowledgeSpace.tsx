import { ThemeContext } from '@fluentui/react';
import { SelectTabData, SelectTabEvent, Tab, TabList } from '@fluentui/react-components';
import type { Theme } from '@fluentui/theme';
import { FC, useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { KnowledgeBaseResources, SettingsTabResources } from '../../Strings/SREAgentResources';
import DataConnectors from './DataKnowledgeSpaceComponents.tsx/DataConnectors.ReactView';
import KnowledgeBase from './DataKnowledgeSpaceComponents.tsx/KnowledgeBase.ReactView';
import { useDataKnowledgeSpaceStyles } from './Styles/DataKnowledgeSpace.styles';

const getTabListStyle = (theme: Theme) => {
    return {
        backgroundColor: theme.semanticColors.bodyBackground,
    };
};

enum TabValues {
    KnowledgeBase = 'knowledgebase',
    DataConnectors = 'dataconnectors',
}

const DataKnowledgeSpace: FC = () => {
    const intl = useIntl();
    const theme = useContext(ThemeContext);
    const { logAmplitudeNavigationEvent } = useAzPortalContext();

    const [selectedTab, setSelectedTab] = useState<TabValues>(TabValues.KnowledgeBase);

    const styles = useDataKnowledgeSpaceStyles();

    const onTabSelect = useCallback(
        (_: SelectTabEvent, data: SelectTabData) => {
            const tabValue = data.value as TabValues;
            logAmplitudeNavigationEvent({
                targetType: 'tab',
                targetAction: 'tabItem',
                targetName: tabValue,
                targetFriendlyName: tabValue,
            });
            setSelectedTab(tabValue);
        },
        [logAmplitudeNavigationEvent]
    );

    return (
        <div className={styles.settingsContainer}>
            <div className={styles.outerContainer}>
                <div className={styles.container}>
                    <div className={styles.header}>{intl.formatMessage(SettingsTabResources.knowledgeBase)}</div>
                    <div>{intl.formatMessage(KnowledgeBaseResources.fileUploadDescription)}</div>
                </div>
            </div>
            <div className={styles.tabsContainer}>
                <TabList selectedValue={selectedTab} onTabSelect={onTabSelect} style={getTabListStyle(theme as Theme)}>
                    <Tab id="KnowledgeBase" value={TabValues.KnowledgeBase}>
                        {intl.formatMessage(SettingsTabResources.fileSource)}
                    </Tab>
                    <Tab id="DataConnectors" value={TabValues.DataConnectors}>
                        {intl.formatMessage(SettingsTabResources.dataSource)}
                    </Tab>
                </TabList>
            </div>
            <div className={styles.containerDivider}>
                {selectedTab === TabValues.DataConnectors && <DataConnectors />}
                {selectedTab === TabValues.KnowledgeBase && <KnowledgeBase />}
            </div>
        </div>
    );
};

export default DataKnowledgeSpace;
