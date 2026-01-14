import { SplitCopilotNavItem as NavSplitItem } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { forwardRef } from 'react';
import { useSplitCopilotNavItemStyles } from './Common.styles';

export const SplitCopilotNavItem = forwardRef<HTMLDivElement, React.ComponentProps<typeof NavSplitItem>>(({ className, ...rest }, ref) => {
    const styles = useSplitCopilotNavItemStyles();

    return <NavSplitItem ref={ref} className={mergeClasses(styles.splitNavItem, className)} {...rest} />;
});
