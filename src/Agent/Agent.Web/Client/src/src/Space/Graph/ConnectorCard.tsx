import { Card, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { Handle, Node, NodeProps, Position } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgentGraphContext, ExtendedAgentGraphNode, ExtendedConnector } from '../Contracts/ExtendedAgentGraph';
import { Badge } from '../Foundry/common/components/src/Badge/Badge';
import { useConnectorNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from './EntityIcon';
import { getHandleId } from './Utility';

type HandlePosition = 'T' | 'B' | 'L' | 'R';

const Handles = memo(() => {
    const { handle } = useConnectorNodeStyles();
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

Handles.displayName = 'ConnectorHandles';

export const ConnectorCard = (props: NodeProps<Node<ExtendedAgentGraphNode>>) => {
    const { id, data } = props;
    const { hoverNode, unHoverNode, nodesToHighlight, selectedNodeId, setSelectedNodeId, expandInfoPanel, hoveredNodeId } =
        useContext(ExtendedAgentGraphContext);

    const {
        connectorCard,
        cardHighlighted,
        cardHovered,
        cardSelected,
        cardContent,
        titleRow,
        nameBlock,
        nameText,
        subtitleText,
        badge,
        badgeRow,
    } = useConnectorNodeStyles();
    const intl = useIntl();

    const isHovered = hoveredNodeId === id;
    const isSelectedNode = selectedNodeId === id;

    const connector = data?.data as ExtendedConnector | undefined;
    const isEnabled = connector?.enabled ?? true;

    const cardStyles = mergeClasses(
        connectorCard,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? cardHovered : undefined,
        isSelectedNode ? cardSelected : undefined
    );

    const statusBadgeElement = useMemo(() => {
        let backgroundColor: string | undefined;
        let color: string | undefined;
        let borderColor: string | undefined;
        switch (!!isEnabled) {
            case true:
                backgroundColor = tokens.colorStatusSuccessBackground1;
                color = tokens.colorStatusSuccessForeground1;
                borderColor = tokens.colorStatusSuccessBorder1;
                break;
            case false:
                backgroundColor = tokens.colorNeutralBackgroundDisabled;
                color = tokens.colorNeutralForegroundDisabled;
                borderColor = tokens.colorNeutralForegroundDisabled;
                break;
            default:
                break;
        }

        return (
            <Badge
                appearance="outline"
                size="small"
                className={badge}
                style={{
                    backgroundColor: backgroundColor,
                    color: color,
                    borderColor: borderColor,
                }}
            >
                {isEnabled
                    ? intl.formatMessage(ExtendedAgentsGraphResources.connectorStatusEnabled)
                    : intl.formatMessage(ExtendedAgentsGraphResources.connectorStatusDisabled)}
            </Badge>
        );
    }, [badge, isEnabled]);

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
                        <EntityIcon type="connector" iconStyle={{ height: '24px', width: '24px' }} />
                        <div className={nameBlock}>
                            <Text className={nameText}>{data?.name}</Text>
                            <Text className={subtitleText}>{intl.formatMessage(ExtendedAgentsGraphResources.connector)}</Text>
                        </div>
                    </div>

                    <div className={badgeRow}>{statusBadgeElement}</div>
                </div>
            </Card>
        </div>
    );
};

ConnectorCard.displayName = 'ConnectorCard';
