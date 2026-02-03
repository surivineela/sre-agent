import { CopilotProvider, CopilotTheme } from '@fluentui-copilot/react-copilot';
import { webDarkTheme, webLightTheme } from '@fluentui/react-components';
import { ReactNode } from 'react';
import { ThemeMode } from '../AzPortalProxy/Models/ITheme';

export const AgentSiteCopilotProvider = (props: { themeMode?: ThemeMode; children: ReactNode }) => {
    return (
        <CopilotProvider {...CopilotTheme} mode={'canvas'} theme={props.themeMode === ThemeMode.Dark ? webDarkTheme : webLightTheme}>
            {props.children}
        </CopilotProvider>
    );
};
