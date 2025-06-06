import { makeStyles, tokens } from '@fluentui/react-components';

export const useIncidentManagementStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        justifyContent: 'flex-start',
        alignItems: 'flex-start',
        overflow: 'hidden',
        borderTop: '1px solid rgba(204,204,204,.8)',
        backgroundColor: tokens.colorNeutralBackground3,
        height: 'calc(100vh - 44px)',
        width: '100vw',
    },
    container: {
        margin: '16px',
        padding: '20px',
        marginBottom: '0',
        display: 'flex',
        flexDirection: 'column',
        height: 'calc(100vh - 25px)',
        borderRadius: tokens.borderRadiusLarge,
        boxShadow: tokens.shadow4,
        backgroundColor: tokens.colorNeutralBackground1,
        width: 'calc(100vw - 32px)',
        flex: 1,
    },
    tabRoot: {
        display: 'flex',
        flexDirection: 'column',
        gap: '10px',
    },
    toolbar: {
        display: 'flex',
        justifyContent: 'space-between',
        gap: '4px',
        paddingBottom: '10px',
    },
    filters: {
        display: 'flex',
        gap: '16px',
        alignItems: 'center',
    },
    input: {
        width: '250px',
    },
    dropdown: {
        width: '200px',
    },
    divider: {
        width: '1px',
        height: '24px',
        backgroundColor: tokens.colorNeutralStroke2,
        alignSelf: 'center',
    },
    button: {
        fontWeight: 400,
    },
});
