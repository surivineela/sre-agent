import { Badge, Card, mergeClasses, Text } from '@fluentui/react-components';
import { Alert24Regular, Clock24Regular, Pause24Regular, Play24Regular } from '@fluentui/react-icons';
import { Handle, Node, NodeProps, Position } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgentGraphContext, ExtendedAgentGraphNode, ExtendedTrigger } from '../Contracts/ExtendedAgentGraph';
import { useTriggerNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
import { getHandleId } from './Utility';

type HandlePosition = 'T' | 'B' | 'L' | 'R';

const Handles = memo(() => {
    const { handle } = useTriggerNodeStyles();
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

Handles.displayName = 'TriggerHandles';

export const TriggerCard = memo((props: NodeProps<Node<ExtendedAgentGraphNode>>) => {
    const { data, id } = props;
    const intl = useIntl();

    const { selectedNode, setSelectedNode, hoverNode, unHoverNode, hoveredNodeId, nodesToHighlight } =
        useContext(ExtendedAgentGraphContext);

    const {
        triggerCard,
        incidentTriggerCard,
        scheduledTriggerCard,
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
        footerRow,
    } = useTriggerNodeStyles();

    const trigger = data?.data as ExtendedTrigger;
    const triggerType = trigger?.type;
    const isHovered = hoveredNodeId === id;
    const isSelectedNode = selectedNode?.id === id;

    const cardStyles = mergeClasses(
        triggerCard,
        triggerType === 'incident' ? incidentTriggerCard : undefined,
        triggerType === 'scheduled' ? scheduledTriggerCard : undefined,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? cardHovered : undefined,
        isSelectedNode ? cardSelected : undefined
    );

    const TriggerIcon = useMemo(() => {
        switch (triggerType) {
            case 'incident':
                return Alert24Regular;
            case 'scheduled':
                return Clock24Regular;
            default:
                return Play24Regular;
        }
    }, [triggerType]);

    const StatusIcon = trigger?.status === 'Paused' ? Pause24Regular : Play24Regular;

    const triggerDescription = trigger?.description?.trim();
    const triggerStatus = trigger?.status ?? 'Active';
    const triggerSubtitle =
        triggerType === 'incident'
            ? `${trigger?.priority || 'All priorities'} • ${trigger?.incidentType || 'All types'}`
            : trigger?.cronExpression
              ? `${trigger.cronExpression} (${trigger.timezone || 'UTC'})`
              : 'Custom schedule';

    return (
        <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <Card onClick={() => setSelectedNode(data)} className={cardStyles}>
                <div className={cardContent}>
                    <div className={titleRow}>
                        <div className={iconWrapper}>
                            <TriggerIcon />
                        </div>
                        <div className={nameBlock}>
                            <Text className={nameText}>{data?.name}</Text>
                            <Text className={subtitleText}>{triggerSubtitle}</Text>
                        </div>
                    </div>

                    {triggerDescription ? (
                        <Text className={descriptionText}>{triggerDescription}</Text>
                    ) : (
                        <Text className={mutedText}>{intl.formatMessage(ExtendedAgentsGraphResources.noDescription)}</Text>
                    )}

                    <div className={footerRow}>
                        <Text className={mutedText}>Agent: {trigger?.agentName || 'Not specified'}</Text>
                        <div className={statusBadge}>
                            <StatusIcon style={{ width: '12px', height: '12px', marginRight: '4px' }} />
                            <Badge
                                appearance={triggerStatus === 'Active' ? 'filled' : 'outline'}
                                color={triggerStatus === 'Active' ? 'success' : triggerStatus === 'Paused' ? 'warning' : 'danger'}
                                size="small"
                            >
                                {triggerStatus}
                            </Badge>
                        </div>
                    </div>
                </div>
            </Card>
        </div>
    );
});

TriggerCard.displayName = 'TriggerCard';
