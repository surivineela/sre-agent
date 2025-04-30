import { ThemeContext } from '@fluentui/react';
import { Tab, TabList } from '@fluentui/react-components';
import { LineHorizontal120Regular, Open16Regular } from '@fluentui/react-icons';
import type { Theme } from '@fluentui/theme';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SreAgentTabResources } from '../Strings/SREAgentResources';
import Activities from './Activities/Activities.ReactView';
import Graph from './Graph/Graph';
import { getTabListStyle, inStandaloneMode, TabValues, useSreAgentSpace } from './Hooks/useSreAgentSpace';
import Settings from './Settings/Settings.ReactView';
import { useSreAgentSpaceStyles } from './Settings/Styles/SreAgentSpaceStyles';

const SREAgentSpace: FC = () => {
    const environmentContext = useContext(EnvironmentContext);
    const theme = useContext(ThemeContext);
    const intl = useIntl();

    const styles = useSreAgentSpaceStyles();

    const { selectedValue, initialThreadId, isLogsItemDisabled, transferDataToActivities, onTabSelect } = useSreAgentSpace(
        environmentContext.resourceId
    );

    return (
        <div>
            <TabList selectedValue={selectedValue} onTabSelect={onTabSelect} style={getTabListStyle(theme as Theme)}>
                <Tab id="Activities" value={TabValues.Activities}>
                    {intl.formatMessage(SreAgentTabResources.activities)}
                </Tab>
                <Tab id="Knowledge" value={TabValues.Graph}>
                    {intl.formatMessage(SreAgentTabResources.managedResources)}
                </Tab>
                {!inStandaloneMode && (
                    <Tab id="Settings" value={TabValues.Settings}>
                        {intl.formatMessage(SreAgentTabResources.settings)}
                    </Tab>
                )}
                <LineHorizontal120Regular className={styles.lineIconStyle} />
                <Tab id="Logs" value={TabValues.Logs} disabled={isLogsItemDisabled}>
                    <div className={styles.logsMenuItemContainer}>
                        <Open16Regular />
                        {intl.formatMessage(SreAgentTabResources.logs)}
                    </div>
                </Tab>
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
