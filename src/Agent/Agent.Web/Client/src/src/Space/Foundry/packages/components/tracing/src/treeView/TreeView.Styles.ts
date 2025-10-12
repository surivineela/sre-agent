import { makeStyles, tokens } from '@fluentui/react-components';

export const useTreeViewStyles = makeStyles({
    traceTreePath: {
        position: 'absolute',
        borderBottom: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
        borderLeft: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
        borderBottomLeftRadius: tokens.borderRadiusXLarge,
    },

    pathLayerWrapper: {
        position: 'relative',
        height: '0px',
        width: '0px',
    },

    pathLayer: {
        position: 'absolute',
    },

    tree: {
        overflow: 'hidden',
        padding: '24px',
    },
});
