import { Body1 } from '@fluentui-copilot/react-copilot';
import { CopilotNavItem as NavItem } from '@fluentui-copilot/react-copilot-nav';
import { mergeClasses } from '@fluentui/react-components';
import { forwardRef } from 'react';
import { useCopilotNavItemStyles } from './Common.styles';

export const CopilotNavItem = forwardRef<HTMLButtonElement, React.ComponentProps<typeof NavItem>>(
    ({ className, children, ...rest }, ref) => {
        const styles = useCopilotNavItemStyles();

        return (
            <NavItem ref={ref} className={mergeClasses(styles.navItem, children ? undefined : styles.iconOnlyNavItem, className)} {...rest}>
                {children && <Body1 wrap={false}>{children}</Body1>}
            </NavItem>
        );
    }
);
