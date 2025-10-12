import { makeStyles, tokens } from '@fluentui/react-components';

export const useMetricsBadgeStyles = makeStyles({
    badge: {
        minWidth: '16px',
        borderRadius: tokens.borderRadiusCircular,
        gap: '0px',
        opacity: 1,
        paddingTop: tokens.spacingVerticalXXS,
        paddingRight: tokens.spacingHorizontalXS,
        paddingBottom: tokens.spacingVerticalXXS,
        paddingLeft: tokens.spacingHorizontalXS,
        border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground4,

        fontFamily: tokens.fontFamilyBase,
        fontWeight: 600,
        fontStyle: 'Semibold',
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
        letterSpacing: '0%',
        textAlign: 'center',
        verticalAlign: 'middle',
    },
});
