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
import { MouseEvent, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedAgentGraphContext } from '../Contracts/ExtendedAgentGraph';
import { useExtendedAgentGraphStyles, useExtendedAgentNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from './EntityIcon';
export interface QuickActionButtonProps {
    agent: ExtendedAgent;
}

export const QuickActionButton: React.FC<QuickActionButtonProps> = ({ agent }) => {
    const intl = useIntl();
    const { quickActionButton, menuPopover } = useExtendedAgentNodeStyles();
    const { contextMenuItemWithIcon } = useExtendedAgentGraphStyles();
    const { triggerAgentQuickAction, openRelationshipDialog } = useContext(ExtendedAgentGraphContext);
    const iconSizeProp = useMemo(() => ({ wrapperSize: 20, iconSize: 16, borderRadius: 6 }), []);

    const handleOpenRelationships = (event: MouseEvent<HTMLButtonElement>) => {
        event.stopPropagation();

        if (agent?.name && openRelationshipDialog) {
            openRelationshipDialog(agent.name);
        }
    };

    return (
        <Menu positioning="above-end">
            <MenuTrigger disableButtonEnhancement>
                <Tooltip
                    content={intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickActionTooltip)}
                    relationship="label"
                    positioning="above"
                >
                    <Button
                        appearance="secondary"
                        shape="circular"
                        icon={<Add20Regular />}
                        className={quickActionButton}
                        onClick={triggerAgentQuickAction ? undefined : handleOpenRelationships}
                    />
                </Tooltip>
            </MenuTrigger>
            <MenuPopover className={menuPopover}>
                {triggerAgentQuickAction ? (
                    <MenuList>
                        <MenuGroup>
                            <MenuGroupHeader>{intl.formatMessage(ExtendedAgentsGraphResources.tool)}</MenuGroupHeader>

                            <MenuItem
                                className={contextMenuItemWithIcon}
                                icon={<EntityIcon type="tool" shorthandStyle={iconSizeProp} />}
                                onClick={() => triggerAgentQuickAction(agent.name, 'addTool')}
                                content={intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAddExistingTool)}
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
                            <MenuItem
                                className={contextMenuItemWithIcon}
                                icon={<EntityIcon type="agent" shorthandStyle={iconSizeProp} />}
                                onClick={() => triggerAgentQuickAction(agent.name, 'addHandoff')}
                                content={intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAddExistingSubagent)}
                            />
                            <MenuItem
                                className={contextMenuItemWithIcon}
                                icon={<EntityIcon type="agent" shorthandStyle={iconSizeProp} />}
                                onClick={() => triggerAgentQuickAction(agent.name, 'createAgent')}
                                content={intl.formatMessage(ExtendedAgentsGraphResources.quickCreateCreateNewSubagent)}
                            />
                        </MenuGroup>
                    </MenuList>
                ) : (
                    <MenuList>
                        <MenuItem
                            onClick={() => {
                                if (agent?.name && openRelationshipDialog) {
                                    openRelationshipDialog(agent.name);
                                }
                            }}
                        >
                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateAgentHeader)}
                        </MenuItem>
                    </MenuList>
                )}
            </MenuPopover>
        </Menu>
    );
};
