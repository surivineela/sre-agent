import { Body1, CopilotNavCategory, CopilotNavSubItemGroup, tokens } from '@fluentui-copilot/react-copilot';
import { makeStyles, Menu, MenuItem, MenuList, MenuPopover, MenuTrigger, mergeClasses, Tooltip, useId } from '@fluentui/react-components';
import { FC, memo } from 'react';
import { useLocation } from 'react-router-dom';
import { CategoryNavItemInput, PrimaryNavItemValues, SecondaryNavItemValues, SubNavItemInput } from '../../Contracts/SreAgentSpace';
import { constructNavItemId, getCategoryNavItemIdFromPathName, getNavItemIdFromPathName } from '../../Utilities';
import { CopilotNavCategoryItem } from './CopilotNavCategoryItem';
import { CopilotNavItem } from './CopilotNavItem';
import { SplitCopilotNavItem } from './SplitCopilotNavItem';

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
    selectedNavCommon: {
        position: 'relative',
        '::after': {
            content: '" "',
            display: 'block',
            width: '3px',
            height: '14px',
            transform: 'translateY(-50%)',
            top: '50%',
            left: '0px',
            position: 'absolute',
            backgroundColor: tokens.colorCompoundBrandForeground1,
            pointerEvents: 'none',
            zIndex: 1,
            opacity: 1,
            marginInlineStart: '0px',
        },
    },
    selectedNavButton: {
        backgroundColor: tokens.colorNeutralBackground4Selected,
        '& .fui-NavItem__icon': {
            color: tokens.colorCompoundBrandForeground1,
        },
    },
    selectedNavMenuItem: {
        backgroundColor: tokens.colorNeutralBackground3Hover,
    },
});

const getSubItemProps = (
    item: SubNavItemInput,
    value: PrimaryNavItemValues,
    onClickSubNavItem: (tabValue: PrimaryNavItemValues, secondaryNavItem: SecondaryNavItemValues) => void
) => {
    return {
        label: item.label,
        key: item.value,
        ref: item.ref,
        value: constructNavItemId(value, item.value, undefined),
        onClick: () => {
            if (item.onClick) {
                item.onClick();
                return;
            }
            onClickSubNavItem(value, item.value);
        },
        disabled: item.disabled,
    };
};

const CategoryNavItem: FC<ICategoryNavItemProps> = props => {
    const {
        categoryItem: { isCollapsed, disabled, icon: Icon, filledIcon: FilledIcon, value, label, ref },
        subItems,
        onClickCategoryNavItem,
        onClickSubNavItem,
    } = props;

    const location = useLocation();

    const categoryNavId = useId(value);

    const styles = useStyles();

    const navItemProps = {
        disabled: disabled,
        ref: ref,
        onClick: () => onClickCategoryNavItem(value),
    };

    return isCollapsed ? (
        <Menu positioning={{ autoSize: true, position: 'after', align: 'end' }}>
            <MenuTrigger disableButtonEnhancement>
                <Tooltip content={label} relationship="label">
                    <CopilotNavItem
                        {...navItemProps}
                        value={value}
                        icon={
                            getCategoryNavItemIdFromPathName(location.pathname) === value ? FilledIcon && <FilledIcon /> : Icon && <Icon />
                        }
                        aria-label={label}
                        className={
                            getCategoryNavItemIdFromPathName(location.pathname) === value
                                ? mergeClasses(styles.selectedNavButton, styles.selectedNavCommon)
                                : undefined
                        }
                    />
                </Tooltip>
            </MenuTrigger>
            <MenuPopover>
                <SubMenuList items={subItems} value={value} onClickSubNavItem={onClickSubNavItem} />
            </MenuPopover>
        </Menu>
    ) : (
        <div>
            <CopilotNavCategory value={value}>
                <CopilotNavCategoryItem {...navItemProps} icon={Icon && <Icon />} value={categoryNavId}>
                    {label}
                </CopilotNavCategoryItem>
                <CopilotNavSubItemGroup aria-labelledby={categoryNavId}>
                    <SubItems items={subItems} value={value} onClickSubNavItem={onClickSubNavItem} />
                </CopilotNavSubItemGroup>
            </CopilotNavCategory>
        </div>
    );
};

const SubMenuList = memo(
    ({
        items,
        value,
        onClickSubNavItem,
    }: {
        items: SubNavItemInput[];
        value: PrimaryNavItemValues;
        onClickSubNavItem: (tabValue: PrimaryNavItemValues, secondaryNavItem: SecondaryNavItemValues) => void;
    }) => {
        const styles = useStyles();
        const location = useLocation();

        return (
            <MenuList>
                {items.map(item => {
                    const subItemProps = getSubItemProps(item, value, onClickSubNavItem);
                    return (
                        <MenuItem
                            key={subItemProps.key}
                            ref={subItemProps.ref}
                            disabled={subItemProps.disabled}
                            onClick={subItemProps.onClick}
                            className={
                                getNavItemIdFromPathName(location.pathname) === subItemProps.value
                                    ? mergeClasses(styles.selectedNavMenuItem, styles.selectedNavCommon)
                                    : undefined
                            }
                        >
                            {subItemProps.label}
                        </MenuItem>
                    );
                })}
            </MenuList>
        );
    }
);

const SubItems = memo(
    ({
        items,
        value,
        onClickSubNavItem,
    }: {
        items: SubNavItemInput[];
        value: PrimaryNavItemValues;
        onClickSubNavItem: (tabValue: PrimaryNavItemValues, secondaryNavItem: SecondaryNavItemValues) => void;
    }) => {
        return (
            <>
                {items
                    .filter(item => item.isVisible)
                    .map(item => {
                        return <SubItem key={item.value} item={item} value={value} onClickSubNavItem={onClickSubNavItem} />;
                    })}
            </>
        );
    }
);

const SubItem = memo(
    ({
        item,
        value,
        onClickSubNavItem,
    }: {
        item: SubNavItemInput;
        value: PrimaryNavItemValues;
        onClickSubNavItem: (tabValue: PrimaryNavItemValues, secondaryNavItem: SecondaryNavItemValues) => void;
    }) => {
        const styles = useStyles();
        const subItemProps = getSubItemProps(item, value, onClickSubNavItem);

        return (
            <SplitCopilotNavItem
                key={subItemProps.key}
                ref={subItemProps.ref}
                className={subItemProps.disabled ? styles.disabledSubItem : undefined}
                navItem={{
                    value: subItemProps.value,
                    children: <Body1 wrap={false}>{item.label}</Body1>,
                    onClick: subItemProps.onClick,
                    disabled: subItemProps.disabled,
                    className: subItemProps.disabled ? styles.disabledSubItemButton : undefined,
                }}
            />
        );
    }
);

export default memo(CategoryNavItem);
