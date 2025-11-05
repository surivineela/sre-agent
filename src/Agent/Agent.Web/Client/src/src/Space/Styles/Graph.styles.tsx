import { makeStyles, tokens } from '@fluentui/react-components';
import { NodeSize } from '../Contracts/Graph';

export const useGraphStyles = makeStyles({
    visualRoot: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'flex-start',
        alignItems: 'flex-start',
        width: '100%',
        height: '100%',
        overflow: 'hidden',
        flex: 1,
    },
    reactFlow: {
        width: '100%',
        height: '100%',
        position: 'relative',
    },
    spinner: {
        position: 'fixed',
        left: '50%',
        top: '50%',
    },
    messageBar: {
        marginTop: '10px',
        marginLeft: '10px',
        marginRight: '10px',
    },
    container: {
        flex: '1 1 auto',
        padding: '1rem',
        paddingRight: 0,
        paddingTop: 0,
        backgroundColor: tokens.colorNeutralBackground1,
        borderTopLeftRadius: tokens.borderRadiusXLarge,
        boxShadow: tokens.shadow4,
        display: 'flex',
        flexDirection: 'row',
        overflow: 'hidden',
        position: 'relative',
    },
    rootContainer: {
        display: 'flex',
        flexDirection: 'column',
        height: 'calc(100vh - 60px)',
        padding: '10px ',
        borderTop: '1px solid rgba(204, 204, 204, 0.8)',
        backgroundColor: tokens.colorNeutralBackground3,
        gap: '0.25rem',
    },
    radioGroupContainer: {
        paddingRight: '1rem',
        paddingBottom: '0.25rem',
        flex: 'none',
    },
});

export const useIntegratedSelectorStyles = makeStyles({
    selectorPanel: {
        position: 'absolute',
        top: '0',
        left: '0',
        right: '0',
        zIndex: 1000,
        display: 'flex',
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: '16px',
        backgroundColor: tokens.colorNeutralBackground1,
        padding: '16px',
        width: '100%',
        boxSizing: 'border-box',
        alignItems: 'center',
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
    description: {
        marginBottom: '8px',
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
        backgroundColor: tokens.colorBrandBackground2,
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
        border: `1px solid ${tokens.colorBrandStroke2}`,
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
        height: '100%',
        overflowY: 'auto',
        flex: '0 0 auto',
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '16px',
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
