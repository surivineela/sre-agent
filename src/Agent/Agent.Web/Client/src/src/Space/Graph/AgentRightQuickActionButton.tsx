import {
    Button,
    Menu,
    MenuDivider,
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
export interface AgentRightQuickActionButtonProps {
    agent: ExtendedAgent;
}

export const AgentRightQuickActionButton: React.FC<AgentRightQuickActionButtonProps> = ({ agent }) => {
    const intl = useIntl();
    const { quickActionButton, menuPopover } = useExtendedAgentNodeStyles();
    const { contextMenuItemWithIcon, menuIconDisabled } = useExtendedAgentGraphStyles();
    const { triggerAgentQuickAction, hasSkills } = useContext(ExtendedAgentGraphContext);
    const iconSizeProp = useMemo(() => ({ wrapperSize: 20, iconSize: 16, borderRadius: 6 }), []);

    return (
        <Menu positioning="after-top">
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
                        <MenuGroupHeader>{intl.formatMessage(ExtendedAgentsGraphResources.tool)}</MenuGroupHeader>
                        <MenuItem
                            className={contextMenuItemWithIcon}
                            icon={<EntityIcon type="tool" shorthandStyle={iconSizeProp} />}
                            onClick={() => triggerAgentQuickAction(agent.name, 'addTool')}
                            content={intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAddExistingTools)}
                        />
                        <MenuItem
                            className={contextMenuItemWithIcon}
                            icon={<EntityIcon type="toolWithGear" shorthandStyle={iconSizeProp} />}
                            onClick={() => triggerAgentQuickAction(agent.name, 'createTool')}
                            content={intl.formatMessage(ExtendedAgentsGraphResources.quickCreateCreateNewKustoTool)}
                        />
                    </MenuGroup>
                    <MenuDivider />
                    <MenuGroup>
                        <MenuGroupHeader>{intl.formatMessage(ExtendedAgentsGraphResources.subagent)}</MenuGroupHeader>
                        <Tooltip
                            content={hasSkills ? intl.formatMessage(ExtendedAgentsGraphResources.cannotCreateSubagentWithSkills) : ''}
                            relationship="description"
                        >
                            <MenuItem
                                className={hasSkills ? menuIconDisabled : contextMenuItemWithIcon}
                                icon={<EntityIcon type="agent" shorthandStyle={iconSizeProp} />}
                                onClick={() => triggerAgentQuickAction(agent.name, 'addHandoffTargetExistingAgent')}
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
                                onClick={() => triggerAgentQuickAction(agent.name, 'createHandoffTargetAgent')}
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
