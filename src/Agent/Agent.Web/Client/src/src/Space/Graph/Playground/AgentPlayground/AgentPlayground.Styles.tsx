import { makeStyles, tokens } from '@fluentui/react-components';

export const useAgentPlaygroundStyles = makeStyles({
    container: {
        position: 'relative',
        height: '0%',
        width: '100%',
        flex: '1 1 auto',
        display: 'flex',
    },
    leftPanel: {
        position: 'relative',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        width: '50%',
        maxWidth: '750px',
        flex: '1 1 auto',
    },
    rightPanel: {
        position: 'relative',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        width: '50%',
        flex: '1 1 auto',
    },
    titleWrapper: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '8px',
        borderBottom: 'none'
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
    },
    dialogContentWrapper: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        flex: '1 1 auto',
        overflowY: 'auto',
        padding: '20px',
    },
    chatBoxWrapper: {
        paddingBottom: '0px',
    },
    instructionsTextArea: {
        width: '100%',
    },
    formControl: {
        maxWidth: '700px',
    },
    dialogContentVerticalDivider: {
        flex: 'none',
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
    },
    yamlEditor: {
        flex: 1,
        overflow: 'hidden',
    },
    tabList: {
        backgroundColor: tokens.colorNeutralBackground4,
        width: 'fit-content',
        padding: '2px',
        borderRadius: '6px',
        marginLeft: 'auto',
        marginRight: 'auto',
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
        maxWidth: '660px',
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
