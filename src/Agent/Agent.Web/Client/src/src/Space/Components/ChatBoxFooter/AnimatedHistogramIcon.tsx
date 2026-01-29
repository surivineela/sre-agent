import { createMotionComponent, makeStyles, tokens } from '@fluentui/react-components';
import { FC, memo } from 'react';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        alignItems: 'flex-end',
        justifyContent: 'center',
        width: '20px',
        height: '20px',
        gap: '2px',
        padding: '2px',
    },
    bar: {
        width: '3px',
        backgroundColor: tokens.colorBrandForeground1,
        borderRadius: '1px',
    },
});

const BAR_CONFIG: Array<[number, string]> = [
    [0, '8px'],
    [200, '10px'],
    [400, '6px'],
    [600, '12px'],
];

const createAnimatedBar = (delay: number, staticHeight: string) =>
    createMotionComponent({
        keyframes: [{ height: '4px' }, { height: '12px' }, { height: '4px' }],
        duration: 800,
        iterations: Infinity,
        easing: 'ease-in-out',
        delay,
        reducedMotion: {
            iterations: 1,
            fill: 'forwards',
            keyframes: [{ height: staticHeight }],
        },
    });

const AnimatedBars = BAR_CONFIG.map(([delay, staticHeight]) => createAnimatedBar(delay, staticHeight));

export const AnimatedHistogramIcon: FC = memo(() => {
    const styles = useStyles();

    return (
        <div className={styles.container}>
            {AnimatedBars.map((AnimatedBar, index) => (
                <AnimatedBar key={index}>
                    <div className={styles.bar} style={{ height: '4px' }} />
                </AnimatedBar>
            ))}
        </div>
    );
});
