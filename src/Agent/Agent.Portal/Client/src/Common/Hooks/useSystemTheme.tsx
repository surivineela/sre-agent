import { useEffect, useState } from 'react';

export type SystemTheme = 'dark' | 'light';

export const getSystemTheme = (): SystemTheme => {
    if (typeof window === 'undefined') return 'dark';
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
};

/**
 * Hook to detect and track the system's preferred color scheme
 * @returns The current system theme ('dark' or 'light')
 */
export const useSystemTheme = (): SystemTheme => {
    const [systemTheme, setSystemTheme] = useState<SystemTheme>(getSystemTheme);

    useEffect(() => {
        const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

        const handleChange = (e: MediaQueryListEvent) => {
            setSystemTheme(e.matches ? 'dark' : 'light');
        };

        mediaQuery.addEventListener('change', handleChange);
        return () => mediaQuery.removeEventListener('change', handleChange);
    }, []);

    return systemTheme;
};
