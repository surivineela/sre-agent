import { makeStyles, tokens } from '@fluentui/react-components';

export const usePythonToolDialogStyles = makeStyles({
    // Dialog container styles
    dialogSurface: {
        minWidth: 'fit-content',
        maxWidth: '1400px',
        width: '85vw',
        maxHeight: '85vh',
        height: '85vh',
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
        padding: '24px 24px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    dialogTitle: {
        margin: '0px',
    },
    dialogActions: {
        display: 'flex',
        justifyContent: 'flex-end',
        padding: tokens.spacingHorizontalL,
        borderTop: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    dialogContent: {
        display: 'flex',
        flex: 1,
        minHeight: 0,
        overflow: 'hidden',
    },

    // Main content layout
    toolForm: {
        display: 'flex',
        gap: '24px',
        padding: tokens.spacingVerticalXL,
        overflowX: 'auto',
        flex: 1,
        minHeight: 0,
    },
    toolFormLeft: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: tokens.spacingVerticalL,
        flex: '1 1 50%',
        alignSelf: 'stretch',
        minWidth: '400px',
        overflow: 'hidden',
    },
    toolFormDivider: {
        backgroundColor: tokens.colorNeutralStroke2,
        width: '1px',
        alignSelf: 'stretch',
        padding: '0px',
        flexShrink: 0,
    },
    toolFormRight: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        minWidth: '500px',
        flex: '1 1 50%',
        overflow: 'hidden',
    },

    // Form fields
    headerRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        alignItems: 'flex-start',
        flexShrink: 0,
    },
    nameField: {
        flex: 1,
    },
    descriptionField: {
        display: 'flex',
        flexDirection: 'column',
        flexShrink: 0,
    },

    // Editor
    editorContainer: {
        flex: 1,
        minHeight: '200px',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        overflow: 'hidden',
    },

    // Right panel tabs
    tabContent: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        paddingTop: tokens.spacingVerticalM,
        minHeight: 0,
        overflow: 'auto',
        width: '100%',
    },

    // Assistant tab
    promptArea: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    promptTextarea: {
        minHeight: '120px',
    },

    // Test panel
    testPanelHeader: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: '12px',
        width: '100%',
    },
    testPanelMessageBar: {
        width: '100%',
        maxWidth: '600px',
        textWrap: 'wrap',
    },
    errorContainer: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: tokens.spacingVerticalS,
        width: '100%',
        maxWidth: '600px',
    },
    parameterSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        width: '100%',
    },
    parameterList: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    paramWarning: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorPaletteRedForeground1,
        marginTop: tokens.spacingVerticalXXS,
    },

    // Results section
    executionResultsSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalM,
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        maxWidth: '600px',
    },
    resultsSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        marginTop: tokens.spacingVerticalM,
        flexShrink: 0,
    },
    resultsHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    resultsContent: {
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        padding: tokens.spacingVerticalM,
        fontFamily: 'Consolas, Monaco, "Courier New", monospace',
        fontSize: tokens.fontSizeBase200,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        maxHeight: '200px',
        overflow: 'auto',
    },

    // Empty state
    emptyContent: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: '20px',
        width: '100%',
        maxHeight: '364px',
        flexGrow: 1,
    },

    // Assistant panel (left side, standalone)
    assistantPanel: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        flex: '1 1 50%',
        minWidth: '350px',
        overflow: 'hidden',
    },
    assistantHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        paddingBottom: tokens.spacingVerticalS,
        borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    assistantContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        flex: 1,
        overflow: 'auto',
        paddingTop: '12px', // Align with content below tabs on right panel
    },

    // Code and Test panel (right side, tabbed)
    codeAndTestPanel: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        flex: '1 1 50%',
        minWidth: '400px',
        minHeight: 0,
        overflow: 'hidden',
    },
    codeTabContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        flex: 1,
        minHeight: 0,
        overflow: 'auto',
        paddingBottom: tokens.spacingVerticalM,
    },
});
