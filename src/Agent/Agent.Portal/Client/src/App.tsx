import { FluentProvider, webDarkTheme, webLightTheme } from '@fluentui/react-components';
import { useEffect } from 'react';
import { NotificationProvider } from './Common/Contexts/NotificationContext';
import { SubscriptionsProvider } from './Common/Contexts/SubscriptionsContext';
import { useUserPreferences } from './Common/Contexts/UserPreferencesContext';
import { SreAgentPortal } from './SreAgentPortal';
import { IntlProvider } from './Strings/Intl/IntlProvider';

// NOTE: react-helmet-async not needed React-19+; use built-in <title>, <meta>, etc. tags

const App = () => {
    const { resolvedTheme, locale } = useUserPreferences();

    // Set data-theme attribute on document for CSS theming
    useEffect(() => {
        document.documentElement.setAttribute('data-theme', resolvedTheme);
    }, [resolvedTheme]);

    return (
        <IntlProvider locale={locale}>
            <FluentProvider theme={resolvedTheme === 'dark' ? webDarkTheme : webLightTheme}>
                <NotificationProvider>
                    <SubscriptionsProvider>
                        <SreAgentPortal />
                    </SubscriptionsProvider>
                </NotificationProvider>
            </FluentProvider>
        </IntlProvider>
    );
};

export default App;
