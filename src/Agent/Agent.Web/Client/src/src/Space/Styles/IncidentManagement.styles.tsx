import { mergeStyleSets } from '@fluentui/react';
import { makeStyles, tokens } from '@fluentui/react-components';

export const useIncidentManagementStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'flex-start',
        overflow: 'hidden',
        height: '100%',
        position: 'relative',
    },
    navPanelWrapper: {
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        position: 'relative',
        flex: 1,
        height: '100%',
    },
    navPanelContent: {
        display: 'flex',
        flexDirection: 'column',
        overflowY: 'auto',
        position: 'relative',
        flex: 1,
    },
    navPanelContentWithSidebar: {
        display: 'flex',
        flexDirection: 'row',
        overflow: 'hidden',
        position: 'relative',
        flex: 1,
    },
    mainFormContent: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        overflowY: 'auto',
    },
    navPanelPadding: {
        padding: '20px',
    },
    fullHeightFlexContainer: {
        width: '100%',
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        minWidth: '0',
        overflow: 'hidden',
    },
    breadCrumbAndPanelWrapper: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        width: '100%',
    },
    breadcrumb: {
        display: 'flex',
        height: '30px',
        marginTop: '10px',
        marginLeft: '16px',
    },
    incidentChatWrapper: {
        height: '100%',
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
        minWidth: '80px',
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
        paddingBottom: '8px',
    },
    incidentFiltersContainer: {
        display: 'flex',
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: '10px',
        marginBottom: '16px',
    },
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
        marginLeft: '20px',
        marginRight: '20px',
    },
    emptyStateTitle: { fontSize: '24px', fontWeight: 600, marginBottom: '8px' },
    emptyStateDescription: { color: tokens.colorNeutralForeground2, marginBottom: '16px' },
    newIncidentFilterButton: { width: 'fit-content', padding: '5px 10px' },
    greenCheckIcon: { color: tokens.colorPaletteGreenForeground1 },
    warningIcon: { color: tokens.colorStatusWarningForeground2 },
    spinnerIcon: { color: tokens.colorBrandForeground1 },
    setUp: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS },
    infoButton: { verticalAlign: 'middle', display: 'flex' },
    spinner: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        height: '100%',
        width: '100%',
    },
    inputField: {
        minWidth: '75px',
        maxWidth: '265px',
    },
    detailsListBase: {
        '& .ms-DetailsHeader': {
            paddingTop: '1px',
        },
    },
    detailsListDarkModeBackground: {
        '&, & *': {
            backgroundColor: `${tokens.colorNeutralBackground2} !important`,
        },
    },
    stepContent: {
        display: 'flex',
        flexDirection: 'column',
        padding: '20px 20px',
        gap: '32px',
        height: 'calc(100% - 114px)',
        overflowY: 'auto',
    },
    stepFooter: {
        display: 'flex',
        gap: '10px',
        padding: '20px',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    },
});

export const generateHandlerStyles = mergeStyleSets({
    dropdown: {
        maxWidth: '300px',
    },
    textField: {
        maxWidth: '600px',
    },
});
