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
        height: '100%',
    },
    navPanelContentWithSidebar: {
        display: 'flex',
        flexDirection: 'row',
        overflow: 'hidden',
        position: 'relative',
        flex: 1,
        height: '100%',
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
        marginTop: '20px',
        marginLeft: '16px',
        marginBottom: '20px',
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
        maxWidth: '600px',
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
    filterStepContentSection: { display: 'flex', flexDirection: 'column', gap: '16px', maxWidth: '644px' },
    stepContentSection: { display: 'flex', flexDirection: 'column', gap: '16px' },
    stepFooter: {
        display: 'flex',
        gap: '10px',
        padding: '20px',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    // Review and Test Content styles
    reviewAndTestRoot: {
        display: 'flex',
        flexDirection: 'row',
        gap: tokens.spacingHorizontalL,
        height: '100%',
        width: 'calc(100% - 16px)',
    },
    reviewAndTestOverlay: {
        position: 'absolute' as const,
        inset: '0',
        backgroundColor: 'rgba(255, 255, 255, 0.6)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1000,
    },
    reviewPanelLeft: {
        display: 'flex',
        flexDirection: 'column',
        paddingTop: tokens.spacingVerticalXL,
        height: 'calc(100% - 20px)',
    },
    reviewPanelLeftHalf: {
        width: '50%',
    },
    reviewPanelLeftFull: {
        width: '100%',
    },
    reviewSectionHeader: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        minHeight: '33%',
        flex: 'none',
    },
    reviewSectionTitle: {
        margin: '0',
    },
    reviewToolsSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        height: '0%',
        flex: '1 1 auto',
    },
    reviewToolsTitle: {
        marginTop: tokens.spacingVerticalXXXL,
        marginBottom: '0',
    },
    formDivider: {
        backgroundColor: tokens.colorNeutralStroke1,
        width: '1px',
        alignSelf: 'stretch',
        padding: '0px',
    },
    testPanelRight: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        paddingTop: tokens.spacingVerticalXL,
        height: 'calc(100% - 20px)',
    },
    testPanelRightHalf: {
        width: '50%',
    },
    testPanelRightFull: {
        width: '100%',
    },
    testIncidentInputRow: {
        display: 'flex',
        flexDirection: 'row',
        gap: tokens.spacingHorizontalS,
        alignItems: 'end',
        position: 'relative' as const,
    },
    testIncidentField: {
        flexBasis: '500px',
    },
    testIncidentDropdownContent: {
        maxHeight: '400px',
        overflowY: 'auto',
        overflowX: 'auto',
    },
    testIncidentNoResults: {
        margin: '2px 0px',
        paddingLeft: tokens.spacingHorizontalS,
    },
    testIncidentSpinner: {
        height: '100%',
    },
    testEmptyStateIcon: {
        height: '100px',
        width: '100px',
    },
});

export const generateHandlerStyles = mergeStyleSets({
    dropdown: {
        maxWidth: '300px',
    },
    textField: {
        maxWidth: '600px',
    },
    textArea: {
        maxWidth: '800px',
    },
});
