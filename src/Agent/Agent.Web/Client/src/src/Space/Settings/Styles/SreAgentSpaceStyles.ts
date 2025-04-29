import { mergeStyleSets } from '@fluentui/react';
import { CSSProperties } from 'react';

const lineIconStyle: CSSProperties = { transform: 'rotate(90deg)', marginRight: '5px', marginLeft: '5px', marginTop: '12px' };

const logsMenuItemContainer: CSSProperties = {
    display: 'flex',
    flexDirection: 'row',
    gap: '5px',
    alignItems: 'center',
};

export const useSreAgentSpaceStyles = () =>
    mergeStyleSets({
        lineIconStyle,
        logsMenuItemContainer,
    });
