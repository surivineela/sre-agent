import { Body1, CopilotNavCategoryItem as NavCategoryItem } from '@fluentui-copilot/react-copilot';
import { mergeClasses } from '@fluentui/react-components';
import { forwardRef } from 'react';
import { useCopilotNavItemStyles } from './Common.styles';

export const CopilotNavCategoryItem = forwardRef<HTMLButtonElement, React.ComponentProps<typeof NavCategoryItem>>(
    ({ className, children, ...rest }, ref) => {
        const styles = useCopilotNavItemStyles();

        return (
            <NavCategoryItem ref={ref} className={mergeClasses(styles.navItem, className)} {...rest}>
                {children && <Body1 wrap={false}>{children}</Body1>}
            </NavCategoryItem>
        );
    }
);
