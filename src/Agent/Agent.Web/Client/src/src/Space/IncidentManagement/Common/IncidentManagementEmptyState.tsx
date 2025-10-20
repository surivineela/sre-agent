import { Button, Link, makeStyles } from '@fluentui/react-components';
import { FC, useMemo } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { SreAgentFwLinks } from '../../../Common/Constants/FwLinks';
import { IncidentManagementResources } from '../../../Strings/SREAgentResources';

const useStyles = makeStyles({
    emptyStateContainer: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        height: '485px',
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '16px',
        maxWidth: '480px',
    },
    messageAndButton: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '24px',
    },
    message: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '8px',
    },
    messageTitle: {
        fontWeight: 600,
        fontSize: '20px',
        lineHeight: '28px',
        textAlign: 'center',
    },
    messageContent: {
        fontSize: '14px',
        textAlign: 'center',
    },
    learnMoreLink: {
        marginLeft: '4px',
    },
});

interface IncidentManagementEmptyStateProps {
    type: 'noPlatform' | 'noHandlers';
    onButtonClick: () => void;
}

export const IncidentManagementEmptyState: FC<IncidentManagementEmptyStateProps> = ({ type, onButtonClick }) => {
    const intl = useIntl();
    const styles = useStyles();
    const { imgSrc, imgAlt, title, description, learnMore, learnMoreLink, buttonText } = useMemo(() => {
        return type === 'noPlatform'
            ? {
                  imgSrc: './PlatformConnection.svg',
                  imgAlt: intl.formatMessage(IncidentManagementResources.incidentPlatform),
                  title: intl.formatMessage(IncidentManagementResources.platformEmptyStateTitle),
                  description: intl.formatMessage(IncidentManagementResources.platformEmptyStateMessage),
                  learnMore: intl.formatMessage(IncidentManagementResources.platformEmptyStateLearnMore),
                  learnMoreLink: SreAgentFwLinks.learnMoreAboutIncidentManagement,
                  buttonText: intl.formatMessage(IncidentManagementResources.platformEmptyStateButtonText),
              }
            : {
                  imgSrc: './ResponsePlan.svg',
                  imgAlt: intl.formatMessage(IncidentManagementResources.handler),
                  title: intl.formatMessage(IncidentManagementResources.handlersEmptyStateTitle),
                  description: intl.formatMessage(IncidentManagementResources.handlersEmptyStateMessage),
                  learnMore: intl.formatMessage(IncidentManagementResources.handlersEmptyStateLearnMore),
                  learnMoreLink: SreAgentFwLinks.learnMoreAboutResponsePlans,
                  buttonText: intl.formatMessage(IncidentManagementResources.handlersEmptyStateButtonText),
              };
    }, [intl, type]);

    return (
        <div className={styles.emptyStateContainer}>
            <div className={styles.emptyState}>
                <img src={imgSrc} alt={imgAlt} />
                <div className={styles.messageAndButton}>
                    <div className={styles.message}>
                        <div className={styles.messageTitle}>{title}</div>
                        <div className={styles.messageContent}>
                            {description}
                            <Link href={learnMoreLink} target="_blank" className={styles.learnMoreLink}>
                                {learnMore}
                            </Link>
                        </div>
                    </div>
                    <Button appearance="primary" onClick={onButtonClick}>
                        {buttonText}
                    </Button>
                </div>
            </div>
        </div>
    );
};
