import { makeStyles, tokens } from '@fluentui/react-components';

export const useConnectorEditDialogStyles = makeStyles({
    dialogSurface: {
        maxWidth: '600px',
        minWidth: '500px',
        minHeight: '400px',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
    },
    dialogTitle: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        paddingBottom: tokens.spacingVerticalM,
    },
    titleContent: {
        display: 'flex',
        alignItems: 'flex-start',
        gap: tokens.spacingHorizontalM,
    },
    titleIcon: {
        width: '40px',
        height: '40px',
        flexShrink: 0,
    },
    titleTextContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    titleText: {
        fontSize: tokens.fontSizeBase500,
        fontWeight: tokens.fontWeightSemibold,
        lineHeight: tokens.lineHeightBase500,
    },
    subtitleText: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground2,
        lineHeight: tokens.lineHeightBase300,
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        overflowY: 'auto',
        flex: 1,
    },
    dialogActions: {
        paddingTop: tokens.spacingVerticalL,
    },
    actionsContainer: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        justifyContent: 'flex-start',
        width: '100%',
    },
});
