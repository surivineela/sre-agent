import { mergeStyleSets } from '@fluentui/react';
import { makeStyles, tokens } from '@fluentui/react-components';

export const navStyles = {
    root: {
        width: 200,
        marginLeft: 20,
        marginTop: 20,
    },
    compositeLink: {
        backgroundColor: 'transparent',
        selectors: {
            '&.is-selected': {
                backgroundColor: tokens.colorNeutralBackground3Selected,
            },
            '&:hover': {
                backgroundColor: tokens.colorNeutralBackground3Hover,
            },
        },
        height: 32,
        borderRadius: 4,
    },
    link: {
        paddingLeft: 5,
        backgroundColor: 'transparent !important',
        selectors: {
            '&:after': {
                inset: '5px 0px',
                width: '0px',
                borderWidth: `3px`,
                borderRadius: tokens.borderRadiusCircular,
            },
        },
        height: 32,
    },
};

export const useIncidentManagementStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'flex-start',
        overflow: 'hidden',
        borderTop: '1px solid rgba(204,204,204,.8)',
        backgroundColor: tokens.colorNeutralBackground3,
        height: 'calc(100vh - 46px)',
        width: '100vw',
        position: 'relative',
    },
    navPanelWrapper: {
        display: 'flex',
        flexDirection: 'column',
        margin: '16px 20px 5px 20px',
        borderRadius: tokens.borderRadiusXLarge,
        boxShadow: tokens.shadow4,
        backgroundColor: tokens.colorNeutralBackground1,
        height: 'calc(100% - 21px)',
        overflow: 'hidden',
        position: 'relative',
        flex: 1,
    },
    navPanelContent: {
        display: 'flex',
        flexDirection: 'column',
        overflowY: 'auto',
        position: 'relative',
        flex: 1,
    },
    navPanelPadding: {
        padding: '16px',
        height: 'calc(100% - 32px)',
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
        // paddingTop: '16px',
        // height: 'calc(100% - 16px)',
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
        paddingBottom: '8px',
    },
    incidentFiltersContainer: {
        display: 'flex',
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: '5px',
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
});

export const generateHandlerStyles = mergeStyleSets({
    dropdown: {
        maxWidth: '300px',
    },
    textField: {
        maxWidth: '600px',
    },
});
