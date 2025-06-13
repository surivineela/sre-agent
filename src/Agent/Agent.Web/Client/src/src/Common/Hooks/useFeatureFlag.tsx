import { useEffect, useState } from 'react';
import { useLocation } from 'react-router';

export const useFeatureFlag = (flagName: string) => {
    const [enabled, setEnabled] = useState<boolean | null>(null);

    const location = useLocation();

    useEffect(() => {
        const query = new URLSearchParams(location.search.toLowerCase() || window.location.search.toLowerCase());
        setEnabled(!!flagName && query.get(flagName.toLowerCase()) === 'true');
    }, [flagName, location.search]);

    return enabled;
};
