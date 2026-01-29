import { makeStyles, tokens } from '@fluentui/react-components';

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
        gap: tokens.spacingVerticalXS,
    },
    headerText: {
        overflow: 'hidden',
        width: '100%',
    },
    headerButton: {
        flex: '0 1 auto',
    },
    emptyStateContainer: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: tokens.spacingVerticalM,
        height: '100%',
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
        padding: '10px 15px 10px 36px', // Left padding aligns items with header text (icon 28px + gap 8px)
        minHeight: '0px',
    },
    taskItem: {
        display: 'flex',
        flexDirection: 'row',
        minWidth: '0px',
        minHeight: '0px',
        alignItems: 'flex-start',
        justifyItems: 'flex-start',
    },
    taskItemIcon: {
        fontSize: '18px',
        width: '18px',
        marginLeft: '-26px', // Pull icon into left margin to align with header icon
        marginRight: '8px', // Space between icon and text
        paddingTop: '2px',
        flexShrink: 0,
    },
    taskItemContent: {
        minWidth: '0px',
        flex: '0 0 auto',
    },
});
