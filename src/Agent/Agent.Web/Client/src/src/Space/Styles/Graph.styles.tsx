import { makeStyles, tokens } from "@fluentui/react-components";

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
        width: 'calc(100% - 360px)',
        height: '100%',
        flex: '0 0 auto'
    },
    spinner: {
        position: 'fixed',
        left: '50%',
        top: '50%'
    }
});

export const useGraphNodeStyles = makeStyles({
    handle: {
        opacity: 0,
        pointerEvents: 'none'
    },
    card: {
        width: '200px',
        height: '170px',
        position: 'relative',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    cardHightlight: {
        backgroundColor: tokens.colorBrandBackground2Hover
    },
    header: {
        width: 'calc(200px - 24px)'
    },
    headerText: {
        textOverflow: 'ellipsis',
        overflow: 'hidden',
        width: 'calc(200px - 24px - 52px)'
    },
    description: {
        color: tokens.colorNeutralForeground3
    },
    footer: {
        position: 'absolute',
        bottom: '5px'
    }
});

export const useGraphEdgeStyles = makeStyles({
    hightlightedEdge: {
        stroke: tokens.colorBrandForegroundLinkHover
    }
});

export const useResourceSelectorStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
        alignItems: 'flex-start',
        width: '300px',
        height: '100%',
        flex: '0 0 auto',
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '35px 30px'
    },
    option: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px'
    },
    optionText: {
        wordBreak: 'break-word'
    },
    optionSubtext: {
        color: tokens.colorNeutralForeground3,
        wordBreak: 'break-word'
    }
});
