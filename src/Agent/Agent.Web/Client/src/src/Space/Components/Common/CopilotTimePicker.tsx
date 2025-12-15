import { tokens } from '@fluentui-copilot/react-copilot';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { TimePicker } from '@fluentui/react-timepicker-compat';
import { forwardRef } from 'react';

const useStyles = makeStyles({
    timePicker: {
        '& input': { textAlign: 'left', lineHeight: tokens.lineHeightBase450, fontSize: tokens.fontSizeBase400, paddingLeft: '0px' },
        border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke1}`,
        borderBottomColor: tokens.colorNeutralStrokeAccessible,
        borderRadius: tokens.borderRadius2XL,
        padding: `calc(${tokens.spacingVerticalMNudge} - ${tokens.strokeWidthThick}) ${tokens.spacingHorizontalL}`,
        lineHeight: tokens.lineHeightBase450,
        height: 'unset',
        overflow: 'hidden',
    },
});

export const CopilotTimePicker = forwardRef<HTMLInputElement, React.ComponentProps<typeof TimePicker>>((props, ref) => {
    const { className, ...rest } = props;

    const styles = useStyles();

    return <TimePicker ref={ref} className={mergeClasses(styles.timePicker, className)} {...rest} />;
});
