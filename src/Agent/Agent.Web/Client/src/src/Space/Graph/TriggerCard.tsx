import { Badge, Card, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { MoreHorizontal16Regular, Play24Regular, Timer24Regular, Warning24Regular } from '@fluentui/react-icons';
import { Handle, Node, NodeProps, Position } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgentGraphContext, ExtendedAgentGraphNode, ExtendedTrigger } from '../Contracts/ExtendedAgentGraph';
import { getHumanReadableCronExpression } from '../ScheduledTasks/V2/ScheduledTasksUtilities';
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
        iconWrapper,
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

    const triggerIcon = useMemo(() => {
        switch (triggerType) {
            case 'incident':
                return (
                    <div className={iconWrapper} style={{ backgroundColor: tokens.colorPaletteCranberryBackground2 }}>
                        <Warning24Regular style={{ color: tokens.colorPaletteCranberryForeground2 }} />
                    </div>
                );
            case 'scheduled':
                return (
                    <div className={iconWrapper} style={{ backgroundColor: tokens.colorPaletteForestBackground2 }}>
                        <Timer24Regular style={{ color: tokens.colorPaletteForestForeground2 }} />
                    </div>
                );
            default:
                return (
                    <div className={iconWrapper} style={{ backgroundColor: tokens.colorNeutralBackground3 }}>
                        <Play24Regular style={{ color: tokens.colorNeutralForeground3 }} />
                    </div>
                );
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
    }, [trigger?.status]);

    const chronBadgeElement = useMemo(() => {
        if (triggerType !== 'scheduled' || !trigger?.cronExpression) {
            return null;
        }

        return (
            <Badge appearance="outline" size="small" className={badge}>
                {getHumanReadableCronExpression(trigger.cronExpression, intl)}
            </Badge>
        );
    }, [triggerType, trigger?.cronExpression]);

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
                        {triggerIcon}
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
