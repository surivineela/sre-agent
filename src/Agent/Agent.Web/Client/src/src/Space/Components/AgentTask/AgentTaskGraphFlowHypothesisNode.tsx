import { Body1, Body2, Card, CardFooter, CardHeader, makeStyles, tokens } from '@fluentui/react-components';
import { Handle, NodeProps, Position } from '@xyflow/react';
import { memo, useContext } from 'react';
import { HypothesisStatus } from '../../../Common/Contracts/DataPlane/AgentTask';
import { AgentTaskNodeSize, GraphFlowNode } from '../../Contracts/Activities';
import { AgentTaskGraphContext } from '../../Contracts/Context';
import NodeStatusPill from './NodeStatusPill';

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
    const { data, id } = props;

    const { nodeContainer, card, title, description, cardFooter, handle } = useStyles();

    const { selectedNodeId, selectNode } = useContext(AgentTaskGraphContext);

    return (
        <div className={nodeContainer}>
            <Card
                focusMode={'tab-only'}
                className={card}
                style={{ border: `1.5px solid ${getCardBorderColor(data.status)}` }}
                selected={selectedNodeId === id}
                onSelectionChange={(_, selection) => selectNode(selection.selected ? id : null)}
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
                    <NodeStatusPill status={data.status} showIcon={true} />
                </CardFooter>
            </Card>
            <Handle type={'target'} position={Position.Top} isConnectable={false} className={handle} />
            <Handle type={'source'} position={Position.Bottom} isConnectable={false} className={handle} />
        </div>
    );
};

export default memo(AgentTaskGraphFlowHypothesisNode);
