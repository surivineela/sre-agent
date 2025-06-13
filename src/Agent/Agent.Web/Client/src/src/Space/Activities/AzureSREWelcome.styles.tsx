import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

export const useWelcomeStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXL,
        fontFamily: tokens.fontFamilyBase,
    },
    headerCard: {
        marginBottom: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.borderRadius('15px'), // Updated to match graph styles
        ...shorthands.padding(tokens.spacingVerticalL),
        border: `1px solid ${tokens.colorNeutralStroke2}`, // Added border to match graph styles
    },
    sectionCard: {
        marginBottom: tokens.spacingVerticalL,
        backgroundColor: tokens.colorNeutralBackground2, // Matches graph card style
        ...shorthands.borderRadius('15px'), // Updated to match graph styles
        ...shorthands.padding(tokens.spacingVerticalS),
        border: `1px solid ${tokens.colorNeutralStroke2}`, // Added border to match graph styles
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
    },
    sectionHeader: {
        display: 'flex',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalS,
        padding: tokens.spacingVerticalM,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    collapsibleHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        cursor: 'pointer',
        width: '100%', // Ensure full width
    },
    sectionContent: {
        padding: tokens.spacingVerticalS,
        marginTop: '-25px',
    },
    sectionHeaderIcon: {
        marginRight: tokens.spacingHorizontalS,
        color: tokens.colorBrandForeground1,
        fontSize: '24px',
    },
    welcomeMessage: {
        marginBottom: tokens.spacingVerticalL,
        padding: tokens.spacingHorizontalL,
        fontSize: tokens.fontSizeBase400,
        lineHeight: tokens.lineHeightBase400,
        color: tokens.colorNeutralForeground1,
    },
    featureGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: tokens.spacingHorizontalM,
        marginTop: tokens.spacingVerticalL,
    },
    featureItem: {
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalL),
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        cursor: 'pointer',
        transition: 'all 0.2s ease',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
            transform: 'translateY(-2px)',
        },
    },
    featureIcon: {
        marginRight: tokens.spacingHorizontalS,
        color: tokens.colorBrandForeground1,
    },
    featureDetails: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        marginTop: tokens.spacingVerticalS,
    },
    featureDetailItem: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    checkIcon: {
        color: tokens.colorPaletteGreenForeground1,
        marginRight: tokens.spacingHorizontalXS,
    },
    statusCompleteIcon: {
        color: tokens.colorPaletteGreenForeground1,
        marginLeft: tokens.spacingHorizontalS,
    },
    featureContent: {
        marginTop: tokens.spacingVerticalS,
        padding: tokens.spacingVerticalS,
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusMedium,
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke1),
    },
    progressContainer: {
        marginBottom: tokens.spacingVerticalM,
    },
    progressHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        marginBottom: tokens.spacingVerticalXS,
    },
    statsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(5, 1fr)',
        gap: tokens.spacingHorizontalM,
        marginBottom: tokens.spacingVerticalM,
    },
    statCard: {
        ...shorthands.padding(tokens.spacingVerticalL),
        borderRadius: tokens.borderRadiusMedium,
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
    },
    statValue: {
        fontSize: '24px',
        fontWeight: tokens.fontWeightSemibold,
    },
    statLabel: {
        color: tokens.colorNeutralForeground2,
        maxWidth: '150px',
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    integrationCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusMedium,
        ...shorthands.padding(tokens.spacingVerticalM),
        position: 'relative',
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        marginBottom: tokens.spacingVerticalM,
    },
    integrationHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalS,
    },
    integrationDetails: {
        marginTop: tokens.spacingVerticalXS,
        marginBottom: tokens.spacingVerticalM,
    },
    integrationAction: {
        marginTop: tokens.spacingVerticalS,
    },
    activeBadge: {
        backgroundColor: tokens.colorPaletteGreenBackground1,
        color: tokens.colorPaletteGreenForeground1,
        ...shorthands.padding('2px', '8px'),
        borderRadius: '12px',
        fontSize: tokens.fontSizeBase100,
        fontWeight: tokens.fontWeightSemibold,
    },
    inactiveBadge: {
        backgroundColor: tokens.colorNeutralBackground4,
        color: tokens.colorNeutralForeground3,
        ...shorthands.padding('2px', '8px'),
        borderRadius: '12px',
        fontSize: tokens.fontSizeBase100,
        fontWeight: tokens.fontWeightSemibold,
    },
    applicationSection: {
        marginTop: tokens.spacingVerticalM,
    },
    applicationList: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    applicationCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusMedium,
        ...shorthands.padding(tokens.spacingVerticalM),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    applicationCardLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    applicationIcon: {
        width: '40px',
        height: '40px',
        borderRadius: '4px',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        color: tokens.colorNeutralForeground1,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    applicationInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    applicationName: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase400,
        textOverflow: 'ellipsis', // Added to match graph styles
        overflow: 'hidden', // Added to match graph styles
        maxWidth: '180px', // Added to prevent long names from breaking layout
    },
    applicationSubtext: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase300,
    },
    resourceLearningMessage: {
        marginBottom: tokens.spacingVerticalM,
        padding: tokens.spacingHorizontalM,
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    linkStatus: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        marginTop: tokens.spacingVerticalXS,
    },
    statusDot: {
        width: '8px',
        height: '8px',
        borderRadius: '50%',
    },
    healthTag: {
        display: 'flex',
        alignItems: 'center',
        backgroundColor: tokens.colorNeutralBackground1,
        padding: '4px 8px',
        borderRadius: '12px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        fontSize: tokens.fontSizeBase200,
    },
    reposRemainingTag: {
        backgroundColor: tokens.colorNeutralBackground4,
        color: tokens.colorNeutralForeground2,
        ...shorthands.padding('2px', '10px'),
        borderRadius: '12px',
        fontSize: tokens.fontSizeBase200,
        fontWeight: tokens.fontWeightSemibold,
        marginLeft: tokens.spacingHorizontalS,
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    healthDot: {
        width: '8px',
        height: '8px',
        borderRadius: '50%',
        marginRight: '4px',
    },
    dialogHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalM,
    },
    dialogTitle: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    dialogTitleIcon: {
        color: tokens.colorBrandForeground1,
    },
    dialogCloseButton: {
        backgroundColor: 'transparent',
        border: 'none',
        cursor: 'pointer',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        color: tokens.colorNeutralForeground3,
        ':hover': {
            color: tokens.colorNeutralForeground2,
        },
    },
    reAuthRequired: {
        backgroundColor: tokens.colorPaletteYellowBackground1,
        color: tokens.colorPaletteYellowForeground1,
        ...shorthands.padding('2px', '8px'),
        borderRadius: '12px',
        fontSize: tokens.fontSizeBase100,
        fontWeight: tokens.fontWeightSemibold,
        marginLeft: tokens.spacingHorizontalS,
    },
});
