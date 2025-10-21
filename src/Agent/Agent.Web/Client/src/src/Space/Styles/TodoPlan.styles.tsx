import { makeStyles, tokens } from '@fluentui/react-components';

export const useTodoItemStatusPillStyles = makeStyles({
    statusContainer: {
        padding: '2px 6px',
        borderRadius: '8px',
        display: 'flex',
        flexDirection: 'row',
        gap: '4px',
        alignItems: 'center',
        width: 'fit-content',
        flex: '0 0 auto',
        fontSize: '10px',
        fontWeight: '500',
        letterSpacing: '0.025em',
        textTransform: 'uppercase',
        transitionProperty: 'all',
        transitionDuration: '0.15s',
        transitionTimingFunction: 'ease',
    },
    icon: {
        fontSize: '10px',
        lineHeight: '1',
    },
});

export const useTodoPlanDrawerStyles = makeStyles({
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
        justifyContent: 'flex-start',
        minWidth: '0px',
        minHeight: '0px',
        gap: tokens.spacingHorizontalS,
    },
    headerIconContainer: {
        alignSelf: 'stretch',
    },
    headerTextContainer: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'flex-start',
        alignItems: 'flex-start',
        flex: '1 1 auto',
        minWidth: '0px',
    },
    headerText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        width: '100%',
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
    },
    emptyStateContainer: {
        position: 'fixed',
        top: '30%',
        left: '50%',
        transform: 'translate(-30%, -50%)',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: tokens.spacingVerticalM,
    },
    emptyStateIcon: {
        fontSize: '60px',
        minHeight: '60px',
        minWidth: '60px',
        opacity: '0.5',
    },
});

export const useTodoPlanContentStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        padding: '10px 15px',
        minHeight: '0px',
    },
    taskItem: {
        display: 'flex',
        flexDirection: 'row',
        minWidth: '0px',
        minHeight: '0px',
        gap: tokens.spacingHorizontalS,
        alignItems: 'flex-start',
        justifyItems: 'flex-start',
    },
    taskItemIcon: {
        fontSize: '18px',
        flex: '0 0 18px',
        paddingTop: '2px',
    },
    taskItemContent: {
        minWidth: '0px',
        flex: '0 0 auto',
    },
});
