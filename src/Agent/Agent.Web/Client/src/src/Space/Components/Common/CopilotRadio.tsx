import { Radio as FluentRadio } from '@fluentui/react-components';
import { forwardRef } from 'react';

export const CopilotRadio = forwardRef<HTMLInputElement, React.ComponentProps<typeof FluentRadio>>((props, ref) => {
    return <FluentRadio ref={ref} {...props} label={{ children: props.label, style: { lineHeight: 'unset' } }} />;
});
