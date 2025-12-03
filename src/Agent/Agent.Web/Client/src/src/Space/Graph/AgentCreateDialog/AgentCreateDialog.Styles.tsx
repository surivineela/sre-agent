import { makeStyles, tokens } from '@fluentui/react-components';

export const useAgentCreateDialogStyles = makeStyles({
    dialogSurface: {
        maxWidth: '1200px',
        width: '80vw',
        maxHeight: '800px',
        height: '80vh',
        padding: '0px',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        gap: '0px',
    },
    dialogTitleWrapper: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '22px 24px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    dialogTitle: {
        marginRight: 'auto',
        width: '0%',
        flex: '1 1 auto',
        overflow: 'hidden',
        whiteSpace: 'nowrap',
        textOverflow: 'ellipsis',
        lineHeight: '28px',
        fontSize: '20px',
        fontWeight: 600,
    },
    dialogCloseButton: {
        marginLeft: 'auto',
    },
    dialogContentOuterWrapper: {
        position: 'relative',
        display: 'flex',
        flex: '1 1 auto',
        height: '0%',
        flexDirection: 'column',
        overflowY: 'hidden',
    },
    dialogContentInnerWrapper: {
        display: 'flex',
        flex: '1 1 auto',
        height: '0%',
        flexDirection: 'row',
        overflowY: 'hidden',
        '@media (width < 1000px)': {
            flexDirection: 'column',
            overflowY: 'auto',
        },
    },
    dialogContentWrapper: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        flex: '1 1 auto',
        overflowY: 'auto',
        padding: '20px',
        width: '50%',
        '@media (width < 1000px)': {
            overflowY: 'visible',
            width: 'unset',
        },
    },
    chatBoxWrapper: {
        paddingBottom: '0px',
        '@media (width < 1000px)': {
            height: '100%',
        },
    },
    formControl: {
        maxWidth: '700px',
    },
    toolsContentWrapper: {
        '@media (width < 1000px)': {
            height: 'calc(100% - 40px)',
        },
    },
    dialogContentVerticalDivider: {
        flex: 'none',
        '@media (width < 1000px)': {
            display: 'none',
        },
    },
    dialogContentHorizontalDivider: {
        flex: 'none',
        '@media (width >= 1000px)': {
            display: 'none',
        },
    },
    formSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
    },
    instructionsButtonsContainer: {
        display: 'flex',
        gap: '4px',
    },
    toolsPickerTitleWrapper: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    searchBox: {
        minWidth: '75px',
        maxWidth: '265px',
    },
    buttonsContainer: {
        display: 'flex',
        gap: '10px',
        padding: '16px 24px 16px 24px',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
        marginTop: 'auto',
        justifyContent: 'flex-end',
    },
    yamlContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        flex: '1 1 auto',
        height: '100%',
        width: '50%',
        '@media (width < 1000px)': {
            overflowY: 'visible',
            width: 'unset',
        },
    },
    yamlEditor: {
        flex: 1,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
    },
    tabListWrapper: {
        flex: '1 1 auto',
        transform: 'translateX(16px)',
    },
    tabList: {
        backgroundColor: tokens.colorNeutralBackground4,
        width: 'fit-content',
        padding: '2px',
        borderRadius: '6px',
    },
    tab: {
        padding: '4px 12px',
        backgroundColor: 'transparent',
        ':before': {
            display: 'none',
        },
        ':after': {
            display: 'none',
        },
    },
    currentTab: {
        backgroundColor: `${tokens.colorNeutralBackground1} !important`,
    },
    suggestionsContainer: {
        overflowY: 'auto',
        backgroundColor: tokens.colorNeutralBackground2,
        padding: '20px',
        borderRadius: '8px',
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
    },
    suggestionSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
    },
    suggestionList: {
        paddingLeft: '20px',
        margin: '0px',
    },
    suggestionListItem: {
        display: 'list-item',
    },
    suggestionText: {
        color: tokens.colorNeutralForeground3,
    },
    loadingOverlay: {
        position: 'absolute',
        inset: 0,
        background: tokens.colorNeutralBackgroundAlpha,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1000,
    },
    memoryToggleContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    memoryInfoIcon: {
        fontSize: '14px',
        color: tokens.colorBrandForeground1,
        cursor: 'help',
    },
});
