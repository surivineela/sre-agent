import { makeStyles, tokens } from '@fluentui/react-components';

export const useMessageBarStyles = makeStyles({
    // Workaround for Fluent bug
    messageBar: {
        // Reduced to account for the border
        // stylelint-disable-next-line declaration-property-value-allowed-list
        paddingTop: `calc(${tokens.spacingHorizontalSNudge} - ${tokens.strokeWidthThin})`,
        // stylelint-disable-next-line declaration-property-value-allowed-list
        paddingBottom: `calc(${tokens.spacingHorizontalSNudge} - ${tokens.strokeWidthThin})`,
        whiteSpace: 'normal',

        // stylelint-disable selector-class-pattern
        '& .fui-MessageBar__icon': {
            alignSelf: 'start',
            transform: 'translateY(2px)' /* Optical alignment */,
        },
        '& .fui-MessageBarActions': {
            alignSelf: 'start',
        },
        '& .fui-MessageBarActions__containerAction': {
            alignSelf: 'start',
        },
    },
});
