import { makeStyles, tokens } from '@fluentui/react-components';

export const useMemorySidePanelStyles = makeStyles({
    root: {
        height: 'calc(100vh - 100px)',
        flex: '1 0 auto',
        borderTopLeftRadius: tokens.borderRadiusXLarge,
        borderBottomLeftRadius: tokens.borderRadiusXLarge,
        borderTopRightRadius: tokens.borderRadiusXLarge,
        borderBottomRightRadius: tokens.borderRadiusXLarge,
        position: 'relative',
    },
    header: {
        display: 'flex',
        flexWrap: 'nowrap',
        alignItems: 'center',
        justifyContent: 'space-between',
        minWidth: '0px',
        minHeight: '0px',
        gap: tokens.spacingHorizontalS,
    },
    headerText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flex: '1 1 auto',
    },
    headerButton: {
        flex: '0 1 auto',
    },
    resizer: {
        width: '2px',
        height: '100%',
        position: 'absolute',
        top: '0',
        left: '0',
        bottom: '0',
        cursor: 'col-resize',
        border: 'none',
        minWidth: '0px',

        '&:before': {
            width: '2px',
            content: '""',
            position: 'absolute',
            borderLeft: `1px solid ${tokens.colorNeutralBackground5}`,
            height: '100%',
        },
        ':hover': {
            cursor: 'col-resize',
        },
        ':hover:active': {
            cursor: 'col-resize',
            userSelect: 'none',
        },
    },
    content: {
        padding: '12px',
        height: '100%',
        width: '100%',
        boxSizing: 'border-box',
        overflowY: 'auto',
    },
});
