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
import { MouseEvent, useContext, useMemo } from 'react';
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
    const { triggerAgentQuickAction, openRelationshipDialog } = useContext(ExtendedAgentGraphContext);
    const iconSizeProp = useMemo(() => ({ wrapperSize: 20, iconSize: 16, borderRadius: 6 }), []);

    const handleOpenRelationships = (event: MouseEvent<HTMLButtonElement>) => {
        event.stopPropagation();

        if (agent?.name && openRelationshipDialog) {
            openRelationshipDialog(agent.name);
        }
    };

    return (
        <Menu positioning="before-top">
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
                            <MenuGroupHeader>{intl.formatMessage(ExtendedAgentsGraphResources.trigger)}</MenuGroupHeader>
                            <MenuItem
                                className={contextMenuItemWithIcon}
                                icon={<EntityIcon type="incidentTrigger" shorthandStyle={iconSizeProp} />}
                                onClick={() => triggerAgentQuickAction(agent.name, 'addIncidentTrigger')}
                                content={intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAddIncidentTrigger)}
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
