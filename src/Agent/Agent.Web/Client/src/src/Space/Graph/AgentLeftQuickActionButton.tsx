import {
    Button,
    Menu,
    MenuGroup,
    MenuGroupHeader,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    Tooltip,
} from '@fluentui/react-components';
import { Add20Regular } from '@fluentui/react-icons';
import { useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedAgentGraphContext } from '../Contracts/ExtendedAgentGraph';
import { useExtendedAgentGraphStyles, useExtendedAgentNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from './EntityIcon';
export interface AgentLeftQuickActionButtonProps {
    agent: ExtendedAgent;
}

export const AgentLeftQuickActionButton: React.FC<AgentLeftQuickActionButtonProps> = ({ agent }) => {
    const intl = useIntl();
    const { quickActionButton, menuPopover } = useExtendedAgentNodeStyles();
    const { contextMenuItemWithIcon } = useExtendedAgentGraphStyles();
    const { triggerAgentQuickAction } = useContext(ExtendedAgentGraphContext);
    const iconSizeProp = useMemo(() => ({ wrapperSize: 20, iconSize: 16, borderRadius: 6 }), []);

    return (
        <Menu positioning="before-top">
            <MenuTrigger disableButtonEnhancement>
                <Tooltip
                    content={intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickActionTooltip)}
                    relationship="label"
                    positioning="above"
                >
                    <Button appearance="secondary" shape="circular" icon={<Add20Regular />} className={quickActionButton} />
                </Tooltip>
            </MenuTrigger>
            <MenuPopover className={menuPopover}>
                <MenuList>
                    <MenuGroup>
                        <MenuGroupHeader>{intl.formatMessage(ExtendedAgentsGraphResources.trigger)}</MenuGroupHeader>
                        <MenuItem
                            className={contextMenuItemWithIcon}
                            icon={<EntityIcon type="incidentTrigger" shorthandStyle={iconSizeProp} />}
                            onClick={() => triggerAgentQuickAction(agent.name, 'addIncidentTrigger')}
                            content={intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAddIncidentTrigger)}
                        />
                        <MenuItem
                            className={contextMenuItemWithIcon}
                            icon={<EntityIcon type="scheduledTask" shorthandStyle={iconSizeProp} />}
                            onClick={() => triggerAgentQuickAction(agent.name, 'addScheduledTask')}
                            content={intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAddScheduledTask)}
                        />
                    </MenuGroup>
                    <MenuGroup>
                        <MenuGroupHeader>{intl.formatMessage(ExtendedAgentsGraphResources.subagent)}</MenuGroupHeader>
                        <MenuItem
                            className={contextMenuItemWithIcon}
                            icon={<EntityIcon type="agent" shorthandStyle={iconSizeProp} />}
                            onClick={() => triggerAgentQuickAction(agent.name, 'addHandoffSourceExistingAgent')}
                            content={intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAddExistingSubagent)}
                        />
                        <MenuItem
                            className={contextMenuItemWithIcon}
                            icon={<EntityIcon type="agent" shorthandStyle={iconSizeProp} />}
                            onClick={() => triggerAgentQuickAction(agent.name, 'createHandoffSourceAgent')}
                            content={intl.formatMessage(ExtendedAgentsGraphResources.quickCreateCreateNewSubagent)}
                        />
                    </MenuGroup>
                </MenuList>
            </MenuPopover>
        </Menu>
    );
};
