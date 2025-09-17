import { FluentProvider, Menu, MenuButton, MenuList, MenuPopover, MenuTrigger, webLightTheme } from '@fluentui/react-components';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, expect, it, vi } from 'vitest';
import PermissionedMenuItem from '../PermissionedMenuItem';

const Wrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => (
    <FluentProvider theme={webLightTheme}>
        <IntlProvider locale="en" messages={{}}>
            {children}
        </IntlProvider>
    </FluentProvider>
);

/**
 * Helper to render a menu with our permissioned item so it actually mounts inside a MenuList
 */
const renderMenu = (node: React.ReactNode) => {
    return render(
        <Wrapper>
            <Menu>
                <MenuTrigger>
                    <MenuButton>Open</MenuButton>
                </MenuTrigger>
                <MenuPopover>
                    <MenuList>{node}</MenuList>
                </MenuPopover>
            </Menu>
        </Wrapper>
    );
};

describe('PermissionedMenuItem', () => {
    it('invokes onClick when permitted and not disabledReason', async () => {
        const onClick = vi.fn();
        renderMenu(
            <PermissionedMenuItem canPerform={true} noPermissionTooltip="No" onClick={onClick}>
                Allowed
            </PermissionedMenuItem>
        );
        // Open the menu to render items
        await userEvent.click(screen.getByRole('button', { name: 'Open' }));
        const item = screen.getByRole('menuitem', { name: 'Allowed' });
        await userEvent.click(item);
        expect(onClick).toHaveBeenCalled();
    });

    it('renders disabled (aria-disabled) inside tooltip when not permitted', async () => {
        const onClick = vi.fn();
        renderMenu(
            <PermissionedMenuItem canPerform={false} noPermissionTooltip="No permission" onClick={onClick}>
                Blocked
            </PermissionedMenuItem>
        );
        await userEvent.click(screen.getByRole('button', { name: 'Open' }));
        const textNode = screen.getByText('Blocked');
        const item = textNode.closest('[role="menuitem"]') as HTMLElement;
        expect(item).not.toBeNull();
        expect(item).toHaveAttribute('aria-disabled', 'true');
        await userEvent.click(textNode);
        expect(onClick).not.toHaveBeenCalled();
    });

    it('applies disabledReason (aria-disabled) when provided even with permission', async () => {
        const onClick = vi.fn();
        renderMenu(
            <PermissionedMenuItem canPerform={true} disabledReason={true} noPermissionTooltip="No" onClick={onClick}>
                Temporarily Disabled
            </PermissionedMenuItem>
        );
        await userEvent.click(screen.getByRole('button', { name: 'Open' }));
        const textNode = screen.getByText('Temporarily Disabled');
        const item = textNode.closest('[role="menuitem"]') as HTMLElement;
        expect(item).not.toBeNull();
        expect(item).toHaveAttribute('aria-disabled', 'true');
        await userEvent.click(textNode);
        expect(onClick).not.toHaveBeenCalled();
    });

    it('hides entirely when hideIfNoPermission is set and no permission', async () => {
        renderMenu(
            <PermissionedMenuItem canPerform={false} hideIfNoPermission noPermissionTooltip="No">
                Hidden Item
            </PermissionedMenuItem>
        );
        await userEvent.click(screen.getByRole('button', { name: 'Open' }));
        // Should not appear
        expect(screen.queryByText('Hidden Item')).toBeNull();
    });
});
