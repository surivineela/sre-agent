import { makeStyles, tokens } from '@fluentui/react-components';

export const useIncidentTriggerCreateDialogStyles = makeStyles({
    dialogSurface: {
        maxWidth: '1200px',
        width: '80vw',
        maxHeight: 'unset',
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
        padding: '24px 24px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    dialogTitle: {
        margin: '0px',
    },
});
