import { makeStyles } from '@fluentui/react-components';

export const useAddExistingToolDialogStyles = makeStyles({
    dialogSurface: {
        minWidth: '480px',
        maxWidth: '1000px',
        width: '80vw',
        maxHeight: '570px',
        height: '80vh',
        padding: '0px',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        height: 'calc(100% - 48px)',
        gap: '12px',
        margin: '24px',
    },
    dialogTitleWrapper: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    dialogTitle: {
        margin: '0px',
    },
    dialogContentWrapper: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        flex: '1 1 auto',
        overflowY: 'hidden',
    },
    searchBox: {
        minWidth: '75px',
        maxWidth: '265px',
    },
    buttonsContainer: {
        display: 'flex',
        gap: '10px',
        paddingTop: '24px',
        marginTop: 'auto',
        justifyContent: 'flex-end',
    },
});
