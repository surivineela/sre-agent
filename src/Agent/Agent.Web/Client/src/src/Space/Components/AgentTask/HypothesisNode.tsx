import { Body1, Body2, Button, Card, CardFooter, CardHeader, makeStyles, tokens, useRestoreFocusTarget } from '@fluentui/react-components';
import { ChevronDownUpRegular, ChevronUpDownRegular } from '@fluentui/react-icons';
import { Handle, NodeProps, Position } from '@xyflow/react';
import { memo, useContext } from 'react';
import { AgentTaskNodeSize, GraphFlowNode } from '../../Contracts/Activities';
import { AgentTaskContext, AgentTaskGraphContext } from '../../Contracts/Context';
import NodeStatusPill from './NodeStatusPill';
import { getHypothesisNodeThemeColor } from './Utility';

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
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'space-between',
        width: '100%',
    },
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
});

const HypothesisNode = (props: NodeProps<GraphFlowNode>) => {
    const { data, id } = props;

    const { nodeContainer, card, title, description, cardFooter, handle } = useStyles();

    const { toggleNode } = useContext(AgentTaskContext);
    const { selectedNodeId, selectNode } = useContext(AgentTaskGraphContext);

    const restoreFocusTargetAttributes = useRestoreFocusTarget();

    return (
        <div className={nodeContainer}>
            <Card
                {...restoreFocusTargetAttributes}
                focusMode={'tab-only'}
                className={card}
                style={{ border: `1.5px solid ${getHypothesisNodeThemeColor(data.status)}` }}
                selected={selectedNodeId === id}
                onSelectionChange={(e, selection) => {
                    e.stopPropagation();
                    selectNode(selection.selected ? id : null);
                }}
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
                    {!data.isChild && data.hasChildren && (
                        <Button
                            appearance="transparent"
                            icon={data.expanded ? <ChevronDownUpRegular /> : <ChevronUpDownRegular />}
                            onClick={e => {
                                e.stopPropagation();
                                toggleNode(id);
                            }}
                        >
                            {data.expanded ? 'Collapse' : 'Expand'}
                        </Button>
                    )}
                </CardFooter>
            </Card>
            <Handle type={'target'} position={Position.Top} isConnectable={false} className={handle} />
            <Handle type={'source'} position={Position.Bottom} isConnectable={false} className={handle} />
        </div>
    );
};

export default memo(HypothesisNode);
