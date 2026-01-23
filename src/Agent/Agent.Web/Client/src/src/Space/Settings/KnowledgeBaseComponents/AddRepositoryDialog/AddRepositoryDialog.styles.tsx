import { makeStyles, tokens } from '@fluentui/react-components';

export const useAddRepositoryDialogStyles = makeStyles({
    dialogSurface: {
        maxWidth: '500px',
        minWidth: '400px',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    description: {
        color: tokens.colorNeutralForeground2,
        marginBottom: tokens.spacingVerticalS,
    },
    hintText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        marginTop: tokens.spacingVerticalXS,
    },
    dialogActions: {
        paddingTop: tokens.spacingVerticalL,
    },
    signInButton: {
        maxWidth: 'fit-content',
    },
    accountCard: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalM}`,
        boxShadow: 'none',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    accountInfo: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    accountText: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    connectedLabel: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
    },
    accountEmail: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    checkmark: {
        color: tokens.colorPaletteGreenBackground3,
    },
    signInLoading: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    differentAccountLink: {
        marginTop: tokens.spacingVerticalS,
    },
    gitHubIcon: {
        width: '24px',
        height: '24px',
    },
});
