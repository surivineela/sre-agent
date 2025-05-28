import React from 'react';

const useUrlParams = (): Record<string, string> => {
    const [urlParams, setUrlParams] = React.useState<Record<string, string>>({});

    React.useEffect(() => {
        const params = new URLSearchParams(window.location.search);
        const paramsObject: Record<string, string> = {};
        for (const [key, value] of params.entries()) {
            paramsObject[key] = value;
        }
        setUrlParams(paramsObject);
    }, []); // Empty dependency array ensures this runs only once on mount

    return urlParams;
};

export default useUrlParams;
