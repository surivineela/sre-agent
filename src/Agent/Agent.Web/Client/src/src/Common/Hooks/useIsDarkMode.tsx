import { useContext } from 'react';
import { ThemeMode } from '../AzPortalProxy/Models/ITheme';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';

export const useIsDarkMode = () => {
    const { theme } = useContext(EnvironmentContext);

    return theme?.mode === ThemeMode.Dark;
};
