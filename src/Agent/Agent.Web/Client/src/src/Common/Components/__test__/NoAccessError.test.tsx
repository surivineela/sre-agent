import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, expect, it, vi } from 'vitest';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import NoAccessError from '../NoAccessError';

const messages: Record<string, string> = Object.fromEntries(
    Object.entries(SreAgentResources).map(([, v]: any) => [v.id, v.defaultMessage])
);

const Wrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => (
    <FluentProvider theme={webLightTheme}>
        <IntlProvider locale="en" messages={messages}>
            {children}
        </IntlProvider>
    </FluentProvider>
);

describe('NoAccessError', () => {
    it('renders heading and permission text', () => {
        render(
            <Wrapper>
                <NoAccessError requiredPermission="Agent.Read" resourceId="agent-123" />
            </Wrapper>
        );
        expect(screen.getByRole('heading', { name: /You do not have access/i })).toBeInTheDocument();
        const container = screen.getByTestId('no-access-error');
        const text = container.textContent || '';
        expect(text).toMatch(/Agent.Read/);
    });

    it('copies error details when copy button clicked', async () => {
        const writeTextMock = vi.fn();
        (navigator as any).clipboard = { writeText: writeTextMock };
        render(
            <Wrapper>
                <NoAccessError requiredPermission="Agent.Manage" resourceId="agent-xyz" />
            </Wrapper>
        );
        const copyButton = screen.getByRole('button', { name: /Copy/i });
        await userEvent.click(copyButton);
        expect(writeTextMock).toHaveBeenCalled();
        const arg = writeTextMock.mock.calls[0][0];
        expect(arg).toContain('agent-xyz');
        expect(arg).toContain('Agent.Manage');
    });
});
