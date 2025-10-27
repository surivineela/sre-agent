import {
    Badge,
    Button,
    Card,
    Menu,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    mergeClasses,
    Text,
    tokens,
    Tooltip,
} from '@fluentui/react-components';
import { Add24Regular, Agents24Regular, MoreHorizontal16Regular } from '@fluentui/react-icons';
import { Handle, Node, NodeProps, Position } from '@xyflow/react';
import { memo, MouseEvent, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedAgentGraphContext, ExtendedAgentGraphNode } from '../Contracts/ExtendedAgentGraph';
import { useExtendedAgentNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
import { getHandleId } from './Utility';

type HandlePosition = 'T' | 'B' | 'L' | 'R';

const Handles = memo(() => {
    const { handle } = useExtendedAgentNodeStyles();
    const positions: HandlePosition[] = ['T', 'B', 'L', 'R'];
    const positionMap: Record<HandlePosition, Position> = {
        T: Position.Top,
        B: Position.Bottom,
        L: Position.Left,
        R: Position.Right,
    };

    return (
        <>
            {positions.map(position => (
                <div key={position}>
                    <Handle className={handle} type="target" position={positionMap[position]} id={getHandleId(position, true)} />
                    <Handle className={handle} type="source" position={positionMap[position]} id={getHandleId(position, false)} />
                </div>
            ))}
        </>
    );
});

Handles.displayName = 'ExtendedAgentHandles';

export const ExtendedAgentCard = (props: NodeProps<Node<ExtendedAgentGraphNode>>) => {
    const { id, data } = props;
    const {
        hoverNode,
        unHoverNode,
        nodesToHighlight,
        selectedNode,
        setSelectedNode,
        hoveredNodeId,
        openRelationshipDialog,
        triggerAgentQuickAction,
    } = useContext(ExtendedAgentGraphContext);

    const {
        agentCard,
        cardHighlighted,
        cardHovered,
        cardSelected,
        cardContent,
        titleRow,
        iconWrapper,
        nameBlock,
        nameText,
        subtitleText,
        badgeRow,
        quickActionButton,
        menuPopover,
        badge,
    } = useExtendedAgentNodeStyles();
    const intl = useIntl();

    const isHovered = hoveredNodeId === id;
    const isSelectedNode = selectedNode?.id === id;

    const agent = data?.data as ExtendedAgent | undefined;
    const agentType = agent?.agentType || 'Autonomous';

    const cardStyles = mergeClasses(
        agentCard,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? cardHovered : undefined,
        isSelectedNode ? cardSelected : undefined
    );

    const toolCount = agent?.tools?.length || 0;

    const handleOpenRelationships = (event: MouseEvent<HTMLButtonElement>) => {
        event.stopPropagation();

        if (agent?.name && openRelationshipDialog) {
            openRelationshipDialog(agent.name);
        }
    };

    return (
        <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <Card onClick={() => setSelectedNode(data)} className={cardStyles}>
                <div className={cardContent}>
                    <div className={titleRow}>
                        <div className={iconWrapper} style={{ backgroundColor: tokens.colorPaletteLavenderBackground2 }}>
                            <Agents24Regular style={{ color: tokens.colorPaletteLavenderForeground2 }} />
                        </div>
                        <div className={nameBlock}>
                            <Text className={nameText}>{data?.name}</Text>
                            <Text className={subtitleText}>
                                {intl.formatMessage(
                                    agentType === 'Orchestrator'
                                        ? ExtendedAgentsGraphResources.orchestrator
                                        : agentType === 'Activity'
                                          ? ExtendedAgentsGraphResources.activity
                                          : ExtendedAgentsGraphResources.autonomous
                                )}
                            </Text>
                        </div>
                        <MoreHorizontal16Regular />
                    </div>
                    <div className={badgeRow}>
                        <Badge appearance="outline" size="small" className={badge}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.toolsCountBadge, { count: toolCount })}
                        </Badge>
                    </div>
                </div>
                {isSelectedNode && agent?.name && (
                    <Menu positioning="above-end">
                        <MenuTrigger disableButtonEnhancement>
                            <Tooltip
                                content={intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickActionTooltip)}
                                relationship="label"
                                positioning="above"
                            >
                                <Button
                                    appearance="primary"
                                    shape="circular"
                                    icon={<Add24Regular />}
                                    className={quickActionButton}
                                    onClick={triggerAgentQuickAction ? undefined : handleOpenRelationships}
                                />
                            </Tooltip>
                        </MenuTrigger>
                        <MenuPopover className={menuPopover}>
                            <MenuList>
                                {triggerAgentQuickAction && (
                                    <MenuItem onClick={() => triggerAgentQuickAction(agent.name, 'addHandoff')}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.relationshipAddHandoffLabel)}
                                    </MenuItem>
                                )}
                                {triggerAgentQuickAction && (
                                    <MenuItem onClick={() => triggerAgentQuickAction(agent.name, 'addTool')}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.relationshipAddToolLabel)}
                                    </MenuItem>
                                )}
                                {triggerAgentQuickAction && (
                                    <MenuItem onClick={() => triggerAgentQuickAction(agent.name, 'createTool')}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateToolHeader)}
                                    </MenuItem>
                                )}
                                {triggerAgentQuickAction ? (
                                    <MenuItem onClick={() => triggerAgentQuickAction(agent.name, 'createAgent')}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateAgentHeader)}
                                    </MenuItem>
                                ) : (
                                    <MenuItem
                                        onClick={() => {
                                            if (agent?.name && openRelationshipDialog) {
                                                openRelationshipDialog(agent.name);
                                            }
                                        }}
                                    >
                                        {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateAgentHeader)}
                                    </MenuItem>
                                )}
                            </MenuList>
                        </MenuPopover>
                    </Menu>
                )}
            </Card>
        </div>
    );
};

ExtendedAgentCard.displayName = 'ExtendedAgentCard';
