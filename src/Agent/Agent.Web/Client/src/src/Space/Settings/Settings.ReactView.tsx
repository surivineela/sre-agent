import { initializeIcons } from '@fluentui/react';
import { FC, useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import GrafanaDashboard from '../../GrafanaDashboard/GrafanaDashboard.ReactView';
import { SecondaryNavItemValues } from '../Contracts/SreAgentSpace';
import SessionInsights from '../SessionInsights/SessionInsights';
import AzureSettings from './AzureSettings.ReactView';
import Basics from './Basics.ReactView';
import { Connectors } from './Connectors/Connectors';
import KnowledgeBase from './KnowledgeBaseComponents/KnowledgeBase.ReactView';
import KnowledgeSettings from './KnowledgeBaseComponents/KnowledgeSettings';
import McpServer from './McpServer';
import Permissions from './Permissions/Permissions.ReactView';
import ManagedResources from './ResourceGroups/ManagedResources.ReactView';
import { useSettingsStyles } from './Styles/Settings.styles';
import SubAgents from './SubAgents.ReactView';
import Usage from './Usage';

const Settings: FC = () => {
    const styles = useSettingsStyles();
    const { menuItem } = useParams();
    const showConnectors = useConfigSetting(SettingNames.Connectors);
    const showSubAgents = useConfigSetting(SettingNames.ShowSubAgentsItemInSettings);
    const showKnowledgeSettings = useConfigSetting(SettingNames.KnowledgeSettings);

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
                    {selectedKey === SecondaryNavItemValues.Connectors && showConnectors && <Connectors />}
                    {selectedKey === SecondaryNavItemValues.KnowledgeBase && <KnowledgeBase />}
                    {selectedKey === SecondaryNavItemValues.KnowledgeSettings && showKnowledgeSettings && <KnowledgeSettings />}
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
