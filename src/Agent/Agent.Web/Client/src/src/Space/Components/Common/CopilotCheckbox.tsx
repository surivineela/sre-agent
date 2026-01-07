import { Checkbox } from '@fluentui/react-components';
import { forwardRef } from 'react';

export const CopilotCheckbox = forwardRef<HTMLInputElement, React.ComponentProps<typeof Checkbox>>((props, ref) => {
    return <Checkbox ref={ref} {...props} label={{ children: props.label, style: { lineHeight: 'unset' } }} />;
});
