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
        borderRadius: tokens.borderRadiusMedium,
        boxShadow: tokens.shadow4,
        gap: '8px',
    },
    appGroupCard: {
        backgroundColor: tokens.colorBrandBackground2Hover,
        '&:hover': {
            backgroundColor: tokens.colorBrandBackground2Hover,
        },
    },
    cardHighlighted: {
        backgroundColor: tokens.colorNeutralBackground2,
        boxShadow: tokens.shadow8,
    },
    cardHovered: {
        backgroundColor: tokens.colorNeutralBackground2,
        boxShadow: `${tokens.shadow16} !important`,
    },
    appGroupCardHovered: {
        backgroundColor: tokens.colorBrandBackground2Hover,
        boxShadow: `${tokens.shadow16} !important`,
    },
    cardSelected: {
        boxShadow: tokens.shadow4,
        border: `2px solid ${tokens.colorBrandStroke1}`,
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
        color: `${tokens.colorNeutralForeground3} !important`,
    },
});

export const useGraphEdgeStyles = makeStyles({
    highlightedEdge: {
        stroke: tokens.colorBrandForegroundLinkHover,
    },
});

export const useResourceSelectorStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        alignItems: 'flex-start',
        maxWidth: '300px',
        minWidth: '100px',
        height: 'calc(100% - 40px)',
        overflowY: 'auto',
        flex: '1 1 auto',
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '20px',
    },
    field: {
        width: '100%',
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
