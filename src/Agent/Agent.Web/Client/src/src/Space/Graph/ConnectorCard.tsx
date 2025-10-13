import { Card, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { PlugConnected24Regular, PlugDisconnected24Regular } from '@fluentui/react-icons';
import { Handle, Node, NodeProps, Position } from '@xyflow/react';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ExtendedAgentGraphContext, ExtendedAgentGraphNode, ExtendedConnector } from '../Contracts/ExtendedAgentGraph';
import { useConnectorNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
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
    const { hoverNode, unHoverNode, nodesToHighlight, selectedNode, setSelectedNode, hoveredNodeId } =
        useContext(ExtendedAgentGraphContext);

    const {
        connectorCard,
        connectorEnabledCard,
        connectorDisabledCard,
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
        mutedText,
        statusBadge,
    } = useConnectorNodeStyles();
    const intl = useIntl();

    const isHovered = hoveredNodeId === id;
    const isSelectedNode = selectedNode?.id === id;

    const connector = data?.data as ExtendedConnector | undefined;
    const isEnabled = connector?.enabled ?? true;

    const cardStyles = mergeClasses(
        connectorCard,
        isEnabled ? connectorEnabledCard : connectorDisabledCard,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? cardHovered : undefined,
        isSelectedNode ? cardSelected : undefined
    );

    const ConnectorIcon = isEnabled ? PlugConnected24Regular : PlugDisconnected24Regular;
    const connectorDescription = connector?.description?.trim();
    const connectorType = connector?.type ?? intl.formatMessage(SreAgentResources.NA);

    return (
        <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <Card onClick={() => setSelectedNode(data)} className={cardStyles}>
                <div className={cardContent}>
                    <div className={titleRow}>
                        <div className={iconWrapper}>
                            <ConnectorIcon />
                        </div>
                        <div className={nameBlock}>
                            <Text className={nameText}>{data?.name}</Text>
                            <Text className={subtitleText}>{connectorType}</Text>
                        </div>
                    </div>

                    {connectorDescription ? (
                        <Text className={descriptionText}>{connectorDescription}</Text>
                    ) : (
                        <Text className={mutedText}>{intl.formatMessage(ExtendedAgentsGraphResources.noDescription)}</Text>
                    )}
                </div>
                <div className={statusBadge}>
                    <div
                        style={{
                            width: '12px',
                            height: '12px',
                            borderRadius: '50%',
                            backgroundColor: isEnabled ? tokens.colorPaletteGreenForeground1 : tokens.colorPaletteRedForeground1,
                        }}
                    />
                </div>
            </Card>
        </div>
    );
};

ConnectorCard.displayName = 'ConnectorCard';
