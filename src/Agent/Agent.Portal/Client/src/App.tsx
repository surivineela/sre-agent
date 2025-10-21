import { FluentProvider, webDarkTheme, webLightTheme } from '@fluentui/react-components';
import { useUserPreferences } from './Common/Contexts/UserPreferencesContext';
import { SreAgentPortal } from './SreAgentPortal';
import { IntlProvider } from './Strings/Intl/IntlProvider';

// NOTE: react-helmet-async not needed React-19+; use built-in <title>, <meta>, etc. tags

const App = () => {
    const { resolvedTheme, locale } = useUserPreferences();

    return (
        <IntlProvider locale={locale}>
            <FluentProvider theme={resolvedTheme === 'dark' ? webDarkTheme : webLightTheme}>
                <SreAgentPortal />
            </FluentProvider>
        </IntlProvider>
    );
};

export default App;
