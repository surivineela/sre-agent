import { Body1, Body2, Card, CardHeader, makeStyles, Text, tokens } from '@fluentui/react-components';
import { Handle, NodeProps, Position } from '@xyflow/react';
import { memo } from 'react';
import { HypothesisStatus } from '../../Common/Contracts/DataPlane/AgentTask';
import { AgentTaskNodeSize, GraphFlowNode } from '../Contracts/Activities';

const useStyles = makeStyles({
    nodeContainer: {
        position: 'relative',
        width: `${AgentTaskNodeSize.HypothesisNode.width}px`,
    },
    statusIndicator: {
        padding: '5px 8px',
        flex: '1 0 auto',
        borderRadius: tokens.borderRadiusMedium,
    },
    card: {
        border: `1px solid ${tokens.colorNeutralBackground2}`,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusXLarge,
        width: '100%',
        position: 'relative',
        height: '100%',
        boxShadow: tokens.shadow4Brand,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        zIndex: 0,
        padding: '16px 16px 16px 20px',
    },
    header: {
        display: 'flex',
        gap: `${tokens.spacingHorizontalM}`,
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        width: '100%',
    },
    title: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: '-webkit-box',
        WebkitLineClamp: 2,
        WebkitBoxOrient: 'vertical',
        fontWeight: tokens.fontWeightSemibold,
    },
    description: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: '-webkit-box',
        WebkitLineClamp: 3,
        WebkitBoxOrient: 'vertical',
    },
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
});

const AgentTaskGraphFlowHypothesisNode = (props: NodeProps<GraphFlowNode>) => {
    const { data } = props;

    const { nodeContainer, statusIndicator, card, header, title, description, handle } = useStyles();

    const getStatusStyle = () => {
        switch (data.status) {
            case HypothesisStatus.Validated:
                return {
                    color: tokens.colorStatusSuccessForeground3,
                    backgroundColor: tokens.colorStatusSuccessBackground2,
                };
            case HypothesisStatus.Invalidated:
                return {
                    color: tokens.colorStatusDangerForeground3,
                    backgroundColor: tokens.colorStatusDangerBackground2,
                };
            case HypothesisStatus.Inconclusive:
                return {
                    color: tokens.colorStatusWarningForeground3,
                    backgroundColor: tokens.colorStatusWarningBackground2,
                };
            default:
                return {
                    color: tokens.colorPaletteBlueForeground2,
                    backgroundColor: tokens.colorPaletteBlueBackground2,
                };
        }
    };

    const getStatusDisplayText = () => {
        switch (data.status) {
            case HypothesisStatus.Validated:
                return 'Validated';
            case HypothesisStatus.Invalidated:
                return 'Invalidated';
            case HypothesisStatus.Inconclusive:
                return 'Inconclusive';
            default:
                return 'Pending';
        }
    };

    return (
        <div className={nodeContainer}>
            <Card className={card}>
                <CardHeader
                    header={
                        <div className={header}>
                            <Body2 block={true} className={title}>
                                {data.title}
                            </Body2>
                            <Text wrap={false} className={statusIndicator} style={{ ...getStatusStyle() }}>
                                {getStatusDisplayText()}
                            </Text>
                        </div>
                    }
                />
                <Body1 className={description}>{data.description}</Body1>
            </Card>
            <Handle type={'target'} position={Position.Top} isConnectable={false} className={handle} />
            <Handle type={'source'} position={Position.Bottom} isConnectable={false} className={handle} />
        </div>
    );
};

export default memo(AgentTaskGraphFlowHypothesisNode);
