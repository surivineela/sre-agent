import { initializeIcons } from '@fluentui/react';
import { FC, useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import GrafanaDashboard from '../../GrafanaDashboard/GrafanaDashboard.ReactView';
import { SecondaryNavItemValues } from '../Contracts/SreAgentSpace';
import SessionInsights from '../SessionInsights/SessionInsights';
import AzureSettings from './AzureSettings.ReactView';
import Basics from './Basics.ReactView';
import McpServer from './McpServer';
import Permissions from './Permissions/Permissions.ReactView';
import ManagedResources from './ResourceGroups/ManagedResources.ReactView';
import { useSettingsStyles } from './Styles/Settings.styles';
import SubAgents from './SubAgents.ReactView';
import Usage from './Usage';

const Settings: FC = () => {
    const styles = useSettingsStyles();
    const { menuItem } = useParams();
    const showSubAgents = useConfigSetting(SettingNames.ShowSubAgentsItemInSettings);

    const [iconsInitialized, setIconsInitialized] = useState(false);
    const selectedKey = useMemo(() => {
        return (
            Object.values(SecondaryNavItemValues).find(settingsKey => settingsKey.toLocaleLowerCase() === menuItem?.toLocaleLowerCase()) ||
            SecondaryNavItemValues.Basics
        );
    }, [menuItem]);

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    return (
        iconsInitialized && (
            <div style={styles.settingsContainer}>
                <div style={styles.settingsContainerInner}>
                    {selectedKey === SecondaryNavItemValues.Basics && <Basics />}
                    {selectedKey === SecondaryNavItemValues.AzureSettings && <AzureSettings />}
                    {selectedKey === SecondaryNavItemValues.ManagedResources && <ManagedResources />}
                    {selectedKey === SecondaryNavItemValues.GrafanaDashboard && <GrafanaDashboard />}
                    {selectedKey === SecondaryNavItemValues.Permissions && <Permissions />}
                    {selectedKey === SecondaryNavItemValues.SubAgents && showSubAgents && <SubAgents />}
                    {selectedKey === SecondaryNavItemValues.McpServers && <McpServer />}
                    {selectedKey === SecondaryNavItemValues.Usage && <Usage />}
                    {selectedKey === SecondaryNavItemValues.SessionInsights && <SessionInsights />}
                </div>
            </div>
        )
    );
};

export default Settings;
