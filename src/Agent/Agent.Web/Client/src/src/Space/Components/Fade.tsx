import { createPresenceComponent, motionTokens } from '@fluentui/react-components';
import { memo } from 'react';

const Fade = createPresenceComponent({
    enter: {
        keyframes: [{ opacity: 0 }, { opacity: 1 }],
        duration: motionTokens.durationUltraSlow,
    },
    exit: {
        keyframes: [{ opacity: 1 }, { opacity: 0 }],
        duration: motionTokens.durationFaster,
    },
});

export default memo(Fade);
