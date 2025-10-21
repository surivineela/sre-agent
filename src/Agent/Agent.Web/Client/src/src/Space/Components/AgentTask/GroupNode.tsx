import { Badge, makeStyles, Subtitle2, tokens } from '@fluentui/react-components';
import { Handle, NodeProps, Position } from '@xyflow/react';
import { memo } from 'react';
import { AgentTaskPhaseNodeIdSuffix, GraphFlowNode } from '../../Contracts/Activities';

const useStyles = makeStyles({
    root: {
        position: 'relative',
        width: '100%',
        height: '100%',
        backgroundColor: tokens.colorNeutralBackground2,
        border: `2px solid ${tokens.colorNeutralBackground3Selected} `,
        borderRadius: '24px',
    },
    title: {
        position: 'absolute',
        top: '-18px',
        left: '50%',
        transform: 'translateX(-50%)',
        padding: '10px',
        width: 'fit-content',
    },
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
});

const GroupNode = (props: NodeProps<GraphFlowNode>) => {
    const { root, title, handle } = useStyles();

    const isInitialInvestigation = props.data.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.InitialInvestigation);

    return (
        <div className={root}>
            <Badge className={title} size={'extra-large'}>
                <Subtitle2>{props.data.title}</Subtitle2>
            </Badge>
            {!isInitialInvestigation && <Handle type={'target'} position={Position.Top} className={handle} />}
            <Handle type={'source'} position={Position.Bottom} className={handle} />
        </div>
    );
};

export default memo(GroupNode);
