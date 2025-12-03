import { Badge, Card, mergeClasses, Text } from '@fluentui/react-components';
import { Handle, Node, NodeProps, Position } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { McpServerResources } from '../../Strings/SREAgentResources';
import {
    ExtendedAgentGraphContext,
    ExtendedAgentGraphNode,
    ExtendedAgentNodeType,
    ExtendedTool,
    SystemTool,
} from '../Contracts/ExtendedAgentGraph';
import { useToolNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from './EntityIcon';
import { getHandleId } from './Utility';

type HandlePosition = 'T' | 'B' | 'L' | 'R';

const Handles = memo(() => {
    const { handle } = useToolNodeStyles();
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

Handles.displayName = 'ToolHandles';

export const ToolCard = (props: NodeProps<Node<ExtendedAgentGraphNode>>) => {
    const { id, data } = props;
    const { hoverNode, unHoverNode, nodesToHighlight, selectedNodeId, setSelectedNodeId, expandInfoPanel, hoveredNodeId } =
        useContext(ExtendedAgentGraphContext);

    const {
        toolCard,
        kustoToolCard,
        linkToolCard,
        cardHighlighted,
        cardHovered,
        cardSelected,
        cardContent,
        titleRow,
        nameBlock,
        nameText,
    } = useToolNodeStyles();
    const intl = useIntl();
    const isHovered = hoveredNodeId === id;
    const isSelectedNode = selectedNodeId === id;

    const isSystemTool = data?.type === ExtendedAgentNodeType.SystemTool;
    const tool = isSystemTool ? undefined : ((data?.data as ExtendedTool | undefined) ?? undefined);
    const systemTool = isSystemTool ? ((data?.data as SystemTool | undefined) ?? undefined) : undefined;
    const toolType = isSystemTool ? (systemTool?.category ?? 'System tool') : (tool?.type ?? 'Tool');

    const shouldAccent = isSelectedNode || isHovered;

    const isPythonTool = toolType === 'PythonFunctionTool';

    const cardStyles = mergeClasses(
        toolCard,
        shouldAccent && toolType === 'KustoTool' ? kustoToolCard : undefined,
        shouldAccent && toolType === 'LinkTool' ? linkToolCard : undefined,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? cardHovered : undefined,
        isSelectedNode ? cardSelected : undefined
    );

    const isMcpTool = useMemo(() => {
        return tool?.type?.toLowerCase() === 'mcp';
    }, [tool?.type]);

    const toolIconType = useMemo(() => {
        if (isSystemTool) {
            return 'toolWithGear';
        }
        if (isPythonTool) {
            return 'pythonTool';
        }
        if (isMcpTool) {
            return 'windowWrenchRegular';
        }
        return 'tool';
    }, [isPythonTool, isSystemTool, isMcpTool]);
    return (
        <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <Card
                onClick={() => {
                    setSelectedNodeId(data?.id);
                    expandInfoPanel();
                }}
                className={cardStyles}
            >
                <div className={cardContent}>
                    <div className={titleRow}>
                        <EntityIcon type={toolIconType} iconStyle={{ height: '24px', width: '24px' }} />
                        <div className={nameBlock}>
                            <Text className={nameText}>{data?.name}</Text>
                        </div>
                        <div style={{ display: 'flex', gap: '8px', flexShrink: 0 }}>
                            {!isSystemTool && tool?.connector && !isMcpTool && (
                                <Badge appearance="outline" size="tiny">
                                    {tool.connector}
                                </Badge>
                            )}
                            {!isSystemTool && isMcpTool && (
                                <Badge appearance="filled" size="small" color={'informative'}>
                                    {intl.formatMessage(McpServerResources.mcp)}
                                </Badge>
                            )}
                            {isSystemTool && systemTool?.resourceType && (
                                <Badge appearance="outline" size="tiny">
                                    {systemTool.resourceType}
                                </Badge>
                            )}
                        </div>
                    </div>
                </div>
            </Card>
        </div>
    );
};

ToolCard.displayName = 'ToolCard';
