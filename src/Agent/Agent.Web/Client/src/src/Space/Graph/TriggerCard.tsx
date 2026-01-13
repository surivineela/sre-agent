import { Badge, Card, mergeClasses, Text } from '@fluentui/react-components';
import { Handle, Node, NodeProps } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { getHumanReadableCronExpression } from '../../Common/Helpers/CronExpression';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgentGraphContext, ExtendedAgentGraphNode, ExtendedTrigger } from '../Contracts/ExtendedAgentGraph';
import { HANDLE_POSITION_MAP, HANDLE_POSITIONS } from '../Contracts/Graph';
import { useTriggerNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon, EntityIconProps } from './EntityIcon';
import { getHandleId } from './Utility';

const Handles = memo(() => {
    const { handle } = useTriggerNodeStyles();

    return (
        <>
            {HANDLE_POSITIONS.map(position => (
                <div key={position}>
                    <Handle className={handle} type="target" position={HANDLE_POSITION_MAP[position]} id={getHandleId(position, true)} />
                    <Handle className={handle} type="source" position={HANDLE_POSITION_MAP[position]} id={getHandleId(position, false)} />
                </div>
            ))}
        </>
    );
});

Handles.displayName = 'TriggerHandles';

export const TriggerCard = memo((props: NodeProps<Node<ExtendedAgentGraphNode>>) => {
    const intl = useIntl();
    const { data, id } = props;

    const { selectedNodeId, setSelectedNodeId, expandInfoPanel, hoverNode, unHoverNode, hoveredNodeId, nodesToHighlight } =
        useContext(ExtendedAgentGraphContext);

    const {
        triggerCard,
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
    } = useTriggerNodeStyles();

    const trigger = data?.data as ExtendedTrigger;
    const triggerType = trigger?.type;
    const isHovered = hoveredNodeId === id;
    const isSelectedNode = selectedNodeId === id;

    const cardStyles = mergeClasses(
        triggerCard,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? cardHovered : undefined,
        isSelectedNode ? cardSelected : undefined
    );

    const triggerIconType: EntityIconProps['type'] = useMemo(() => {
        switch (triggerType) {
            case 'incident':
                return 'incidentTrigger';
            case 'scheduled':
                return 'scheduledTask';
            default:
                return 'genericTrigger';
        }
    }, [triggerType]);

    const statusBadgeElement = useMemo(() => {
        return (
            <Badge
                appearance={trigger.status === 'Active' ? 'tint' : 'outline'}
                size="small"
                color={trigger.status === 'Active' ? 'success' : 'danger'}
            >
                {trigger.status === 'Active'
                    ? intl.formatMessage(ExtendedAgentsGraphResources.onLabel)
                    : intl.formatMessage(ExtendedAgentsGraphResources.offLabel)}
            </Badge>
        );
    }, [badge, trigger?.status, intl]);

    const chronBadgeElement = useMemo(() => {
        if (triggerType !== 'scheduled' || !trigger?.cronExpression) {
            return null;
        }

        return (
            <Badge appearance="outline" size="small" className={badge}>
                {getHumanReadableCronExpression(trigger.cronExpression, intl)}
            </Badge>
        );
    }, [triggerType, trigger.cronExpression, badge, intl]);

    const triggerSubtitle = useMemo(
        () =>
            triggerType === 'incident'
                ? intl.formatMessage(ExtendedAgentsGraphResources.triggerBadgeIncident)
                : triggerType === 'scheduled'
                    ? intl.formatMessage(ExtendedAgentsGraphResources.triggerBadgeScheduled)
                    : '',
        [triggerType, intl]
    );

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
                        <EntityIcon type={triggerIconType} iconStyle={{ height: '24px', width: '24px' }} />
                        <div className={nameBlock}>
                            <Text className={nameText}>{data?.name}</Text>
                            <Text className={subtitleText}>{triggerSubtitle}</Text>
                        </div>
                    </div>

                    <div className={badgeRow}>
                        {statusBadgeElement}
                        {chronBadgeElement}
                    </div>
                </div>
            </Card>
        </div>
    );
});

TriggerCard.displayName = 'TriggerCard';
