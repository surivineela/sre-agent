/**
 * Hook to generate asset URLs with the correct base path for versioned deployments.
 * @param path - The relative path to the asset (e.g., 'SreAgent.svg')
 * @returns The full URL to the asset including the base path
 */
export const useAssetUrl = (path: string): string => {
    return `${import.meta.env.BASE_URL}${path}`;
};
