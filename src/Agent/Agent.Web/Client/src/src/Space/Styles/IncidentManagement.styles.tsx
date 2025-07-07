import { mergeStyleSets } from '@fluentui/react';
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
        justifyContent: 'start',
        gap: '8px',
        padding: '20px',
        paddingLeft: '0px',
    },
    toolsToolbar: {
        display: 'flex',
        justifyContent: 'start',
        gap: '8px',
        padding: '0px',
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
        padding: 0,
        minWidth: '20px',
    },
    dangerButton: {
        backgroundColor: tokens.colorStatusDangerBackground3,
        color: `${tokens.colorNeutralForegroundInverted} !important`,
        ':hover': {
            backgroundColor: tokens.colorStatusDangerBackground3Hover,
        },
        ':active': {
            backgroundColor: tokens.colorStatusDangerBackground3Pressed,
        },
    },
    description: {
        paddingTop: '20px',
        paddingRight: '20px',
        paddingLeft: '0px',
        paddingBottom: '0px',
    },
    incidentFiltersContainer: { display: 'flex', flexDirection: 'row', gap: '5px' },
    searchBox: {
        width: '330px',
        fontSize: '13px',
        zIndex: 1,
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        height: '60vh',
        textAlign: 'center',
        gap: '24px',
    },
    emptyStateTitle: { fontSize: '24px', fontWeight: 600, marginBottom: '8px' },
    emptyStateDescription: { color: tokens.colorNeutralForeground2, marginBottom: '16px' },
    newIncidentFilterButton: { width: 'fit-content', padding: '5px 10px' },
    greenCheckIcon: { color: tokens.colorPaletteGreenForeground1 },
    setUp: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS },
    infoButton: { verticalAlign: 'middle', display: 'flex' },
});

export const generateHandlerStyles = mergeStyleSets({
    dropdown: {
        maxWidth: '300px',
    },
    textField: {
        maxWidth: '600px',
    },
});
