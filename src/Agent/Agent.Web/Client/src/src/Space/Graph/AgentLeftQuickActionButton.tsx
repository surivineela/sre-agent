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
    const { contextMenuItemWithIcon, menuIconDisabled } = useExtendedAgentGraphStyles();
    const { triggerAgentQuickAction, hasSkills } = useContext(ExtendedAgentGraphContext);
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
                        <Tooltip
                            content={hasSkills ? intl.formatMessage(ExtendedAgentsGraphResources.cannotCreateSubagentWithSkills) : ''}
                            relationship="description"
                        >
                            <MenuItem
                                className={hasSkills ? menuIconDisabled : contextMenuItemWithIcon}
                                icon={<EntityIcon type="agent" shorthandStyle={iconSizeProp} />}
                                onClick={() => triggerAgentQuickAction(agent.name, 'addHandoffSourceExistingAgent')}
                                disabled={hasSkills}
                            >
                                {intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAddExistingSubagent)}
                            </MenuItem>
                        </Tooltip>
                        <Tooltip
                            content={hasSkills ? intl.formatMessage(ExtendedAgentsGraphResources.cannotCreateSubagentWithSkills) : ''}
                            relationship="description"
                        >
                            <MenuItem
                                className={hasSkills ? menuIconDisabled : contextMenuItemWithIcon}
                                icon={<EntityIcon type="agent" shorthandStyle={iconSizeProp} />}
                                onClick={() => triggerAgentQuickAction(agent.name, 'createHandoffSourceAgent')}
                                disabled={hasSkills}
                            >
                                {intl.formatMessage(ExtendedAgentsGraphResources.quickCreateCreateNewSubagent)}
                            </MenuItem>
                        </Tooltip>
                    </MenuGroup>
                </MenuList>
            </MenuPopover>
        </Menu>
    );
};
