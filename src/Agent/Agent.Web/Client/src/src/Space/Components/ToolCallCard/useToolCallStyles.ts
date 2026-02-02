import { makeStyles, tokens } from '@fluentui/react-components';

/**
 * Shared styles for tool call card content areas.
 * Minimal design inspired by VS Code / Cursor.
 */
export const useToolCallStyles = makeStyles({
    // Container for expanded content - rounded card design
    expandedContainer: {
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: '8px',
        marginTop: '8px',
        marginLeft: '24px',
        overflow: 'hidden',
        backgroundColor: tokens.colorNeutralBackground1,
    },

    // Header bar - minimal, no background
    contentHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '4px 8px 4px 12px',
        gap: '8px',
    },

    contentHeaderLeft: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        flex: 1,
        minWidth: 0,
    },

    // Full command text in expanded view
    commandText: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
    },

    // Code/content area with monospace font
    codeContainer: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        lineHeight: '18px',
    },

    // Scrollable code container with max height
    codeContainerScrollable: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        lineHeight: '18px',
        maxHeight: '280px',
        overflowY: 'auto',
    },

    // Individual code line row
    codeLine: {
        display: 'flex',
        minHeight: '18px',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },

    // Line number column - minimal
    lineNumber: {
        minWidth: '40px',
        padding: '0 8px 0 0',
        textAlign: 'right',
        color: tokens.colorNeutralForeground4,
        userSelect: 'none',
        flexShrink: 0,
        fontSize: '11px',
    },

    // Line content column
    lineContent: {
        padding: '0 8px',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        flex: 1,
    },

    // Match highlight (for grep) - subtle yellow
    matchHighlight: {
        backgroundColor: 'rgba(255, 213, 0, 0.25)',
        borderRadius: '2px',
    },

    // Context line styling (dimmed)
    contextLine: {
        color: tokens.colorNeutralForeground4,
    },

    // Match line styling (prominent)
    matchLine: {
        color: tokens.colorNeutralForeground1,
    },

    // Terminal output area - minimal
    terminalOutput: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        padding: '8px 12px',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        maxHeight: '280px',
        overflow: 'auto',
        color: tokens.colorNeutralForeground2,
    },

    // Error text styling
    errorText: {
        color: tokens.colorPaletteRedForeground1,
    },

    // Success text styling
    successText: {
        color: tokens.colorPaletteGreenForeground1,
    },

    // File header row - minimal
    fileHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        padding: '4px 8px 4px 12px',
        cursor: 'pointer',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },

    fileIcon: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
        fontSize: '14px',
    },

    filePath: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
        flex: 1,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },

    // Badge for match count - subtle
    infoBadge: {
        marginLeft: 'auto',
        flexShrink: 0,
        fontSize: '11px',
        color: tokens.colorNeutralForeground4,
    },

    // Copy button positioning
    copyButtonInline: {
        marginLeft: '4px',
        opacity: 0.6,
        ':hover': {
            opacity: 1,
        },
    },

    // Truncation notice - minimal
    truncationNotice: {
        padding: '4px 12px',
        color: tokens.colorNeutralForeground4,
        fontSize: '11px',
        fontStyle: 'italic',
    },

    // Status badge styling
    statusBadge: {
        minWidth: '20px',
        height: '20px',
        fontSize: '11px',
    },
});
