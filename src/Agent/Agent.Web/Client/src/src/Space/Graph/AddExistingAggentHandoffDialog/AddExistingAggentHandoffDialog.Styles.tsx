import { makeStyles } from '@fluentui/react-components';

export const useAddAggentHandoffDialogStyles = makeStyles({
    dialogSurface: {
        maxWidth: '508px',
        width: '80vw',
        maxHeight: '360px',
        height: '80vh',
        padding: '0px',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        gap: '0px',
        padding: '24px 24px',
    },
    dialogTitleWrapper: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingBottom: '24px',
    },
    dialogTitle: {
        margin: '0px',
    },
    dialogContentWrapper: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
    },
    emptyOptionsMessage: {
        margin: '2px 0px',
        paddingLeft: '10px',
    },
    optionsWrapper: {
        maxHeight: '135px',
        overflowY: 'auto',
        overflowX: 'auto',
    },
    option: {
        margin: '2px',
    },
    optionContent: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    buttonsContainer: {
        display: 'flex',
        gap: '10px',
        marginTop: 'auto',
        justifyContent: 'flex-end',
    },
});
