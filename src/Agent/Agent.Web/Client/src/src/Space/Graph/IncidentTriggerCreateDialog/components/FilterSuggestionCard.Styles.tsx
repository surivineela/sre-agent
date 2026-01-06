import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

export const useFilterSuggestionCardStyles = makeStyles({
    card: {
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalM),
        backgroundColor: tokens.colorNeutralBackground1,
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.gap(tokens.spacingVerticalM),
        cursor: 'pointer',
        ':hover': {
            boxShadow: tokens.shadow8,
        },
    },
    cardHeader: {
        display: 'flex',
        alignItems: 'flex-start',
        ...shorthands.gap(tokens.spacingHorizontalM),
    },
    iconWrapper: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '40px',
        height: '40px',
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        backgroundColor: tokens.colorBrandBackground2,
        flexShrink: 0,
    },
    icon: {
        fontSize: '20px',
        color: tokens.colorBrandForeground1,
    },
    headerContent: {
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.gap(tokens.spacingVerticalXS),
        flex: 1,
    },
    filterName: {
        color: tokens.colorNeutralForeground1,
        fontSize: tokens.fontSizeBase300,
        lineHeight: tokens.lineHeightBase300,
    },
    incidentCount: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
    },
    cardBody: {
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.gap(tokens.spacingVerticalXS),
    },
    filterRow: {
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap(tokens.spacingHorizontalXS),
    },
    filterLabel: {
        color: tokens.colorNeutralForeground2,
        fontWeight: tokens.fontWeightRegular,
    },
    filterValue: {
        color: tokens.colorNeutralForeground1,
        fontWeight: tokens.fontWeightRegular,
    },
    noFilters: {
        color: tokens.colorNeutralForeground3,
        fontStyle: 'italic',
    },
    cardFooter: {
        display: 'flex',
        justifyContent: 'flex-end',
        ...shorthands.padding(tokens.spacingVerticalS, 0, 0, 0),
    },
    applyButton: {
        minWidth: '80px',
    },
    appliedButton: {
        minWidth: '80px',
        backgroundColor: tokens.colorPaletteGreenBackground3,
        color: tokens.colorNeutralForegroundOnBrand,
        ':hover': {
            backgroundColor: tokens.colorPaletteGreenBackground3,
        },
    },
});
