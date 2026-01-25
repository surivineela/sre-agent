import { Image, makeStyles, Skeleton, SkeletonItem, Text, tokens } from '@fluentui/react-components';
import { CircleFilled } from '@fluentui/react-icons';
import { FC, memo, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';

const START_TYPING_MS = 50;
const TYPING_SPEED_MS = 80;
const CURSOR_BLINK_MS = 530;
const HIDE_CURSOR_MS = 2500;
const SLOW_LOAD_WARNING_MS = 30000;

const LoadingDots = memo(() => {
    const styles = `
        @keyframes jump {
            0%, 80%, 100% {
                transform: translateY(0);
            }
            40% {
                transform: translateY(-5px);
            }
        }`;

    const Dot = ({ delay = '0s' }: { delay?: string }) => (
        <CircleFilled
            style={{
                fontSize: '6px',
                animation: `jump 1.2s infinite ease-in-out ${delay}`,
                color: tokens.colorNeutralForeground3,
            }}
        />
    );

    return (
        <>
            <style>{styles}</style>
            <div style={{ display: 'flex', gap: tokens.spacingHorizontalXXS, marginTop: tokens.spacingVerticalS }}>
                <Dot />
                <Dot delay="0.2s" />
                <Dot delay="0.4s" />
            </div>
        </>
    );
});

LoadingDots.displayName = 'LoadingDots';

interface TerminalTypingTextProps {
    text: string;
    typingSpeedMs?: number;
    cursorBlinkMs?: number;
}

const TerminalTypingText: FC<TerminalTypingTextProps> = memo(
    ({ text, typingSpeedMs = TYPING_SPEED_MS, cursorBlinkMs = CURSOR_BLINK_MS }) => {
        const [displayedLength, setDisplayedLength] = useState(0);
        const [showCursor, setShowCursor] = useState(true);
        const [hasStarted, setHasStarted] = useState(false);
        const [cursorHidden, setCursorHidden] = useState(false);

        const prefersReducedMotion = useMemo(
            () => typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches,
            []
        );

        // Delay before starting the typing animation
        useEffect(() => {
            if (prefersReducedMotion) {
                setHasStarted(true);
                setDisplayedLength(text.length);
                return;
            }

            const startTimer = setTimeout(() => {
                setHasStarted(true);
            }, START_TYPING_MS);
            return () => clearTimeout(startTimer);
        }, [prefersReducedMotion, text.length]);

        // Typing animation effect
        useEffect(() => {
            if (!hasStarted || prefersReducedMotion) {
                return;
            }

            if (displayedLength < text.length) {
                const typingTimer = setTimeout(() => {
                    setDisplayedLength(prev => prev + 1);
                }, typingSpeedMs);
                return () => clearTimeout(typingTimer);
            }
        }, [hasStarted, displayedLength, text.length, typingSpeedMs, prefersReducedMotion]);

        // Cursor blink effect
        useEffect(() => {
            if (prefersReducedMotion || cursorHidden) {
                setShowCursor(false);
                return;
            }

            const blinkTimer = setInterval(() => {
                setShowCursor(prev => !prev);
            }, cursorBlinkMs);
            return () => clearInterval(blinkTimer);
        }, [cursorBlinkMs, prefersReducedMotion, cursorHidden]);

        const isComplete = displayedLength >= text.length;

        // Hide cursor 5 seconds after animation completes
        useEffect(() => {
            if (!isComplete || prefersReducedMotion) {
                return;
            }

            const hideCursorTimer = setTimeout(() => {
                setCursorHidden(true);
            }, HIDE_CURSOR_MS);
            return () => clearTimeout(hideCursorTimer);
        }, [isComplete, prefersReducedMotion]);

        const displayedText = text.slice(0, displayedLength);

        return (
            <span role="status" aria-live="polite">
                {/* Visually hidden text for screen readers - announces full text once complete */}
                <span
                    style={{
                        position: 'absolute',
                        width: '1px',
                        height: '1px',
                        padding: 0,
                        margin: '-1px',
                        overflow: 'hidden',
                        clip: 'rect(0, 0, 0, 0)',
                        whiteSpace: 'nowrap',
                        border: 0,
                    }}
                >
                    {isComplete ? text : ''}
                </span>
                {/* Visual typing animation - hidden from screen readers */}
                <Text aria-hidden="true" weight="bold" style={{ fontSize: tokens.fontSizeBase600 }} >
                    {displayedText}
                    <span
                        style={{
                            opacity: showCursor ? 1 : 0,
                            marginLeft: '1px',
                            borderRight: `2px solid ${tokens.colorNeutralForeground1}`,
                        }}
                    >
                        &nbsp;
                    </span>
                </Text>
            </span>
        );
    }
);

TerminalTypingText.displayName = 'TerminalTypingText';

const useStyles = makeStyles({
    wrapper: {
        display: 'flex',
        flexDirection: 'column',
        height: '95vh',
        backgroundColor: tokens.colorNeutralBackground3,
    },
    mainContent: {
        display: 'flex',
        flex: 1,
        overflow: 'hidden',
        padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalM} ${tokens.spacingVerticalM} 0`,
    },
    sidebar: {
        width: '280px',
        padding: `${tokens.spacingVerticalL} ${tokens.spacingHorizontalM}`,
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        maxHeight: '100%',
    },
    sidebarTopItems: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    chatArea: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusXLarge,
        padding: `${tokens.spacingVerticalL} ${tokens.spacingHorizontalL}`,
    },
    agentSection: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'flex-start',
        flexGrow: 1,
        gap: tokens.spacingVerticalM,
        paddingTop: 'calc(38.2vh - 90px)'
    },
    logoAndText: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    chatInput: {
        width: '100%',
        maxWidth: '1000px',
        minHeight: '20px',
        maxHeight: '240px',
        marginTop: tokens.spacingVerticalXL,
    },
    slowWarning: {
        marginTop: tokens.spacingVerticalM,
        textAlign: 'center',
        maxWidth: '400px',
        color: tokens.colorNeutralForeground3,
    },
});

export const MockShimmeredUx = () => {
    const intl = useIntl();
    const styles = useStyles();
    const [showSlowWarning, setShowSlowWarning] = useState(false);

    const agentText = useMemo(() => intl.formatMessage(PortalResources.azureSreAgent), [intl]);

    useEffect(() => {
        const timer = setTimeout(() => {
            setShowSlowWarning(true);
        }, SLOW_LOAD_WARNING_MS);

        return () => clearTimeout(timer);
    }, []);

    return (
        <div className={styles.wrapper}>
            <div className={styles.mainContent}>
                <div className={styles.sidebar}>
                    <div className={styles.sidebarTopItems}>
                        <Skeleton aria-label={intl.formatMessage(PortalResources.loading)}>
                            {Array.from({ length: 6 }).map((_, index) => (
                                <SkeletonItem
                                    key={index}
                                    size={40}
                                    style={{
                                        marginBottom: 20,
                                        borderRadius: tokens.borderRadiusMedium,
                                    }}
                                />
                            ))}
                        </Skeleton>
                    </div>
                    <Skeleton aria-label={intl.formatMessage(PortalResources.loading)}>
                        <SkeletonItem
                            size={40}
                            style={{
                                borderRadius: tokens.borderRadiusMedium,
                            }}
                        />
                    </Skeleton>
                </div>

                <div className={styles.chatArea}>
                    <div className={styles.agentSection}>
                        <div className={styles.logoAndText}>
                            <Image src="SreAgent.svg" width={32} height={32} alt={agentText} />
                            <TerminalTypingText text={agentText} />
                        </div>

                        <LoadingDots />

                        <div className={styles.chatInput}>
                            <Skeleton aria-label={intl.formatMessage(PortalResources.loading)}>
                                <SkeletonItem
                                    style={{
                                        maxWidth: '800px',
                                        margin: 'auto',
                                        height: '108px',
                                        borderRadius: '32px',
                                    }}
                                />
                            </Skeleton>
                        </div>

                        {showSlowWarning && (
                            <Text size={200} className={styles.slowWarning}>
                                {intl.formatMessage(PortalResources.agentLoadSlowWarning)}
                            </Text>
                        )}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default MockShimmeredUx;
