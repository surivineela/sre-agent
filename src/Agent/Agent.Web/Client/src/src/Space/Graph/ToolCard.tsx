import { Badge, Card, mergeClasses, Text } from '@fluentui/react-components';
import { Database24Regular, Link24Regular, Wrench24Regular } from '@fluentui/react-icons';
import { Handle, Node, NodeProps, Position } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../Strings/SREAgentResources';
import {
    ExtendedAgentGraphContext,
    ExtendedAgentGraphNode,
    ExtendedAgentNodeType,
    ExtendedTool,
    SystemTool,
} from '../Contracts/ExtendedAgentGraph';
import { useToolNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
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
    const { hoverNode, unHoverNode, nodesToHighlight, selectedNode, setSelectedNode, hoveredNodeId } =
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
        iconWrapper,
        nameBlock,
        nameText,
        subtitleText,
        descriptionText,
        footerRow,
        mutedText,
    } = useToolNodeStyles();
    const intl = useIntl();

    const isHovered = hoveredNodeId === id;
    const isSelectedNode = selectedNode?.id === id;

    const isSystemTool = data?.type === ExtendedAgentNodeType.SystemTool;
    const tool = isSystemTool ? undefined : ((data?.data as ExtendedTool | undefined) ?? undefined);
    const systemTool = isSystemTool ? ((data?.data as SystemTool | undefined) ?? undefined) : undefined;
    const toolType = isSystemTool ? (systemTool?.category ?? 'System tool') : (tool?.type ?? 'Tool');

    const shouldAccent = isSelectedNode || isHovered;

    const cardStyles = mergeClasses(
        toolCard,
        shouldAccent && toolType === 'KustoTool' ? kustoToolCard : undefined,
        shouldAccent && toolType === 'LinkTool' ? linkToolCard : undefined,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? cardHovered : undefined,
        isSelectedNode ? cardSelected : undefined
    );

    const ToolIcon = useMemo(() => {
        if (isSystemTool) {
            return Wrench24Regular;
        }

        switch (toolType) {
            case 'KustoTool':
                return Database24Regular;
            case 'LinkTool':
                return Link24Regular;
            default:
                return Wrench24Regular;
        }
    }, [isSystemTool, toolType]);

    const toolDescription = (isSystemTool ? systemTool?.description : tool?.description)?.trim();
    const connectorOrPlugin = isSystemTool
        ? (systemTool?.pluginName ?? intl.formatMessage(SreAgentResources.NA))
        : (tool?.connector ?? intl.formatMessage(SreAgentResources.NA));
    const connectorLabel = isSystemTool
        ? intl.formatMessage(ExtendedAgentsGraphResources.systemToolPluginLabel)
        : intl.formatMessage(ExtendedAgentsGraphResources.connectorLabel);
    const parameterCount = isSystemTool ? (systemTool?.parameters?.length ?? 0) : (tool?.parameters?.length ?? 0);

    return (
        <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <Card onClick={() => setSelectedNode(data)} className={cardStyles}>
                <div className={cardContent}>
                    <div className={titleRow}>
                        <div className={iconWrapper}>
                            <ToolIcon />
                        </div>
                        <div className={nameBlock}>
                            <Text className={nameText}>{data?.name}</Text>
                            <Text className={subtitleText}>{toolType}</Text>
                        </div>
                        {!isSystemTool && tool?.connector && (
                            <Badge appearance="outline" size="tiny">
                                {tool.connector}
                            </Badge>
                        )}
                        {isSystemTool && systemTool?.resourceType && (
                            <Badge appearance="outline" size="tiny">
                                {systemTool.resourceType}
                            </Badge>
                        )}
                    </div>

                    {toolDescription ? (
                        <Text className={descriptionText}>{toolDescription}</Text>
                    ) : (
                        <Text className={mutedText}>{intl.formatMessage(ExtendedAgentsGraphResources.noDescription)}</Text>
                    )}

                    <div className={footerRow}>
                        <Text className={mutedText}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.parametersSectionTitle)}: {parameterCount}
                        </Text>
                        <Text className={mutedText}>
                            {connectorLabel}: {connectorOrPlugin}
                        </Text>
                    </div>
                </div>
            </Card>
        </div>
    );
};

ToolCard.displayName = 'ToolCard';
