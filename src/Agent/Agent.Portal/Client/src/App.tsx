import { FluentProvider, webDarkTheme, webLightTheme } from '@fluentui/react-components';
import { SreAgentPortal } from './SreAgentPortal';
import { IntlProvider } from './Strings/Intl/IntlProvider';

// NOTE: react-helmet-async not needed React-19+; use built-in <title>, <meta>, etc. tags

// TODO: Hook up locale to in-site setting
const locale = typeof navigator !== 'undefined' ? navigator.language || 'en' : 'en';

const App = () => {
    const isDarkTheme = true;

    // TODO: localStorage stuff probably here (locale default to above first time; theme always default to dark first time)

    return (
        <IntlProvider locale={locale}>
            <FluentProvider theme={isDarkTheme ? webDarkTheme : webLightTheme}>
                <SreAgentPortal />
            </FluentProvider>
        </IntlProvider>
    );
};

export default App;
