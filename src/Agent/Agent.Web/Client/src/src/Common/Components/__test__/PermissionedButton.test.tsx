import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, expect, it, vi } from 'vitest';
import PermissionedButton from '../PermissionedButton';

const Wrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => (
    <FluentProvider theme={webLightTheme}>
        <IntlProvider locale="en" messages={{}}>
            {children}
        </IntlProvider>
    </FluentProvider>
);

describe('PermissionedButton', () => {
    it('renders enabled when canPerform', async () => {
        const onClick = vi.fn();
        render(
            <Wrapper>
                <PermissionedButton canPerform={true} noPermissionTooltip="No" onClick={onClick}>
                    Do It
                </PermissionedButton>
            </Wrapper>
        );
        const btn = screen.getByRole('button', { name: 'Do It' });
        expect(btn).toBeEnabled();
        await userEvent.click(btn);
        expect(onClick).toHaveBeenCalled();
    });

    it('shows tooltip wrapper and is disabled when no permission', () => {
        render(
            <Wrapper>
                <PermissionedButton canPerform={false} noPermissionTooltip="No access">
                    Do It
                </PermissionedButton>
            </Wrapper>
        );
        // Tooltip provides aria-label so accessible name shifts; fall back to text lookup then climb to button
        const textNode = screen.getByText('Do It');
        const btn = textNode.closest('button');
        expect(btn).not.toBeNull();
        expect(btn).toBeDisabled();
    });

    it('applies disabledReason when provided', () => {
        render(
            <Wrapper>
                <PermissionedButton canPerform={true} disabledReason={true} noPermissionTooltip="No">
                    Do It
                </PermissionedButton>
            </Wrapper>
        );
        const btn = screen.getByRole('button', { name: 'Do It' });
        expect(btn).toBeDisabled();
    });
});
