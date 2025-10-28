import { Image, makeStyles, shorthands, Skeleton, SkeletonItem, tokens } from '@fluentui/react-components';
import { CircleFilled } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';

/** Shamelessly stolen from Paas extension which originally stole from sreagent-runtime repo */
const AgentMessageLoadingComponent = () => {
    const styles = `
        @keyframes jump {
            0%, 80%, 100% {
                transform: translateY(0);
            }
            40% {
                transform: translateY(-5px);
            }
        };`;

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
            <div style={{ display: 'flex', gap: tokens.spacingHorizontalXXS }}>
                <Dot />
                <Dot delay="0.2s" />
                <Dot delay="0.4s" />
            </div>
        </>
    );
};

const useStyles = makeStyles({
    wrapper: {
        display: 'flex',
        flexDirection: 'column',
        height: '95vh',
        backgroundColor: tokens.colorNeutralBackground1,
    },
    topTabs: {
        display: 'flex',
        justifyContent: 'space-between',
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalL),
    },
    mainContent: {
        display: 'flex',
        flex: 1,
        overflow: 'hidden',
    },
    sidebar: {
        width: '280px',
        ...shorthands.padding(tokens.spacingVerticalL, tokens.spacingHorizontalM),
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        maxHeight: '100%',
    },
    chatArea: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.padding(tokens.spacingVerticalL, tokens.spacingHorizontalL),
    },
    agentSection: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        flexGrow: 1,
        position: 'relative',
    },
    agentBackground: {
        position: 'absolute',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        borderRadius: tokens.borderRadiusLarge,
        display: 'flex',
    },
    agentContent: {
        position: 'relative',
        zIndex: 1,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalM,
    },
    chatInput: {
        marginTop: tokens.spacingVerticalM,
    },
});

export const MockShimmeredUx = () => {
    const intl = useIntl();
    const styles = useStyles();

    return (
        <div className={styles.wrapper}>
            <div className={styles.topTabs}>
                <div style={{ display: 'flex', gap: tokens.spacingHorizontalM }}>
                    <Skeleton>
                        <SkeletonItem size={32} style={{ width: 80 }} />
                    </Skeleton>
                    <Skeleton>
                        <SkeletonItem size={32} style={{ width: 80 }} />
                    </Skeleton>
                    <Skeleton>
                        <SkeletonItem size={32} style={{ width: 80 }} />
                    </Skeleton>
                </div>

                <Skeleton>
                    <SkeletonItem size={32} style={{ width: 120 }} />
                </Skeleton>
            </div>

            <div className={styles.mainContent}>
                <div className={styles.sidebar}>
                    <Skeleton>
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

                <div className={styles.chatArea}>
                    <div className={styles.agentSection}>
                        <div className={styles.agentBackground}>
                            <Skeleton style={{ width: '100%', height: '100%' }}>
                                <SkeletonItem style={{ width: '100%', height: '100%' }} />
                            </Skeleton>
                        </div>

                        <div className={styles.agentContent}>
                            <Image src='SreAgent.svg' width={32} height={32} alt={intl.formatMessage(PortalResources.azureSreAgent)} />
                            <AgentMessageLoadingComponent />
                        </div>
                    </div>

                    <div className={styles.chatInput}>
                        <Skeleton>
                            <SkeletonItem
                                size={72}
                                style={{
                                    borderRadius: tokens.borderRadiusLarge,
                                }}
                            />
                        </Skeleton>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default MockShimmeredUx;
