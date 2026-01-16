import { makeStyles, tokens } from '@fluentui/react-components';

/**
 * Shared styles for tool call card content areas.
 * Provides consistent styling for code blocks, line numbers, and content containers.
 */
export const useToolCallStyles = makeStyles({
    // Container for expanded content
    expandedContainer: {
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground1,
        overflow: 'hidden',
        marginTop: '4px',
    },

    // Header bar within expanded container
    contentHeader: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'space-between',
        padding: '8px 12px',
        backgroundColor: tokens.colorNeutralBackground3,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        gap: '8px',
    },

    contentHeaderLeft: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
        flex: 1,
        minWidth: 0,
    },

    // Full command text in expanded view
    commandText: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        color: tokens.colorNeutralForeground1,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
    },

    // Code/content area with monospace font
    codeContainer: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        lineHeight: '18px',
        backgroundColor: tokens.colorNeutralBackground2,
    },

    // Individual code line row
    codeLine: {
        display: 'flex',
        minHeight: '18px',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground2Hover,
        },
    },

    // Line number column
    lineNumber: {
        minWidth: '48px',
        padding: '0 8px',
        textAlign: 'right',
        color: tokens.colorNeutralForeground4,
        userSelect: 'none',
        borderRight: `1px solid ${tokens.colorNeutralStroke3}`,
        flexShrink: 0,
    },

    // Line content column
    lineContent: {
        padding: '0 12px',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        flex: 1,
    },

    // Match highlight (for grep)
    matchHighlight: {
        backgroundColor: '#fff3cd',
        color: tokens.colorNeutralForeground1,
        borderRadius: '2px',
        padding: '0 1px',
    },

    // Context line styling (dimmed)
    contextLine: {
        color: tokens.colorNeutralForeground4,
    },

    // Match line styling (prominent)
    matchLine: {
        color: tokens.colorNeutralForeground1,
    },

    // Terminal output area
    terminalOutput: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        backgroundColor: tokens.colorNeutralBackground2,
        padding: '12px',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        maxHeight: '300px',
        overflow: 'auto',
    },

    // Error text styling
    errorText: {
        color: tokens.colorPaletteRedForeground1,
    },

    // Success text styling
    successText: {
        color: tokens.colorPaletteGreenForeground1,
    },

    // File header row (collapsible file in grep results)
    fileHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        padding: '6px 12px',
        cursor: 'pointer',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground2Hover,
        },
    },

    fileIcon: {
        color: tokens.colorNeutralForeground3,
        flexShrink: 0,
    },

    filePath: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '13px',
        color: tokens.colorNeutralForeground1,
        flex: 1,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },

    // Badge for match count, exit code, etc.
    infoBadge: {
        marginLeft: 'auto',
        flexShrink: 0,
    },

    // Copy button positioning
    copyButtonInline: {
        marginLeft: '8px',
    },

    // Truncation notice
    truncationNotice: {
        padding: '8px 12px',
        backgroundColor: tokens.colorNeutralBackground3,
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
        color: tokens.colorNeutralForeground3,
        fontSize: '12px',
        fontStyle: 'italic',
    },

    // Status badge styling
    statusBadge: {
        minWidth: '24px',
        borderRadius: tokens.borderRadiusLarge,
        height: '24px',
    },
});
