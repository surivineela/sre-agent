import { Button, makeStyles, tokens } from '@fluentui/react-components';
import { Add24Regular, Bot24Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';

const useEmptyStateStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        height: '100%',
        padding: '48px',
        textAlign: 'center',
    },
    illustration: {
        marginBottom: '32px',
        opacity: 0.6,
    },
    icon: {
        fontSize: '120px',
        color: tokens.colorBrandForeground1,
    },
    title: {
        fontSize: tokens.fontSizeHero800,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        marginBottom: '12px',
    },
    description: {
        fontSize: tokens.fontSizeBase400,
        color: tokens.colorNeutralForeground3,
        maxWidth: '500px',
        lineHeight: '1.6',
        marginBottom: '32px',
    },
    actions: {
        display: 'flex',
        gap: '12px',
    },
    featureList: {
        display: 'flex',
        gap: '24px',
        marginTop: '48px',
        flexWrap: 'wrap',
        justifyContent: 'center',
    },
    feature: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '8px',
        maxWidth: '200px',
    },
    featureIcon: {
        fontSize: '32px',
        color: tokens.colorBrandForeground1,
        marginBottom: '4px',
    },
    featureTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    featureDescription: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        textAlign: 'center',
    },
});

interface ExtendedAgentEmptyStateProps {
    onCreateClick: () => void;
}

export const ExtendedAgentEmptyState: FC<ExtendedAgentEmptyStateProps> = ({ onCreateClick }) => {
    const styles = useEmptyStateStyles();
    const intl = useIntl();

    return (
        <div className={styles.container}>
            <div className={styles.illustration}>
                <Bot24Regular className={styles.icon} />
            </div>

            <h2 className={styles.title}>{intl.formatMessage(ExtendedAgentsGraphResources.buildYourAgentEcosystem)}</h2>

            <p className={styles.description}>{intl.formatMessage(ExtendedAgentsGraphResources.emptyStateDescription)}</p>

            <div className={styles.actions}>
                <Button appearance="primary" size="large" icon={<Add24Regular />} onClick={onCreateClick}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.createYourFirstEntity)}
                </Button>
            </div>

            <div className={styles.featureList}>
                <div className={styles.feature}>
                    <Bot24Regular className={styles.featureIcon} />
                    <div className={styles.featureTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.aiAgents)}</div>
                    <div className={styles.featureDescription}>{intl.formatMessage(ExtendedAgentsGraphResources.aiAgentsFeature)}</div>
                </div>

                <div className={styles.feature}>
                    <span className={styles.featureIcon}>🔧</span>
                    <div className={styles.featureTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.tools)}</div>
                    <div className={styles.featureDescription}>{intl.formatMessage(ExtendedAgentsGraphResources.toolsFeature)}</div>
                </div>

                <div className={styles.feature}>
                    <span className={styles.featureIcon}>🔌</span>
                    <div className={styles.featureTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.connector)}</div>
                    <div className={styles.featureDescription}>{intl.formatMessage(ExtendedAgentsGraphResources.connectorsFeature)}</div>
                </div>
            </div>
        </div>
    );
};
