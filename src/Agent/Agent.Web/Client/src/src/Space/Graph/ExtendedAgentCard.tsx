import { Badge, Card, mergeClasses, Text } from '@fluentui/react-components';
import { Handle, Node, NodeProps, Position } from '@xyflow/react';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedAgentGraphContext, ExtendedAgentGraphNode } from '../Contracts/ExtendedAgentGraph';
import { useExtendedAgentNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
import { AgentLeftQuickActionButton } from './AgentLeftQuickActionButton';
import { AgentRightQuickActionButton } from './AgentRightQuickActionButton';
import { EntityIcon } from './EntityIcon';
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

    const { hoverNode, unHoverNode, nodesToHighlight, selectedNode, setSelectedNode, expandInfoPanel, hoveredNodeId } =
        useContext(ExtendedAgentGraphContext);

    const {
        cardWrapper,
        agentCard,
        cardHighlighted,
        cardHovered,
        cardSelected,
        cardContent,
        titleRow,
        nameBlock,
        nameText,
        subtitleText,
        badgeRow,
        badge,
    } = useExtendedAgentNodeStyles();

    const intl = useIntl();

    const isHovered = hoveredNodeId === id;
    const isSelectedNode = selectedNode?.id === id;

    const agent = data?.data as ExtendedAgent | undefined;
    const agentType = agent?.agentType || 'Autonomous';

    // Memory is enabled if the SearchMemory tool is available in the agent's tools
    const memoryEnabled =
        agent?.tools?.some(t => t.toLowerCase() === 'searchmemory') ||
        agent?.systemTools?.some(t => t.toLowerCase() === 'searchmemory') ||
        false;

    const cardStyles = mergeClasses(
        agentCard,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? cardHovered : undefined,
        isSelectedNode ? cardSelected : undefined
    );

    const toolCount = agent?.tools?.length || 0;

    return (
        <div className={cardWrapper}>
            {agent?.name && <AgentLeftQuickActionButton agent={agent} />}
            <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
                <Handles />
                <Card onClick={() => (setSelectedNode(data), expandInfoPanel())} className={cardStyles}>
                    <div className={cardContent}>
                        <div className={titleRow}>
                            <EntityIcon
                                type={agent?.name === 'meta_agent' ? 'metaAgent' : 'agent'}
                                iconStyle={{ height: '24px', width: '24px' }}
                            />
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
                        </div>
                        <div className={badgeRow}>
                            <Badge appearance="outline" size="small" className={badge}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.toolsCountBadge, { count: toolCount })}
                            </Badge>
                            {memoryEnabled && (
                                <Badge appearance="outline" size="small" className={badge}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.memoryEnabledBadge)}
                                </Badge>
                            )}
                        </div>
                    </div>
                </Card>
            </div>
            {agent?.name && <AgentRightQuickActionButton agent={agent} />}
        </div>
    );
};

ExtendedAgentCard.displayName = 'ExtendedAgentCard';
