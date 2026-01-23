import { makeStyles, tokens } from '@fluentui/react-components';

export const useKnowledgeSettingsStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        width: '100%',
    },
    header: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    title: {
        fontSize: tokens.fontSizeBase500,
        fontWeight: tokens.fontWeightSemibold,
        margin: 0,
    },
    description: {
        maxWidth: '580px',
        color: tokens.colorNeutralForeground2,
        lineHeight: tokens.lineHeightBase300,
    },
    learnMoreLink: {
        marginTop: tokens.spacingVerticalXS,
    },
    actionCardsContainer: {
        display: 'flex',
        flexDirection: 'row',
        gap: tokens.spacingHorizontalM,
        flexWrap: 'wrap',
    },
    toolbar: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
        paddingLeft: 0,
        marginLeft: '-8px',
    },
    deleteButton: {
        marginRight: '-12px',
    },
    searchBox: {
        width: '230px',
    },
    lastIndexedText: {
        marginLeft: 'auto',
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        fontWeight: tokens.fontWeightRegular,
        '& button': {
            fontWeight: tokens.fontWeightRegular,
        },
        '& svg': {
            fontWeight: tokens.fontWeightRegular,
        },
    },
    dataGridContainer: {
        width: '100%',
        overflow: 'visible',
    },
    filesSelectedText: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
    },
});

export const useActionCardStyles = makeStyles({
    card: {
        width: '160px',
        height: '80px',
        padding: '16px',
        cursor: 'pointer',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: '16px',
        backgroundColor: tokens.colorNeutralBackground1,
        boxShadow: tokens.shadow4,
        transition: 'border-color 0.15s ease, box-shadow 0.15s ease',
        ':hover': {
            border: `1px solid ${tokens.colorBrandStroke1}`,
            boxShadow: tokens.shadow8,
        },
        ':focus-visible': {
            outline: `2px solid ${tokens.colorBrandStroke1}`,
            outlineOffset: '2px',
        },
    },
    cardDisabled: {
        cursor: 'not-allowed',
        opacity: 0.5,
        ':hover': {
            border: `1px solid ${tokens.colorNeutralStroke1}`,
            boxShadow: 'none',
        },
    },
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-start',
        justifyContent: 'center',
        height: '100%',
        gap: tokens.spacingVerticalXS,
    },
    iconContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'flex-start',
        width: '20px',
        height: '20px',
        color: tokens.colorNeutralForeground2,
        '& > svg': {
            width: '20px',
            height: '20px',
        },
    },
    label: {
        lineHeight: '20px',
        fontWeight: tokens.fontWeightRegular,
        textAlign: 'left',
        color: tokens.colorNeutralForeground1,
    },
});

export const useKnowledgeSettingsEmptyStateStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        padding: tokens.spacingVerticalXXL,
        gap: tokens.spacingVerticalL,
        minHeight: '400px',
        flex: 1,
    },
    illustration: {
        width: '150px',
        height: '150px',
    },
    textContainer: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        gap: tokens.spacingVerticalS,
        maxWidth: '400px',
    },
    title: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    description: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground2,
    },
});
