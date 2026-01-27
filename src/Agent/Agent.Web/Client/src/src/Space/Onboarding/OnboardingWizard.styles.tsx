import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

export const useOnboardingWizardStyles = makeStyles({
    fullPageContainer: {
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: tokens.colorNeutralBackground1,
        zIndex: 1000,
        overflow: 'auto',
    },
    header: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        paddingTop: '2px',
        paddingBottom: '8px',
        backgroundColor: tokens.colorNeutralBackground1,
    },
    rocketIcon: {
        width: '100px',
        height: '100px',
        marginBottom: tokens.spacingVerticalS,
    },
    welcomeTitle: {
        fontSize: tokens.fontSizeHero700,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        marginBottom: tokens.spacingVerticalS,
    },
    welcomeSubtitle: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground2,
    },
    cardContainer: {
        display: 'flex',
        flex: 1,
        justifyContent: 'center',
        paddingLeft: tokens.spacingHorizontalXXXL,
        paddingRight: tokens.spacingHorizontalXXXL,
        paddingBottom: tokens.spacingVerticalXXXL,
        marginTop: tokens.spacingVerticalS,
    },
    wizardCard: {
        display: 'flex',
        flexDirection: 'column',
        maxWidth: '1100px',
        width: '100%',
        minHeight: '500px',
        maxHeight: '580px',
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: '16px',
        boxShadow: tokens.shadow28,
        overflow: 'hidden',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    contentContainer: {
        display: 'flex',
        flex: 1,
        overflow: 'hidden',
    },
    stepperPanel: {
        width: '260px',
        minWidth: '260px',
        padding: `${tokens.spacingVerticalXXL} ${tokens.spacingHorizontalXL}`,
        borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground2,
        overflowY: 'auto',
    },
    mainContent: {
        flex: 1,
        padding: `${tokens.spacingVerticalXXL} ${tokens.spacingHorizontalXXXL}`,
        overflowY: 'auto',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    stepVisible: {
        display: 'contents',
    },
    stepHidden: {
        display: 'none',
    },
    footer: {
        display: 'flex',
        justifyContent: 'flex-end',
        gap: tokens.spacingHorizontalM,
        padding: `${tokens.spacingVerticalL} ${tokens.spacingHorizontalXXL}`,
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    footerSpacer: {
        flex: 1,
    },
});

export const useInfrastructureScopeStepStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    headerSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    headerTitle: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    headerDescription: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
    },
    addButtonsContainer: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
    },
    scopeTypeContainer: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
    },
    recommendedText: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
        marginBottom: tokens.spacingVerticalM,
    },
    formField: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: tokens.spacingHorizontalL,
    },
    formFieldLabel: {
        width: '150px',
        minWidth: '150px',
        flexShrink: 0,
    },
    formFieldDropdown: {
        flex: 1,
        maxWidth: '400px',
    },
    skeletonDropdown: {
        height: '32px',
        maxWidth: '400px',
    },
    detailsSection: {
        marginTop: tokens.spacingVerticalM,
    },
    detailsTitle: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        marginBottom: tokens.spacingVerticalM,
    },
    detailsGrid: {
        display: 'grid',
        gridTemplateColumns: 'auto 1fr',
        gap: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalL}`,
    },
    detailsLabel: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
    },
    detailsValue: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground1,
    },
    selectedValueContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
});

export const useIncidentPlatformStepStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    description: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground2,
    },
    platformGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))',
        gap: tokens.spacingHorizontalM,
    },
    platformCard: {
        padding: tokens.spacingVerticalM,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusLarge,
        cursor: 'pointer',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalS,
        transitionProperty: 'all',
        transitionDuration: '0.2s',
        transitionTimingFunction: 'ease',
        backgroundColor: tokens.colorNeutralBackground1,
        '&:hover': {
            borderTopColor: tokens.colorBrandStroke1,
            borderRightColor: tokens.colorBrandStroke1,
            borderBottomColor: tokens.colorBrandStroke1,
            borderLeftColor: tokens.colorBrandStroke1,
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    platformCardSelected: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
        backgroundColor: tokens.colorBrandBackground2,
    },
    platformIcon: {
        width: '40px',
        height: '40px',
        color: tokens.colorNeutralForeground3,
    },
    platformImage: {
        width: '40px',
        height: '40px',
        objectFit: 'contain',
    },
    platformName: {
        fontSize: tokens.fontSizeBase200,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        textAlign: 'center',
    },
    configForm: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        marginTop: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusLarge,
    },
    formField: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
});

export const useRepositoriesStepStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        alignItems: 'center',
        justifyContent: 'center',
        flex: 1,
        textAlign: 'center',
    },
    icon: {
        fontSize: '48px',
        color: tokens.colorNeutralForeground3,
    },
    title: {
        fontSize: tokens.fontSizeBase500,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    description: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground2,
        maxWidth: '500px',
    },
    comingSoonMessage: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground3,
        maxWidth: '500px',
        fontStyle: 'italic',
    },
});

export const useKnowledgeBaseStepStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    headerSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    description: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
    },
    addButtonsContainer: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        flexWrap: 'wrap',
    },
    nameCell: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
});

export const usePermissionsStepStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    description: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground2,
    },
    optionalNote: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        fontStyle: 'italic',
    },
    // Permission level selection
    permissionLevelContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    permissionLevelTitle: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    permissionLevelOptions: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    permissionLevelCard: {
        padding: tokens.spacingVerticalM,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusLarge,
        cursor: 'pointer',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        transitionProperty: 'all',
        transitionDuration: '0.2s',
        transitionTimingFunction: 'ease',
        backgroundColor: tokens.colorNeutralBackground1,
        '&:hover': {
            borderTopColor: tokens.colorBrandStroke1,
            borderRightColor: tokens.colorBrandStroke1,
            borderBottomColor: tokens.colorBrandStroke1,
            borderLeftColor: tokens.colorBrandStroke1,
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    permissionLevelCardSelected: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
        backgroundColor: tokens.colorBrandBackground2,
    },
    permissionLevelCardTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    permissionLevelCardDescription: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
        marginLeft: tokens.spacingHorizontalXL,
    },
    radioIcon: {
        width: '16px',
        height: '16px',
        borderRadius: '50%',
        border: `2px solid ${tokens.colorNeutralStroke1}`,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
    },
    radioIconSelected: {
        ...shorthands.borderColor(tokens.colorBrandForeground1),
        backgroundColor: tokens.colorBrandForeground1,
    },
    radioIconInner: {
        width: '8px',
        height: '8px',
        borderRadius: '50%',
        backgroundColor: tokens.colorNeutralBackground1,
    },
    // Scope display
    scopeSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    scopeLabel: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
    },
    scopeValue: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    // Role grid
    roleGridContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    roleGridTitle: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    roleGrid: {
        borderRadius: tokens.borderRadiusLarge,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
        maxHeight: '250px',
        overflowY: 'auto',
    },
    roleRow: {
        display: 'flex',
        alignItems: 'center',
        padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        '&:last-child': {
            borderBottom: 'none',
        },
    },
    roleRowHeader: {
        backgroundColor: tokens.colorNeutralBackground2,
        fontWeight: tokens.fontWeightSemibold,
    },
    roleColumn: {
        flex: 1,
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground1,
        minWidth: '100px',
    },
    descriptionColumn: {
        flex: 2,
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
        minWidth: '200px',
    },
    statusColumn: {
        width: '100px',
        fontSize: tokens.fontSizeBase200,
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    statusGranted: {
        color: tokens.colorPaletteGreenForeground1,
    },
    statusNeeded: {
        color: tokens.colorPaletteYellowForeground1,
    },
    // Summary section
    summarySection: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
    },
    summaryItem: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    // Action area
    actionSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        alignItems: 'flex-start',
    },
    successMessage: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        color: tokens.colorPaletteGreenForeground1,
        fontSize: tokens.fontSizeBase300,
    },
    errorMessage: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        color: tokens.colorPaletteRedForeground1,
        fontSize: tokens.fontSizeBase200,
    },
    warningMessage: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        color: tokens.colorPaletteYellowForeground1,
        fontSize: tokens.fontSizeBase200,
        padding: tokens.spacingVerticalS,
        backgroundColor: tokens.colorPaletteYellowBackground1,
        borderRadius: tokens.borderRadiusLarge,
    },
    infoMessage: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        padding: tokens.spacingVerticalS,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusLarge,
    },
    // Loading state
    loadingContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
        padding: tokens.spacingVerticalL,
    },
    loadingText: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground2,
    },
});
