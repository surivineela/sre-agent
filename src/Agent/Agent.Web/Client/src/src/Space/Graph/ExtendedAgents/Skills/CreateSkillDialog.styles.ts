import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

export const useSkillDialogStyles = makeStyles({
    dialogSurface: {
        maxWidth: '1400px',
        width: '95vw',
    },
    dialogSurfaceWithPanel: {
        maxWidth: '1800px',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        minHeight: '70vh',
    },
    dialogContentWrapper: {
        display: 'flex',
        flexDirection: 'row',
        ...shorthands.gap('16px'),
        flexGrow: 1,
    },
    leftColumn: {
        display: 'flex',
        flexDirection: 'column',
        flexBasis: '33%',
        minWidth: '300px',
        flexShrink: 0,
        ...shorthands.gap('16px'),
    },
    formContent: {
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.gap('16px'),
        flexShrink: 0,
    },
    fileBrowserWrapper: {
        display: 'flex',
        flexDirection: 'column',
        flexGrow: 1,
        minHeight: 0,
    },
    fileBrowserField: {
        display: 'flex',
        flexDirection: 'column',
        flexGrow: 1,
        minHeight: 0,
    },
    editorColumn: {
        display: 'flex',
        flexDirection: 'column',
        flexGrow: 1,
        minWidth: 0,
    },
    panelDivider: {
        ...shorthands.margin('0', '0px'),
    },
    toolsPanelWrapper: {
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.gap('16px'),
        width: '400px',
        minWidth: '400px',
        flexShrink: 0,
    },
    toolsPanelHeader: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    errorMessage: {
        color: tokens.colorPaletteRedForeground1,
        fontSize: '14px',
        marginBottom: '8px',
    },
    descriptionLabelRow: {
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap('8px'),
    },
    descriptionText: {
        color: tokens.colorNeutralForeground1,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
    },
    descriptionTextarea: {
        width: '100%',
    },
    toolsFieldContent: {
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap('8px'),
    },
    toolsLink: {
        minWidth: 'max-content',
    },
});
