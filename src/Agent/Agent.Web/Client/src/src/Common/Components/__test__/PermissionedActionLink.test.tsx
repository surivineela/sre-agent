import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, expect, it, vi } from 'vitest';
import PermissionedActionLink from '../PermissionedActionLink';

const Wrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => (
    <FluentProvider theme={webLightTheme}>
        <IntlProvider locale="en" messages={{}}>
            {children}
        </IntlProvider>
    </FluentProvider>
);

describe('PermissionedActionLink', () => {
    it('renders interactive link when permitted', async () => {
        const onClick = vi.fn();
        render(
            <Wrapper>
                <PermissionedActionLink canPerform={true} noPermissionTooltip="No" onClick={onClick}>
                    Edit
                </PermissionedActionLink>
            </Wrapper>
        );
        const link = screen.getByRole('link');
        await userEvent.click(link);
        expect(onClick).toHaveBeenCalled();
    });

    it('renders disabled span when no permission', () => {
        render(
            <Wrapper>
                <PermissionedActionLink canPerform={false} noPermissionTooltip="No access">
                    Edit
                </PermissionedActionLink>
            </Wrapper>
        );
        const link = screen.getByText('Edit');
        expect(link).toHaveAttribute('aria-disabled', 'true');
    });

    it('hides when hideIfNoPermission set', () => {
        render(
            <Wrapper>
                <PermissionedActionLink canPerform={false} hideIfNoPermission noPermissionTooltip="No access">
                    Hidden
                </PermissionedActionLink>
            </Wrapper>
        );
        expect(screen.queryByText('Hidden')).toBeNull();
    });
});
