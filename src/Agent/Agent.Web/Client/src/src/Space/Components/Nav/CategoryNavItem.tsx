import {
    CopilotNavCategory,
    CopilotNavCategoryItem,
    CopilotNavSubItemGroup,
    SplitCopilotNavItem,
    tokens,
} from '@fluentui-copilot/react-copilot';
import { makeStyles, useId } from '@fluentui/react-components';
import { FC, memo } from 'react';
import { CategoryNavItemInput, PrimaryNavItemValues, SecondaryNavItemValues, SubNavItemInput } from '../../Contracts/SreAgentSpace';
import { constructNavItemId } from '../../Utilities';
import Fade from '../Fade';

interface ICategoryNavItemProps {
    categoryItem: CategoryNavItemInput;
    subItems: SubNavItemInput[];
    onClickCategoryNavItem: (tabValue: PrimaryNavItemValues) => void;
    onClickSubNavItem: (tabValue: PrimaryNavItemValues, secondaryNavItem: SecondaryNavItemValues) => void;
}

const useStyles = makeStyles({
    disabledSubItem: {
        backgroundColor: `${tokens.colorTransparentBackground} !important`,
    },
    disabledSubItemButton: {
        cursor: 'not-allowed',
        color: `${tokens.colorNeutralForegroundDisabled} !important`,
    },
});

const CategoryNavItem: FC<ICategoryNavItemProps> = props => {
    const {
        categoryItem: { isVisible, disabled, icon: Icon, value, label, ref },
        subItems,
        onClickCategoryNavItem,
        onClickSubNavItem,
    } = props;

    const categoryNavId = useId(value);

    const styles = useStyles();

    return (
        <Fade visible={isVisible} unmountOnExit>
            <div>
                <CopilotNavCategory value={value}>
                    <CopilotNavCategoryItem
                        disabled={disabled}
                        icon={Icon && <Icon />}
                        value={categoryNavId}
                        ref={ref}
                        onClick={() => onClickCategoryNavItem(value)}
                    >
                        {label}
                    </CopilotNavCategoryItem>
                    <CopilotNavSubItemGroup aria-labelledby={categoryNavId}>
                        {subItems
                            .filter(item => item.isVisible)
                            .map(item => (
                                <SplitCopilotNavItem
                                    key={item.value}
                                    ref={item.ref}
                                    className={item.disabled ? styles.disabledSubItem : undefined}
                                    navItem={{
                                        value: constructNavItemId(value, item.value, undefined),
                                        children: item.label,
                                        onClick: () => {
                                            if (item.onClick) {
                                                item.onClick();
                                                return;
                                            }
                                            onClickSubNavItem(value, item.value);
                                        },
                                        disabled: item.disabled,
                                        className: item.disabled ? styles.disabledSubItemButton : undefined,
                                    }}
                                />
                            ))}
                    </CopilotNavSubItemGroup>
                </CopilotNavCategory>
            </div>
        </Fade>
    );
};

export default memo(CategoryNavItem);
