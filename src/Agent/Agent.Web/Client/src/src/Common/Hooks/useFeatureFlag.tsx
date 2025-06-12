import { useEffect, useState } from 'react';

export const useFeatureFlag = (flagName: string) => {
    const [enabled, setEnabled] = useState<boolean | null>(null);

    useEffect(() => {
        const query = new URLSearchParams(window.location.search.toLowerCase());
        setEnabled(!!flagName && query.get(flagName.toLowerCase()) === 'true');
    }, [flagName]);

    return enabled;
};
