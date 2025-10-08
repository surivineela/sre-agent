import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { IntlProvider } from 'react-intl';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { ActivitiesResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { StreamingContext } from '../../Contracts/Context';
import { PermissionContext } from '../../Contracts/PermissionContext';
import ChatBoxFooter from '../ChatBoxFooter';

const AzPortalContext = React.createContext({ logAmplitudeControlEvent: () => {} });

// Provide only the props actually used for aria-label toggle
const baseProps = {
    sendMessage: vi.fn().mockResolvedValue(undefined),
    isLoading: false,
    onClickDownButton: vi.fn(),
    downButtonState: { visible: false, flash: false },
    prompts: [],
    messagePromptsUsed: [],
    cancelStreaming: vi.fn(),
    isTyping: false,
    isCancellingStreaming: false,
    threadId: 't1',
    threadSource: 'test',
    showDeepInvestigationButton: false,
    isDeepInvestigationButtonEnabled: true,
    isDeepInvestigationTurnedOn: false,
    onClickDeepInvestigationButton: vi.fn(),
};

const Wrapper: React.FC<{ children: React.ReactNode; permission?: boolean }> = ({ children, permission = true }) => (
    <MemoryRouter>
        <FluentProvider theme={webLightTheme}>
            <IntlProvider locale="en" messages={{}}>
                <AzPortalContext.Provider value={{ logAmplitudeControlEvent: () => {} }}>
                    <StreamingContext.Provider
                        value={{
                            isConnected: true,
                            isConnecting: false,
                            isReconnecting: false,
                            noPermission: false,
                            startMessageStreamingOnNewThread: () => {},
                            startMessageStreamingOnExistingThread: () => {},
                            cancelMessageStreaming: () => {},
                            subscribeMessageUpdateEvent: () => () => {},
                            subscribeThreadUpdateEvent: () => () => {},
                            subscribeTaskUpdateEvent: () => () => {},
                        }}
                    >
                        <PermissionContext.Provider
                            value={{
                                canWriteThreads: permission,
                                canDeleteThreads: false,
                                canApproveThreads: false,
                                loading: false,
                                error: false,
                                refresh: () => {},
                            }}
                        >
                            {children}
                        </PermissionContext.Provider>
                    </StreamingContext.Provider>
                </AzPortalContext.Provider>
            </IntlProvider>
        </FluentProvider>
    </MemoryRouter>
);

describe('ChatBoxFooter send/cancel aria-label', () => {
    it('shows Send message aria-label when not typing', () => {
        render(
            <Wrapper>
                <ChatBoxFooter {...baseProps} isTyping={false} />
            </Wrapper>
        );

        // Accessible name for the icon-only button
        const sendButton = screen.getByRole('button', { name: ActivitiesResources.sendMessageAriaLabel.defaultMessage });
        expect(sendButton).toBeInTheDocument();
    });

    it('shows Cancel generation aria-label when typing (streaming)', () => {
        render(
            <Wrapper>
                <ChatBoxFooter {...baseProps} isTyping={true} />
            </Wrapper>
        );

        const cancelButton = screen.getByRole('button', { name: SreAgentResources.stop.defaultMessage });
        expect(cancelButton).toBeInTheDocument();
    });

    it('toggles after state change (simulate by re-render)', async () => {
        const { rerender } = render(
            <Wrapper>
                <ChatBoxFooter {...baseProps} isTyping={false} />
            </Wrapper>
        );

        const sendButton = screen.getByRole('button', { name: ActivitiesResources.sendMessageAriaLabel.defaultMessage });
        expect(sendButton).toBeInTheDocument();

        rerender(
            <Wrapper>
                <ChatBoxFooter {...baseProps} isTyping={true} />
            </Wrapper>
        );

        const cancelButton = screen.getByRole('button', { name: SreAgentResources.stop.defaultMessage });
        expect(cancelButton).toBeInTheDocument();
    });
});
