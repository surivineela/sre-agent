import { Badge, Card, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { MoreHorizontal16Regular } from '@fluentui/react-icons';
import { Handle, Node, NodeProps, Position } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgentGraphContext, ExtendedAgentGraphNode, ExtendedTrigger } from '../Contracts/ExtendedAgentGraph';
import { getHumanReadableCronExpression } from '../ScheduledTasks/V2/ScheduledTasksUtilities';
import { useTriggerNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon, EntityIconProps } from './EntityIcon';
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
    const intl = useIntl();
    const { data, id } = props;

    const { selectedNode, setSelectedNode, hoverNode, unHoverNode, hoveredNodeId, nodesToHighlight } =
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
    const isSelectedNode = selectedNode?.id === id;

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
        const triggerStatus = trigger?.status ?? 'Active';
        let backgroundColor: string | undefined;
        let color: string | undefined;
        let borderColor: string | undefined;
        switch (triggerStatus) {
            case 'Active':
                backgroundColor = tokens.colorStatusSuccessBackground1;
                color = tokens.colorStatusSuccessForeground1;
                borderColor = tokens.colorStatusSuccessBorder1;
                break;
            case 'Paused':
                backgroundColor = tokens.colorStatusWarningBackground1;
                color = tokens.colorStatusWarningForeground1;
                borderColor = tokens.colorStatusWarningBorder1;
                break;
            case 'Disabled':
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
                {triggerStatus}
            </Badge>
        );
    }, [badge, trigger?.status]);

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
            <Card onClick={() => setSelectedNode(data)} className={cardStyles}>
                <div className={cardContent}>
                    <div className={titleRow}>
                        <EntityIcon type={triggerIconType} iconStyle={{ height: '24px', width: '24px' }} />
                        <div className={nameBlock}>
                            <Text className={nameText}>{data?.name}</Text>
                            <Text className={subtitleText}>{triggerSubtitle}</Text>
                        </div>
                        <MoreHorizontal16Regular />
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
