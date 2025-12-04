import { makeStyles, tokens } from '@fluentui/react-components';

export const useSkillGroupCardStyles = makeStyles({
    groupContainer: {
        border: `2px dashed ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusLarge,
        padding: tokens.spacingVerticalXXL,
        backgroundColor: 'transparent',
    },
    skillsContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    expandLinkContainer: {
        display: 'flex',
        justifyContent: 'flex-start',
        marginTop: tokens.spacingVerticalS,
    },
    expandLink: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        cursor: 'pointer',
        ':hover': {
            color: tokens.colorNeutralForeground1,
        },
    },
    collapseLinkContainer: {
        display: 'flex',
        justifyContent: 'flex-start',
        marginTop: tokens.spacingVerticalS,
    },
    collapseLink: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        cursor: 'pointer',
        ':hover': {
            color: tokens.colorNeutralForeground1,
        },
    },
    // Compact skill card styles for use inside the group
    compactSkillCard: {
        padding: tokens.spacingVerticalS + ' ' + tokens.spacingHorizontalM,
        minHeight: 'unset',
    },
});
