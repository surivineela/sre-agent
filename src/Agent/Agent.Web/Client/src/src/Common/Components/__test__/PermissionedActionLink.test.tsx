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
        // Fluent UI Link without href renders as a button
        const link = screen.getByRole('button', { name: 'Edit' });
        await userEvent.click(link);
        expect(onClick).toHaveBeenCalled();
    });

    it('renders disabled link when no permission', () => {
        render(
            <Wrapper>
                <PermissionedActionLink canPerform={false} noPermissionTooltip="No access">
                    Edit
                </PermissionedActionLink>
            </Wrapper>
        );
        // Tooltip with relationship="label" sets aria-label to the tooltip content
        const link = screen.getByRole('button', { name: 'No access' });
        expect(link).toBeDisabled();
        expect(link).toHaveTextContent('Edit');
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
