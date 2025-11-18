import { mergeStyleSets } from '@fluentui/react';
import { tokens } from '@fluentui/react-components';
import { CSSProperties } from 'react';

const lineIconStyle: CSSProperties = { transform: 'rotate(90deg)', marginRight: '5px', marginLeft: '5px', marginTop: '12px' };

const logsMenuItemContainer: CSSProperties = {
    display: 'flex',
    flexDirection: 'row',
    gap: '5px',
    alignItems: 'center',
};

const stoppedAgentComponentContainer: CSSProperties = {
    height: 'calc(100vh - 44px)',
    width: '100%',
};

const stoppedAgentComponentFlexBox: CSSProperties = {
    display: 'flex',
    flexDirection: 'column',
    justifyContent: 'center',
    alignItems: 'center',
    gap: tokens.spacingVerticalM,
    width: '100%',
    height: '100%',
};

export const useSreAgentSpaceStyles = () =>
    mergeStyleSets({
        lineIconStyle,
        logsMenuItemContainer,
        stoppedAgentComponentContainer,
        stoppedAgentComponentFlexBox,
    });
