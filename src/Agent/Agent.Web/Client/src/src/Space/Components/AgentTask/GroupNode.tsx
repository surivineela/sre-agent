import { makeStyles, mergeClasses, Subtitle2, tokens } from '@fluentui/react-components';
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
        borderRadius: tokens.borderRadiusXLarge,
    },
    initialInvestigationGroupRoot: {
        backgroundColor: tokens.colorBrandBackground2,
        border: `1px solid ${tokens.colorBrandForeground1}`,
    },
    title: {
        position: 'absolute',
        top: '-25px',
        left: '50%',
        transform: 'translateX(-50%)',
        padding: '10px',
        border: `0.5px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        width: 'fit-content',
        backgroundColor: tokens.colorNeutralBackground1,
    },
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
});

const GroupNode = (props: NodeProps<GraphFlowNode>) => {
    const { root, initialInvestigationGroupRoot, title, handle } = useStyles();

    const isInitialInvestigation = props.data.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.InitialInvestigation);
    const rootClassName = mergeClasses(root, isInitialInvestigation && initialInvestigationGroupRoot);

    return (
        <div className={rootClassName}>
            <Subtitle2 className={title}>{props.data.title}</Subtitle2>
            {!isInitialInvestigation && <Handle type={'target'} position={Position.Top} className={handle} />}
            <Handle type={'source'} position={Position.Bottom} className={handle} />
        </div>
    );
};

export default memo(GroupNode);
