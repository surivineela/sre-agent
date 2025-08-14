import { Body1, Body2, Card, CardFooter, CardHeader, makeStyles, Text, tokens } from '@fluentui/react-components';
import { ArrowClockwiseRegular, CheckmarkCircleRegular, DismissCircleRegular, QuestionCircleRegular } from '@fluentui/react-icons';
import { Handle, NodeProps, Position } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { HypothesisStatus } from '../../Common/Contracts/DataPlane/AgentTask';
import { AgentTaskNodeSize, GraphFlowNode } from '../Contracts/Activities';
import { AgentTaskContext } from '../Contracts/Context';

const useStyles = makeStyles({
    nodeContainer: {
        position: 'relative',
        width: `${AgentTaskNodeSize.HypothesisNode.width}px`,
        height: `${AgentTaskNodeSize.HypothesisNode.height}px`,
    },
    card: {
        borderRadius: tokens.borderRadiusXLarge,
        width: '100%',
        position: 'relative',
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        zIndex: 0,
        padding: '16px 16px 16px 20px',
    },
    title: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: '-webkit-box',
        WebkitLineClamp: 2,
        WebkitBoxOrient: 'vertical',
        fontWeight: tokens.fontWeightSemibold,
        width: '100%',
    },
    description: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: '-webkit-box',
        WebkitLineClamp: 2,
        WebkitBoxOrient: 'vertical',
        marginTop: '10px',
    },
    cardFooter: {
        justifySelf: 'flex-end',
    },
    statusContainer: {
        padding: '3px 10px',
        width: '100%',
        borderRadius: tokens.borderRadiusCircular,
        display: 'flex',
        flexDirection: 'row',
        gap: tokens.spacingHorizontalXS,
        alignItems: 'center',
    },
    statusTextFont: {
        fontSize: tokens.fontSizeBase400,
    },
    statusIconFont: {
        fontSize: tokens.fontSizeBase500,
    },
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
});

const getCardBorderColor = (status: string) => {
    switch (status) {
        case HypothesisStatus.Validated:
            return tokens.colorStatusSuccessBackground2;
        case HypothesisStatus.Invalidated:
            return tokens.colorNeutralBackground3;
        case HypothesisStatus.Inconclusive:
            return tokens.colorStatusWarningBackground2;
        default:
            return tokens.colorNeutralBackground2;
    }
};

const AgentTaskGraphFlowHypothesisNode = (props: NodeProps<GraphFlowNode>) => {
    const { data } = props;

    const { nodeContainer, card, title, description, cardFooter, handle } = useStyles();

    const { selectedNode, selectNode } = useContext(AgentTaskContext);

    return (
        <div className={nodeContainer}>
            <Card
                focusMode={'tab-only'}
                className={card}
                style={{ border: `1.5px solid ${getCardBorderColor(data.status)}` }}
                selected={selectedNode === data.id}
                onSelectionChange={(_, selection) => selectNode(selection.selected ? data.id : null)}
            >
                <CardHeader
                    header={
                        <Body2 block={true} className={title}>
                            {data.title}
                        </Body2>
                    }
                    description={<Body1 className={description}>{data.description}</Body1>}
                />
                <CardFooter className={cardFooter}>
                    <StatusIndicator status={data.status} />
                </CardFooter>
            </Card>
            <Handle type={'target'} position={Position.Top} isConnectable={false} className={handle} />
            <Handle type={'source'} position={Position.Bottom} isConnectable={false} className={handle} />
        </div>
    );
};

const StatusIndicator = memo(({ status }: { status: string }) => {
    const { statusContainer, statusTextFont, statusIconFont } = useStyles();

    const statusProps = useMemo(() => {
        const backgroundColor = getCardBorderColor(status);
        switch (status) {
            case HypothesisStatus.Validated:
                return {
                    text: 'Validated',
                    fontColor: tokens.colorNeutralForegroundInverted,
                    icon: CheckmarkCircleRegular,
                    backgroundColor: tokens.colorPaletteGreenBackground3,
                };
            case HypothesisStatus.Invalidated:
                return {
                    text: 'Invalidated',
                    fontColor: undefined,
                    icon: DismissCircleRegular,
                    backgroundColor,
                };
            case HypothesisStatus.Inconclusive:
                return {
                    text: 'Inconclusive',
                    fontColor: tokens.colorStatusWarningForeground3,
                    icon: QuestionCircleRegular,
                    borderColor: backgroundColor,
                };
            default:
                return {
                    text: 'Pending',
                    fontColor: tokens.colorNeutralForegroundInverted,
                    icon: ArrowClockwiseRegular,
                    backgroundColor: tokens.colorPaletteBlueBorderActive,
                };
        }
    }, [status]);

    return (
        <div
            className={statusContainer}
            style={{
                backgroundColor: statusProps.backgroundColor,
                border: statusProps.borderColor ? `1.5px solid ${statusProps.borderColor}` : 'none',
            }}
        >
            <statusProps.icon className={statusIconFont} style={statusProps.fontColor ? { color: statusProps.fontColor } : undefined} />
            <Text className={statusTextFont} weight={'semibold'} style={{ color: statusProps.fontColor }}>
                {statusProps.text}
            </Text>
        </div>
    );
});

export default memo(AgentTaskGraphFlowHypothesisNode);
