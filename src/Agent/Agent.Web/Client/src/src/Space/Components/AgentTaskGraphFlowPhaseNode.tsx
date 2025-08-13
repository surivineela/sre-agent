import { Body1, Card, CardHeader, makeStyles, Subtitle1, tokens } from '@fluentui/react-components';
import { ArrowClockwiseFilled, CheckmarkCircleFilled, DismissCircleFilled } from '@fluentui/react-icons';
import { Handle, NodeProps, Position } from '@xyflow/react';
import { memo, useMemo } from 'react';
import { InvestigationStatusCommon, TaskProgressStatus } from '../../Common/Contracts/DataPlane/AgentTask';
import { AgentTaskNodeSize, AgentTaskPhaseNodeIdSuffix, GraphFlowNode } from '../Contracts/Activities';

const useStyles = makeStyles({
    nodeContainer: {
        pointer: 'cursor',
        width: `${AgentTaskNodeSize.PhaseNode.width}px`,
        height: `${AgentTaskNodeSize.PhaseNode.height}px`,
    },
    statusIndicator: {
        fontSize: '30px',
        minWidth: '30px',
    },
    card: {
        borderRadius: tokens.borderRadiusXLarge,
        width: '100%',
        height: '100%',
        boxShadow: tokens.shadow4Brand,
        padding: '16px',
    },
    header: {
        display: 'flex',
        gap: `${tokens.spacingHorizontalM}`,
        justifyContent: 'space-between',
        alignItems: 'center',
        width: '100%',
    },
    title: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: '-webkit-box',
        WebkitLineClamp: 2,
        WebkitBoxOrient: 'vertical',
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

const AgentTaskGraphFlowPhaseNode = (props: NodeProps<GraphFlowNode>) => {
    const { data } = props;

    const { nodeContainer, statusIndicator, card, header, title, description, handle } = useStyles();

    const isInitialInvestigation = data.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.InitialInvestigation);
    const isConclusion = data.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.Conclusion);

    const backgroundColor = useMemo(() => {
        switch (data.status) {
            case InvestigationStatusCommon.Complete:
            case TaskProgressStatus.Completed:
                return tokens.colorStatusSuccessBackground1;
            case InvestigationStatusCommon.InProgress:
            case TaskProgressStatus.InProgress:
                return tokens.colorPaletteBlueBackground2;
            case TaskProgressStatus.Failed:
                return tokens.colorStatusDangerBackground1;
            default:
                return tokens.colorNeutralBackground2;
        }
    }, [data.status]);

    const StatusIndicator = () => {
        switch (data.status) {
            case InvestigationStatusCommon.Complete:
            case TaskProgressStatus.Completed:
                return <CheckmarkCircleFilled className={statusIndicator} style={{ color: tokens.colorStatusSuccessForeground3 }} />;
            case InvestigationStatusCommon.InProgress:
            case TaskProgressStatus.InProgress:
                return <ArrowClockwiseFilled className={statusIndicator} style={{ color: tokens.colorPaletteBlueForeground2 }} />;
            case TaskProgressStatus.Failed:
            case 'error':
                return <DismissCircleFilled className={statusIndicator} style={{ color: tokens.colorStatusDangerForeground3 }} />;
            default:
                return null;
        }
    };

    return (
        <div className={nodeContainer}>
            <Card className={card} style={{ backgroundColor: backgroundColor, border: `1px solid ${backgroundColor}` }}>
                <CardHeader
                    header={
                        <div className={header}>
                            <Subtitle1 block={true} className={title}>
                                {data.title}
                            </Subtitle1>
                            <StatusIndicator />
                        </div>
                    }
                />
                <Body1 className={description}>{data.description}</Body1>
            </Card>
            {!isInitialInvestigation && <Handle type={'target'} position={Position.Top} isConnectable={false} className={handle} />}
            {!isConclusion && <Handle type={'source'} position={Position.Bottom} isConnectable={false} className={handle} />}
        </div>
    );
};

export default memo(AgentTaskGraphFlowPhaseNode);
