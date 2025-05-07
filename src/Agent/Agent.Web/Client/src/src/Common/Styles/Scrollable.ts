import { mergeStyleSets } from '@fluentui/react';
import { tokens } from '@fluentui/react-components';

export const useScrollableComponentStyles = () => {
    const supportStableScrollbar = CSS.supports('scrollbar-gutter', 'stable');

    return mergeStyleSets({
        scrollable: {
            scrollbarWidth: 'thin',
            scrollbarColor: `${tokens.colorScrollbarOverlay} transparent`,
            overflowX: 'hidden',
            overflowY: supportStableScrollbar ? 'hidden' : 'auto',
            scrollbarGutter: 'stable',
            selectors: {
                '&:hover': {
                    overflowY: 'auto',
                },
                '&::-webkit-scrollbar': {
                    width: '8px',
                },
                '&::-webkit-scrollbar-track': {
                    background: 'transparent',
                },
            },
        },
    });
};
