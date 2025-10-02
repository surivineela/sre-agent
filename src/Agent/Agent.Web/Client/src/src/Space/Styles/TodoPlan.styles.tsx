import { makeStyles, tokens } from '@fluentui/react-components';

// ============================================================================
// TodoItemStatusPill Styles
// ============================================================================

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

// ============================================================================
// TodoPlanDrawer Styles
// ============================================================================

export const useTodoPlanDrawerStyles = makeStyles({
    root: {
        backgroundColor: tokens.colorNeutralBackground1,
        height: 'calc(100vh - 44px)',
        flex: '1 0 auto',
        borderTopLeftRadius: tokens.borderRadiusXLarge,
        borderBottomLeftRadius: tokens.borderRadiusXLarge,
        borderTopRightRadius: tokens.borderRadiusXLarge,
        borderBottomRightRadius: tokens.borderRadiusXLarge,
        position: 'relative',
    },
    header: {
        width: '100%',
        maxWidth: '100%',
        padding: '0',
        gap: tokens.spacingHorizontalS,
        alignSelf: 'stretch',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        boxSizing: 'border-box',
        position: 'relative',
        zIndex: '2',
        borderBottom: 'none',
        backgroundColor: tokens.colorNeutralBackground3,
    },
    titleContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
        flex: '1 1 0',
        minWidth: '0',
        maxWidth: '100%',
        padding: '12px 16px',
    },
    titleText: {
        fontSize: '13px',
        fontWeight: '600',
        color: tokens.colorNeutralForeground1,
        margin: '0',
        wordBreak: 'break-all',
        whiteSpace: 'normal',
        overflow: 'visible',
        textOverflow: 'clip',
        display: 'block',
        overflowWrap: 'anywhere',
        maxWidth: '100%',
        minWidth: '0',
        lineHeight: '1.3',
    },
    headerToolbar: {
        margin: '8px 8px 0 0',
        flexShrink: '0',
    },
    titleProgressRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '8px',
        flexWrap: 'nowrap',
    },
    titleProgressBar: {
        height: '8px',
        backgroundColor: tokens.colorNeutralBackground6,
        borderRadius: '2px',
        overflow: 'hidden',
        position: 'relative',
        flex: '1',
    },
    titleProgressFill: {
        height: '100%',
        background: tokens.colorBrandBackground,
        borderRadius: '3px',
        transitionProperty: 'width',
        transitionDuration: '0.6s',
        transitionTimingFunction: 'ease',
    },
    titleCount: {
        fontSize: '11px',
        color: tokens.colorNeutralForeground3,
        fontWeight: '400',
        flexShrink: '0',
        marginLeft: '8px',
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
        flex: '1 1 auto',
        overflow: 'auto',
        height: '100%',
        backgroundColor: tokens.colorNeutralBackground1,
    },
    emptyStateContainer: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: '12px',
        padding: '32px 16px',
        textAlign: 'center',
        color: tokens.colorNeutralForeground3,
    },
    emptyStateIcon: {
        fontSize: '32px',
        opacity: '0.5',
    },
    emptyStateText: {
        fontSize: '12px',
    },
});

// ============================================================================
// TodoPlanContentFixed Styles
// ============================================================================

export const useTodoPlanContentStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        padding: '0',
    },
    timeline: {
        position: 'relative',
        padding: '0 16px 0 40px',
    },
    timelineLine: {
        position: 'absolute',
        left: '21px',
        top: '8px',
        bottom: '13px',
        width: '3px',
        backgroundColor: tokens.colorNeutralStroke3,
    },
    taskItem: {
        position: 'relative',
        marginBottom: '20px',
        display: 'flex',
        alignItems: 'flex-start',
    },
    taskItemLast: {
        marginBottom: '0',
    },
    // Base task dot style
    taskDot: {
        position: 'absolute',
        left: '-26px',
        top: '2px',
        borderRadius: '50%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: '1',
        width: '14px',
        height: '14px',
    },
    taskDotCompleted: {
        backgroundColor: tokens.colorNeutralBackground1,
        border: `2px solid ${tokens.colorNeutralStroke2}`,
    },
    taskDotInProgress: {
        backgroundColor: tokens.colorBrandBackground2,
        border: `2px solid ${tokens.colorBrandBackground}`,
        animationName: 'pulse-blue',
        animationDuration: '2s',
        animationIterationCount: 'infinite',
    },
    taskDotFailed: {
        backgroundColor: tokens.colorNeutralBackground1,
        border: `2px solid ${tokens.colorNeutralStroke2}`,
    },
    taskDotPending: {
        backgroundColor: tokens.colorNeutralBackground1,
        border: `2px solid ${tokens.colorNeutralStroke2}`,
    },
    taskContent: {
        flex: '1',
        display: 'flex',
        flexDirection: 'column',
        gap: '3px',
        minWidth: '0',
    },
    taskText: {
        fontSize: '13px',
        fontWeight: '400',
        color: tokens.colorNeutralForeground1,
        lineHeight: '1.5',
        margin: '0',
    },
    taskTextCompleted: {
        color: tokens.colorNeutralForeground3,
    },
    taskMeta: {
        fontSize: '10px',
        color: tokens.colorNeutralForeground3,
        fontWeight: '400',
        opacity: '0.8',
    },
    taskMetaInline: {
        marginLeft: '6px',
    },
    completedIcon: {
        color: tokens.colorPaletteGreenForeground1,
        fontSize: '10px',
    },
    innerDotPending: {
        width: '4px',
        height: '4px',
        borderRadius: '50%',
        backgroundColor: tokens.colorNeutralStroke2,
    },
    innerDotFailed: {
        width: '4px',
        height: '4px',
        borderRadius: '50%',
        backgroundColor: tokens.colorPaletteRedForeground1,
    },
});

// ============================================================================
// CSS Animations
// ============================================================================

export const todoPlanAnimations = `
    @keyframes pulse-blue {
        0% {
            box-shadow: 0 0 0 0 rgba(96, 165, 250, 0.4);
        }
        70% {
            box-shadow: 0 0 0 4px rgba(96, 165, 250, 0);
        }
        100% {
            box-shadow: 0 0 0 0 rgba(96, 165, 250, 0);
        }
    }
`;
