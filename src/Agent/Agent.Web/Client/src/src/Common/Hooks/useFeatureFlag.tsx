import { useEffect, useState } from 'react';
import { useLocation } from 'react-router';

export const useFeatureFlag = (flagName: string) => {
    const [enabled, setEnabled] = useState<boolean | null>(null);
    const location = useLocation();

    useEffect(() => {
        const query = new URLSearchParams(location.search);
        setEnabled(!!flagName && query.get(flagName.toLowerCase()) === 'true');
    }, [location.search, flagName]);

    return enabled;
};
