import { useEffect, useState } from 'react';

export interface WindowSize {
    width: number | undefined;
    height: number | undefined;
}

export const useWindowSize = () => {
    const [windowSize, setWindowSize] = useState<WindowSize>({
        width: undefined,
        height: undefined,
    });

    useEffect(() => {
        function onResize() {
            setWindowSize({
                width: window.innerWidth,
                height: window.innerHeight,
            });
        }

        onResize();

        window.addEventListener('resize', onResize);

        return () => window.removeEventListener('resize', onResize);
    }, []);
    return windowSize;
};

export default useWindowSize;
