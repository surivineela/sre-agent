import { makeStyles, Subtitle2, tokens } from '@fluentui/react-components';
import { Handle, NodeProps, Position } from '@xyflow/react';
import { memo } from 'react';
import { GraphFlowNode } from '../Contracts/Activities';

const useStyles = makeStyles({
    root: {
        position: 'relative',
        width: '100%',
        height: '100%',
        backgroundColor: tokens.colorNeutralBackground2,
        border: `5px solid ${tokens.colorNeutralStroke3} `,
        borderRadius: tokens.borderRadiusXLarge,
    },
    title: {
        position: 'absolute',
        top: '-25px',
        left: '50%',
        transform: 'translateX(-50%)',
        padding: '10px',
        border: `2px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        width: '100px',
        backgroundColor: tokens.colorNeutralBackground1,
    },
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
});

const AgentTaskGraphFlowGroupNode = (props: NodeProps<GraphFlowNode>) => {
    const { root, title, handle } = useStyles();

    return (
        <div className={root}>
            <Subtitle2 className={title}>{props.data.title}</Subtitle2>
            <Handle type={'target'} position={Position.Top} className={handle} />
            <Handle type={'source'} position={Position.Bottom} className={handle} />
        </div>
    );
};

export default memo(AgentTaskGraphFlowGroupNode);
