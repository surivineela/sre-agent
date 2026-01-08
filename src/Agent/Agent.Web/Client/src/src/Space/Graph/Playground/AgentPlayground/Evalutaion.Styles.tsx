import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

export const useEvaluationStyles = makeStyles({
    hidden: {
        display: 'none',
    },
    watcherPanel: {
        backgroundColor: tokens.colorNeutralBackground1,
        display: 'flex',
        flexDirection: 'column',
        flex: '1 1 auto',
        maxHeight: '100%',
        height: '100%',
        minHeight: 0,
    },
    watcherPanelBody: {
        flex: '1 1 auto',
        overflowY: 'auto',
        ...shorthands.padding(tokens.spacingVerticalL, tokens.spacingHorizontalL),
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        minHeight: 0,
    },
    watcherScoresRow: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    watcherSubscoreList: {
        display: 'grid',
        gridTemplateColumns: 'repeat(2, 1fr)',
        gap: tokens.spacingHorizontalS,
        margin: 0,
        padding: 0,
        listStyle: 'none',
    },
    watcherSubscoreItem: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusSmall,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalS),
    },
    watcherSubscoreLabel: {
        fontSize: tokens.fontSizeBase100,
        color: tokens.colorNeutralForeground3,
        textTransform: 'uppercase',
        letterSpacing: '0.04em',
    },
    inProgressWrapper: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        alignItems: 'center',
        marginTop: tokens.spacingVerticalXL,
    },
    inProgressText: {
        color: tokens.colorNeutralForeground3,
        textAlign: 'center',
    },
    insightsErrorMessageBar: {
        maxWidth: '700px',
    },
    messageBarWrapper: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
    },
    messageBar: {
        borderRadius: '8px',
        maxWidth: 'unset',
        width: '100%',
    },
    overallScoreCard: {
        backgroundColor: tokens.colorBrandBackground2,
        borderRadius: tokens.borderRadiusMedium,
        padding: `${tokens.spacingVerticalL} ${tokens.spacingHorizontalL}`,
        border: `1px solid ${tokens.colorBrandStroke2}`,
        textAlign: 'center' as const,
    },
    overallScoreLabel: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        textTransform: 'uppercase',
        letterSpacing: '0.5px',
        display: 'block',
        marginBottom: tokens.spacingVerticalXS,
    },
    overallScoreValue: {
        fontSize: '48px',
        fontWeight: tokens.fontWeightBold,
        color: tokens.colorBrandForeground1,
        lineHeight: '1.2',
    },
    overallScoreEvidence: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
        marginTop: tokens.spacingVerticalXXS,
    },
    intentMatchScore: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusSmall,
        padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    },
    intentMatchScoreLabelWrapper: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXXS,
    },
    intentMatchScoreLabel: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
    },
    tooltipContent: {
        maxWidth: '300px',
    },
    tooltipTitle: {
        fontWeight: tokens.fontWeightSemibold,
        marginBottom: '4px',
    },
    tooltipBody: {
        fontSize: tokens.fontSizeBase200,
    },
    tooltipList: {
        marginTop: '8px',
    },
    infoIcon: {
        color: tokens.colorNeutralForeground3,
        cursor: 'pointer',
        width: '12px',
        height: '12px',
    },
    subscoreLabelWrapper: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
    },
    subscoreValue: {
        fontSize: tokens.fontSizeBase500,
        fontWeight: tokens.fontWeightBold,
        margin: 0,
    },
    subscoreEvidence: {
        fontSize: tokens.fontSizeBase100,
        color: tokens.colorNeutralForeground3,
        margin: 0,
    },
    tooltipContentSmall: {
        maxWidth: '250px',
    },
    highlightsSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    highlightGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    highlightGroupTitle: {
        color: tokens.colorNeutralForeground2,
    },
    highlightList: {
        margin: 0,
        paddingLeft: '16px',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    highlightItem: {
        color: tokens.colorNeutralForeground2,
        margin: 0,
    },
    emptyStateWrapper: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        height: '100%',
        gap: '16px',
        overflow: 'auto',
    },
    emptyStateIcon: {
        width: '128px',
        height: '128px',
        color: tokens.colorBrandForeground1,
    },
    footerStatusWrapper: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        flex: 1,
    },
    footerStatusText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase100,
    },
});
