import { useCallback } from 'react';
import { NavigateOptions, To, useLocation, useNavigate } from 'react-router-dom';

/**
 * Hook that provides a navigate function that automatically preserves query strings.
 * This ensures feature flags like ?sre_ux_local=true persist across navigation.
 *
 * @returns A navigate function with the same signature as useNavigate() from react-router-dom
 *
 * @example
 * const navigate = usePersistentNavigate();
 * // Current URL: /home?sre_ux_local=true
 * navigate('/agents/xyz');
 * // Navigates to: /agents/xyz?sre_ux_local=true
 */
export const usePersistentNavigate = () => {
    const navigate = useNavigate();
    const location = useLocation();

    const persistentNavigate = useCallback(
        (to: To | number, options?: NavigateOptions) => {
            // If navigating with a number (e.g., navigate(-1)), pass through directly
            if (typeof to === 'number') {
                navigate(to);
                return;
            }

            // If 'to' is a string, append the current query string
            if (typeof to === 'string') {
                const targetPath = location.search ? `${to}${location.search}` : to;
                navigate(targetPath, options);
                return;
            }

            // If 'to' is a Path object, merge the current search with any provided search
            const targetPath = {
                ...to,
                search: to.search || location.search,
            };
            navigate(targetPath, options);
        },
        [navigate, location.search]
    );

    return persistentNavigate;
};
