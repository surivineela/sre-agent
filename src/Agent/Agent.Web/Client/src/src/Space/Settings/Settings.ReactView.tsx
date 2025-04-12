import { INavLinkGroup, initializeIcons, Nav } from '@fluentui/react';
import { FC, useEffect, useState } from 'react';
import { Settings_Tabs } from '../../Strings/SREResources.resjson';
import AccessControl from './AccessControl.ReactView';
import AgentDetails from './AgentDetails.ReactView';
import IncidentManagement from './IncidentManagement.ReactView';
import { navStyles, useSettingsStyles } from './Styles/Settings.styles';

interface ISettingsProps {
    parameters: {
        resourceId: string;
        region: string;
    };
}

enum SettingsKeys {
    IncidentManagement = 'incidentManagement',
    AccessControl = 'accessControl',
    AgentDetails = 'agentDetails',
}

const navLinkGroups: INavLinkGroup[] = [
    {
        links: [
            {
                name: Settings_Tabs.incidentManagement,
                url: '',
                key: SettingsKeys.IncidentManagement,
            },
            {
                name: Settings_Tabs.accessControl,
                url: '',
                key: SettingsKeys.AccessControl,
            },
            {
                name: Settings_Tabs.agentDetails,
                url: '',
                key: SettingsKeys.AgentDetails,
            },
        ],
    },
];

const Settings: FC<ISettingsProps> = ({ parameters }) => {
    const [ iconsInitialized, setIconsInitialized ] = useState(false);

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    const styles = useSettingsStyles();

    const [selectedKey, setSelectedKey] = useState<SettingsKeys>(SettingsKeys.IncidentManagement);

    return iconsInitialized && (
        <div style={styles.navContainer}>
            <Nav
                groups={navLinkGroups}
                styles={navStyles}
                selectedKey={selectedKey}
                onLinkClick={(_, item) => {
                    if (item?.key && Object.values(SettingsKeys).includes(item.key as SettingsKeys)) {
                        setSelectedKey(item.key as SettingsKeys);
                    }
                }}
            />
            <div style={styles.navPivotContainer}>
                {selectedKey === SettingsKeys.IncidentManagement && <IncidentManagement parameters={parameters} />}
                {selectedKey === SettingsKeys.AccessControl && <AccessControl parameters={parameters} />}
                {selectedKey === SettingsKeys.AgentDetails && <AgentDetails parameters={parameters} />}
            </div>
        </div>
    );
};

export default Settings;
