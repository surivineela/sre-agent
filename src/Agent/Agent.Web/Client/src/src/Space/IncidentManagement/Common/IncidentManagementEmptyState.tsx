import { Button, Link, makeStyles } from '@fluentui/react-components';
import { FC, useContext, useMemo } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { SreAgentFwLinks } from '../../../Common/Constants/FwLinks';
import { IncidentManagementResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';

const useStyles = makeStyles({
    emptyStateContainer: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100%',
        width: '100%',
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
    type: 'noPlatform' | 'noHandlers' | 'noAppInsights';
    onButtonClick: () => void;
}

export const IncidentManagementEmptyState: FC<IncidentManagementEmptyStateProps> = ({ type, onButtonClick }) => {
    const intl = useIntl();
    const styles = useStyles();
    const {
        incidentManagement: { incidentManagementConnectionState },
    } = useContext(SreAgentContext);

    const { imgSrc, imgAlt, title, description, learnMore, learnMoreLink, buttonText, disabled } = useMemo(() => {
        if (type === 'noPlatform') {
            return {
                imgSrc: './PlatformConnection.svg',
                imgAlt: intl.formatMessage(IncidentManagementResources.incidentPlatform),
                title: intl.formatMessage(IncidentManagementResources.platformEmptyStateTitle),
                description: intl.formatMessage(IncidentManagementResources.platformEmptyStateMessage),
                learnMore: intl.formatMessage(IncidentManagementResources.platformEmptyStateLearnMore),
                learnMoreLink: SreAgentFwLinks.learnMoreAboutIncidentManagement,
                buttonText: intl.formatMessage(IncidentManagementResources.platformEmptyStateButtonText),
                disabled: false,
            };
        }

        if (type === 'noAppInsights') {
            return {
                imgSrc: './ResponsePlan.svg',
                imgAlt: intl.formatMessage(IncidentManagementResources.configureApplicationInsights),
                title: intl.formatMessage(IncidentManagementResources.configureApplicationInsights),
                description: intl.formatMessage(IncidentManagementResources.configureApplicationInsightsDescription),
                learnMore: undefined,
                learnMoreLink: undefined,
                buttonText: intl.formatMessage(IncidentManagementResources.goToSettings),
                disabled: false,
            };
        }

        return {
            imgSrc: './ResponsePlan.svg',
            imgAlt: intl.formatMessage(IncidentManagementResources.handler),
            title: intl.formatMessage(IncidentManagementResources.handlersEmptyStateTitle),
            description: intl.formatMessage(IncidentManagementResources.handlersEmptyStateMessage),
            learnMore: intl.formatMessage(IncidentManagementResources.handlersEmptyStateLearnMore),
            learnMoreLink: SreAgentFwLinks.learnMoreAboutResponsePlans,
            buttonText: intl.formatMessage(IncidentManagementResources.handlersEmptyStateButtonText),
            disabled: incidentManagementConnectionState !== 'connected',
        };
    }, [intl, type, incidentManagementConnectionState]);

    return (
        <div className={styles.emptyStateContainer}>
            <div className={styles.emptyState}>
                <img src={imgSrc} alt={imgAlt} />
                <div className={styles.messageAndButton}>
                    <div className={styles.message}>
                        <div className={styles.messageTitle}>{title}</div>
                        <div className={styles.messageContent}>
                            {description}
                            {learnMore && learnMoreLink && (
                                <Link href={learnMoreLink} target="_blank" className={styles.learnMoreLink}>
                                    {learnMore}
                                </Link>
                            )}
                        </div>
                    </div>
                    <Button appearance="primary" onClick={onButtonClick} disabled={disabled}>
                        {buttonText}
                    </Button>
                </div>
            </div>
        </div>
    );
};
