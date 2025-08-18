import { Card, CardHeader, makeStyles, Subtitle1, Subtitle2, tokens } from '@fluentui/react-components';
import { Handle, NodeProps, Position } from '@xyflow/react';
import { memo, useContext } from 'react';
import { AgentTaskNodeSize, GraphFlowNode } from '../../Contracts/Activities';
import { AgentTaskGraphContext } from '../../Contracts/Context';

const useStyles = makeStyles({
    nodeContainer: {
        width: `${AgentTaskNodeSize.ConclusionNode.width}px`,
        maxHeight: `${AgentTaskNodeSize.ConclusionNode.height}px`,
        position: 'relative',
    },
    tag: {
        position: 'absolute',
        top: '-22px',
        left: '50%',
        transform: 'translateX(-50%)',
        padding: '11px 10px',
        backgroundColor: tokens.colorPaletteBlueBorderActive,
        borderRadius: tokens.borderRadiusCircular,
        color: tokens.colorNeutralForegroundInverted2,
        zIndex: 2,
    },
    card: {
        border: `2px solid ${tokens.colorPaletteBlueBorderActive}`,
        borderRadius: tokens.borderRadiusXLarge,
        boxShadow: tokens.shadow4Brand,
        width: '100%',
        height: '100%',
        padding: '35px 16px',
        zIndex: 1,
    },
    title: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: '-webkit-box',
        WebkitLineClamp: 5,
        WebkitBoxOrient: 'vertical',
    },
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
});

const AgentTaskGraphFlowConclusionNode = (props: NodeProps<GraphFlowNode>) => {
    const { data, id } = props;

    const { nodeContainer, tag, card, title, handle } = useStyles();

    const { selectedNodeId, selectNode } = useContext(AgentTaskGraphContext);

    return (
        <div className={nodeContainer}>
            <Subtitle2 className={tag}>{'Conclusion'}</Subtitle2>
            <Card
                focusMode={'tab-only'}
                className={card}
                selected={selectedNodeId === id}
                onSelectionChange={(_, selection) => selectNode(selection.selected ? id : null)}
            >
                <CardHeader
                    header={
                        <Subtitle1 block={true} className={title}>
                            {data.title}
                        </Subtitle1>
                    }
                />
            </Card>
            <Handle type={'target'} position={Position.Top} isConnectable={false} className={handle} />
        </div>
    );
};

export default memo(AgentTaskGraphFlowConclusionNode);
