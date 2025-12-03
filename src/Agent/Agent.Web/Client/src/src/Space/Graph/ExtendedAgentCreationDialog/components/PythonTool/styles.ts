import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

// ─────────────────────────────────────────────────────────────────────────────
// Terminal Colors (VS Code Dark+ Theme)
// ─────────────────────────────────────────────────────────────────────────────

export const terminalColors = {
    bg: '#1E1E1E',
    bgLight: '#252526',
    bgLighter: '#2D2D2D',
    border: '#3E3E3E',
    text: '#D4D4D4',
    textMuted: '#808080',
    textDim: '#6A9955',
    accent: '#007ACC',
    accentHover: '#1177BB',
    success: '#4EC9B0',
    successBg: '#1D3D2D',
    error: '#F48771',
    errorBg: '#3D1D1D',
    warning: '#DCDCAA',
    keyword: '#569CD6',
    string: '#CE9178',
    number: '#B5CEA8',
    param: '#9CDCFE',
};

// ─────────────────────────────────────────────────────────────────────────────
// Prompt View Styles
// ─────────────────────────────────────────────────────────────────────────────

export const usePromptViewStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        ...shorthands.padding(tokens.spacingVerticalXL, tokens.spacingHorizontalXL),
        backgroundColor: tokens.colorNeutralBackground1,
    },
    header: {
        textAlign: 'center',
        marginBottom: tokens.spacingVerticalXL,
    },
    iconContainer: {
        width: '56px',
        height: '56px',
        ...shorthands.borderRadius('50%'),
        background: `linear-gradient(135deg, ${tokens.colorBrandBackground} 0%, ${tokens.colorBrandBackgroundPressed} 100%)`,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        ...shorthands.margin('0', 'auto', tokens.spacingVerticalM),
        boxShadow: tokens.shadow8,
    },
    promptArea: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.gap(tokens.spacingVerticalM),
    },
    textarea: {
        flex: 1,
        minHeight: '180px',
        fontSize: '15px',
        lineHeight: '1.7',
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
    },
    examples: {
        display: 'flex',
        ...shorthands.gap(tokens.spacingHorizontalS),
        flexWrap: 'wrap',
        alignItems: 'center',
    },
    exampleBadge: {
        cursor: 'pointer',
        transitionProperty: 'all',
        transitionDuration: '0.15s',
        ':hover': {
            backgroundColor: tokens.colorBrandBackground2,
            ...shorthands.borderColor(tokens.colorBrandStroke1),
        },
    },
    actions: {
        display: 'flex',
        ...shorthands.gap(tokens.spacingHorizontalM),
        justifyContent: 'center',
        paddingTop: tokens.spacingVerticalL,
    },
    contextBanner: {
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        marginBottom: tokens.spacingVerticalM,
    },
});

// ─────────────────────────────────────────────────────────────────────────────
// Code View Styles
// ─────────────────────────────────────────────────────────────────────────────

export const useCodeViewStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        ...shorthands.overflow('hidden'),
    },
    toolbar: {
        display: 'flex',
        ...shorthands.gap(tokens.spacingHorizontalM),
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalM),
        ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke2),
        alignItems: 'center',
        backgroundColor: tokens.colorNeutralBackground1,
        flexShrink: 0,
    },
    toolbarField: {
        flex: 1,
    },
    toolbarFieldSmall: {
        width: '100px',
    },
    descriptionBar: {
        ...shorthands.padding(tokens.spacingVerticalXS, tokens.spacingHorizontalM),
        ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke3),
        backgroundColor: tokens.colorNeutralBackground2,
    },
    editorContainer: {
        flex: 1,
        minHeight: 0,
        ...shorthands.overflow('hidden'),
    },
});

// ─────────────────────────────────────────────────────────────────────────────
// Terminal Styles
// ─────────────────────────────────────────────────────────────────────────────

export const useTerminalStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        backgroundColor: terminalColors.bg,
        fontFamily: 'Consolas, Monaco, "Courier New", monospace',
        fontSize: '13px',
        lineHeight: '1.6',
        color: terminalColors.text,
        ...shorthands.overflow('hidden'),
    },
    header: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalM),
        backgroundColor: terminalColors.bgLight,
        ...shorthands.borderBottom('1px', 'solid', terminalColors.border),
    },
    headerTitle: {
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap(tokens.spacingHorizontalS),
    },
    headerDot: {
        width: '8px',
        height: '8px',
        ...shorthands.borderRadius('50%'),
        backgroundColor: terminalColors.success,
    },
    content: {
        flex: 1,
        ...shorthands.overflow('auto'),
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalM),
    },
    section: {
        marginBottom: tokens.spacingVerticalL,
    },
    sectionLabel: {
        color: terminalColors.success,
        fontWeight: 600,
        marginBottom: tokens.spacingVerticalS,
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap(tokens.spacingHorizontalXS),
    },
    paramRow: {
        marginBottom: tokens.spacingVerticalM,
    },
    paramLabel: {
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap(tokens.spacingHorizontalXS),
        marginBottom: '4px',
        fontSize: '12px',
    },
    paramName: {
        color: terminalColors.param,
    },
    paramType: {
        color: terminalColors.success,
    },
    paramRequired: {
        color: terminalColors.error,
        fontSize: '11px',
    },
    paramOptional: {
        color: terminalColors.textMuted,
        fontSize: '11px',
    },
    input: {
        width: '100%',
        backgroundColor: terminalColors.bgLighter,
        ...shorthands.border('1px', 'solid', terminalColors.border),
        ...shorthands.borderRadius('4px'),
        ...shorthands.padding('8px', '10px'),
        color: terminalColors.text,
        fontSize: '13px',
        fontFamily: 'inherit',
        ':focus': {
            ...shorthands.borderColor(terminalColors.accent),
            ...shorthands.outline('none'),
        },
        '::placeholder': {
            color: terminalColors.textMuted,
        },
    },
    missingWarning: {
        color: terminalColors.error,
        fontSize: '12px',
        marginTop: tokens.spacingVerticalS,
        fontWeight: 500,
    },
    commandLine: {
        color: terminalColors.success,
        marginBottom: tokens.spacingVerticalS,
        wordBreak: 'break-all',
    },
    resultCard: {
        ...shorthands.borderRadius('6px'),
        ...shorthands.padding(tokens.spacingVerticalM),
        marginTop: tokens.spacingVerticalM,
    },
    successCard: {
        backgroundColor: terminalColors.successBg,
        ...shorthands.border('1px', 'solid', terminalColors.success),
    },
    errorCard: {
        backgroundColor: terminalColors.errorBg,
        ...shorthands.border('1px', 'solid', terminalColors.error),
    },
    resultHeader: {
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap(tokens.spacingHorizontalS),
        marginBottom: tokens.spacingVerticalS,
    },
    resultPre: {
        backgroundColor: 'rgba(0, 0, 0, 0.3)',
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalS),
        ...shorthands.borderRadius('4px'),
        ...shorthands.margin(0),
        fontSize: '12px',
        ...shorthands.overflow('auto'),
        maxHeight: '200px',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
    },
    refinements: {
        marginTop: tokens.spacingVerticalM,
        paddingTop: tokens.spacingVerticalM,
        ...shorthands.borderTop('1px', 'solid', 'rgba(255,255,255,0.1)'),
    },
    refinementChips: {
        display: 'flex',
        flexWrap: 'wrap',
        ...shorthands.gap(tokens.spacingHorizontalXS),
        marginTop: tokens.spacingVerticalXS,
    },
    chip: {
        ...shorthands.padding('4px', '10px'),
        ...shorthands.borderRadius('12px'),
        backgroundColor: terminalColors.bgLighter,
        ...shorthands.border('1px', 'solid', terminalColors.border),
        color: terminalColors.text,
        fontSize: '11px',
        cursor: 'pointer',
        transitionProperty: 'all',
        transitionDuration: '0.15s',
        ':hover': {
            backgroundColor: terminalColors.accent,
            ...shorthands.borderColor(terminalColors.accent),
        },
    },
    fixButton: {
        width: '100%',
        marginTop: tokens.spacingVerticalM,
        backgroundColor: terminalColors.accent,
        ...shorthands.border('none'),
        ...shorthands.borderRadius('6px'),
        ...shorthands.padding(tokens.spacingVerticalS),
        color: 'white',
        fontWeight: 600,
        cursor: 'pointer',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        ...shorthands.gap(tokens.spacingHorizontalS),
        ':hover': {
            backgroundColor: terminalColors.accentHover,
        },
    },
    emptyState: {
        textAlign: 'center',
        ...shorthands.padding(tokens.spacingVerticalXXL),
        color: terminalColors.textMuted,
    },
    emptyIcon: {
        fontSize: '40px',
        marginBottom: tokens.spacingVerticalM,
        color: terminalColors.textMuted,
    },
    kbd: {
        ...shorthands.padding('2px', '8px'),
        ...shorthands.borderRadius('4px'),
        backgroundColor: terminalColors.bgLighter,
        ...shorthands.border('1px', 'solid', terminalColors.border),
        fontSize: '11px',
        fontFamily: 'inherit',
    },
});

// ─────────────────────────────────────────────────────────────────────────────
// Main Layout Styles
// ─────────────────────────────────────────────────────────────────────────────

export const usePythonToolStyles = makeStyles({
    splitContainer: {
        display: 'flex',
        height: '100%',
        ...shorthands.overflow('hidden'),
    },
    leftPanel: {
        flex: '1 1 55%',
        display: 'flex',
        flexDirection: 'column',
        minWidth: '400px',
        ...shorthands.borderRight('1px', 'solid', tokens.colorNeutralStroke2),
    },
    rightPanel: {
        flex: '0 0 45%',
        minWidth: '450px',
        maxWidth: '550px',
        ...shorthands.overflow('hidden'),
    },
});
