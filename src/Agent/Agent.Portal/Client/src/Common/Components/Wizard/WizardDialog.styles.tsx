import { makeStyles, tokens } from '@fluentui/react-components';

export const useWizardStyles = makeStyles({
    dialogSurface: {
        padding: '0px',
        maxWidth: '1080px',
        minHeight: '750px',
        height: '750px',
    },
    dialogBodyGrid: {
        gap: 0,
        display: 'grid',
        gridTemplateAreas: `"title title" "menu content" "menu actions"`,
        gridTemplateColumns: '1fr 3fr',
        gridTemplateRows: 'auto 1fr auto',
        justifyItems: 'stretch',
        height: '100%',
    },
    dialogTitle: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: tokens.spacingVerticalXXL,
        gridArea: 'title',
        borderBottom: `1px solid ${tokens.colorNeutralForegroundDisabled}`,
    },
    stepper: {
        gridArea: 'menu',
        borderRight: `1px solid ${tokens.colorNeutralForegroundDisabled}`,
        padding: tokens.spacingVerticalXXL,
    },
    dialogContent: {
        gridArea: 'content',
        padding: '20px',
    },
    dialogActions: {
        gridArea: 'actions',
        justifySelf: 'stretch',
        borderTop: `1px solid ${tokens.colorNeutralForegroundDisabled}`,
        padding: `${tokens.spacingVerticalXL} ${tokens.spacingHorizontalXXL}`,
    },
    defaultActionsContainer: {
        display: 'flex',
        justifyContent: 'space-between',
        width: '100%',
    },
    leftActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
    },
    rightActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
    },
});
