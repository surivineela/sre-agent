import { makeStyles, tokens } from '@fluentui/react-components';
import { NodeSize } from '../Contracts/Graph';

export const useGraphStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'flex-start',
        width: '100vw',
        height: 'calc(100vh - 45px)',
        borderTop: '1px solid rgba(204,204,204,.8)',
    },
    reactFlow: {
        maxWidth: 'calc(100% - 280px)',
        minWidth: '400px',
        height: '100%',
        flex: '1 1 auto',
    },
    spinner: {
        position: 'fixed',
        left: '50%',
        top: '50%',
    },
});

export const useGraphNodeStyles = makeStyles({
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
    card: {
        width: `${NodeSize.width}px`,
        height: `${NodeSize.height}px`,
        position: 'relative',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: '15px',
        border: `1px solid`,
    },
    appGroupCard: {
        backgroundColor: tokens.colorBrandStroke2,
    },
    cardHightlight: {
        backgroundColor: tokens.colorBrandBackground2Hover,
    },
    header: {
        width: `calc(${NodeSize.width}px - 24px)`,
    },
    headerText: {
        textOverflow: 'ellipsis',
        overflow: 'hidden',
        width: `calc(${NodeSize.width}px - 76px)`,
    },
    description: {
        color: tokens.colorNeutralForeground3,
    },
});

export const useGraphEdgeStyles = makeStyles({
    hightlightedEdge: {
        stroke: tokens.colorBrandForegroundLinkHover,
    },
});

export const useResourceSelectorStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
        alignItems: 'flex-start',
        maxWidth: '300px',
        minWidth: '100px',
        height: '100%',
        flex: '1 1 auto',
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '35px 20px',
    },
    option: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
    },
    optionText: {
        wordBreak: 'break-word',
    },
    optionSubtext: {
        color: tokens.colorNeutralForeground3,
        wordBreak: 'break-word',
    },
});
