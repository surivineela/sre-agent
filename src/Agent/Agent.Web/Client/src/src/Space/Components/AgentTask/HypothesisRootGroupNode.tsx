import { makeStyles, tokens } from '@fluentui/react-components';
import { Handle, Position } from '@xyflow/react';
import { memo } from 'react';

const useStyles = makeStyles({
    root: {
        position: 'relative',
        width: '100%',
        height: '100%',
        background: 'transparent',
        border: `${tokens.strokeWidthThicker} solid ${tokens.colorNeutralStroke2}`,
        borderRadius: '24px',
        zIndex: -99,
    },
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
});

const HypothesisRootGroupNode = () => {
    const { root, handle } = useStyles();

    return (
        <div className={root}>
            <Handle type={'target'} position={Position.Top} className={handle} />
            <Handle type={'source'} position={Position.Bottom} className={handle} />
        </div>
    );
};

export default memo(HypothesisRootGroupNode);
