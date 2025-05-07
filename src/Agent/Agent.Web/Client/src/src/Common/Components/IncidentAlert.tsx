import { FontWeights, Text, useTheme } from '@fluentui/react';
import { makeStyles, tokens } from '@fluentui/react-components';
import React, { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentAlertResources } from '../../Strings/SREAgentResources';
import './sre-dashboard.css';

export interface IncidentAlertProps {
    messageText: string;
}

interface AlertData {
    alertId: string;
    alertRule: string;
    description: string;
    monitoredResource?: string;
    severity: string;
    monitorCondition: string;
    monitorService?: string;
    firedAt: string;
    subscription?: string;
    resourceGroup?: string;
    portalUrl?: string;
}

const useIncidentAlertStyles = makeStyles({
    container: {
        backgroundColor: tokens.colorNeutralBackground1,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: '8px',
        boxShadow: '0 2px 4px rgba(0, 0, 0, 0.05)',
        marginBottom: '16px',
        overflow: 'hidden',
    },
    header: {
        backgroundColor: tokens.colorNeutralBackground3,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '12px 16px',
    },
    headerTitle: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        fontWeight: 600,
        fontSize: '16px',
    },
    headerIcon: {
        color: tokens.colorPaletteRedForeground1,
        marginRight: '8px',
    },
    badgeContainer: {
        display: 'flex',
        gap: '8px',
        alignItems: 'center',
    },
    body: {
        padding: '16px 24px',
    },
    detailsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: '16px',
        marginBottom: '20px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        paddingBottom: '16px',
    },
    detailItem: {
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    detailLabel: {
        fontWeight: 600,
        fontSize: '14px',
        color: tokens.colorNeutralForeground2,
    },
    detailValue: {
        fontSize: '14px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        color: tokens.colorNeutralForeground1,
        fontFamily: 'monospace',
        backgroundColor: tokens.colorNeutralBackground4,
        padding: '2px 6px',
        borderRadius: '3px',
        maxWidth: '100%',
    },
    section: {
        marginBottom: '16px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        paddingBottom: '16px',
    },
    viewDetailsButton: {
        backgroundColor: tokens.colorBrandBackground,
        color: tokens.colorNeutralBackground1,
        border: 'none',
        borderRadius: '4px',
        padding: '8px 16px',
        fontSize: '14px',
        fontWeight: 600,
        cursor: 'pointer',
        marginTop: '16px',
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        '&:hover': {
            backgroundColor: tokens.colorBrandBackgroundHover,
        },
    },
    infoGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(2, 1fr)',
        gap: '16px',
        marginBottom: '20px',
        paddingBottom: '16px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    linkIcon: {
        width: '16px',
        height: '16px',
        marginRight: '4px',
    },
});

/**
 * Severity can be Sev0, Sev1, Sev2, Sev3, Sev4
 */
const getSeverityClass = (severity: string): string => {
    const sev = severity.toLowerCase();
    if (sev === 'sev0') return 'badge-red';
    if (sev === 'sev1') return 'badge-orange';
    if (sev === 'sev2') return 'badge-yellow';
    if (sev === 'sev3') return 'badge-amber';
    return 'badge-green'; // Sev4 or unknown
};

/**
 * Monitor Condition can be Fired, Resolved
 */
const getConditionClass = (condition: string): string => {
    const cond = condition.toLowerCase();
    if (cond === 'fired') return 'badge-red';
    if (cond === 'resolved' || cond === 'closed') return 'badge-green';
    return 'badge-amber'; // Unknown condition
};

export const extractIncidentAlertData = (text: string): AlertData | null => {
    if (!text) return null;

    const incidentAlertRegex = /```incident-alert\s+([\s\S]*?)```/;
    const match = text.match(incidentAlertRegex);

    if (match && match[1]) {
        try {
            const alertData = JSON.parse(match[1]);
            return {
                alertId: alertData.alertId,
                alertRule: alertData.alertRule,
                description: alertData.description,
                monitoredResource: alertData.monitoredResource,
                severity: alertData.severity,
                monitorCondition: alertData.monitorCondition,
                monitorService: alertData.monitorService,
                firedAt: alertData.firedAt,
                subscription: alertData.subscription,
                resourceGroup: alertData.resourceGroup,
                portalUrl: alertData.portalUrl,
            };
        } catch (error) {
            console.error('Failed to parse incident alert data:', error);
            return null;
        }
    }

    return null;
};

const IncidentAlert: React.FC<IncidentAlertProps> = ({ messageText }) => {
    const alertData = useMemo(() => extractIncidentAlertData(messageText), [messageText]);
    const theme = useTheme();
    const styles = useIncidentAlertStyles();
    const intl = useIntl();

    if (!alertData) {
        return null;
    }

    const {
        alertId,
        alertRule,
        description,
        monitoredResource,
        severity,
        monitorCondition,
        monitorService,
        firedAt,
        subscription,
        resourceGroup,
        portalUrl,
    } = alertData;

    const handleViewDetails = () => {
        if (portalUrl) {
            window.open(portalUrl, '_blank');
        }
    };

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <div className={styles.headerTitle}>
                    <img src="./AzMonitorAlert.svg" alt="Az Monitor Alert" />
                    <Text
                        variant="mediumPlus"
                        styles={{
                            root: {
                                fontWeight: FontWeights.semibold,
                                color: theme.isInverted ? tokens.colorNeutralForeground1 : undefined,
                            },
                        }}
                    >
                        {intl.formatMessage(IncidentAlertResources.headerTitle)}
                    </Text>
                </div>
                <div className={styles.badgeContainer}>
                    <span className={`status-badge ${getSeverityClass(severity)}`}>{severity}</span>
                    <span className={`status-badge ${getConditionClass(monitorCondition)}`}>{monitorCondition}</span>
                </div>
            </div>
            <div className={styles.body}>
                <div className={styles.detailsGrid}>
                    <div className={styles.detailItem}>
                        <div className={styles.detailLabel}>{intl.formatMessage(IncidentAlertResources.alertID)}</div>
                        <div className={styles.detailValue} title={alertId}>
                            {alertId}
                        </div>
                    </div>
                    <div className={styles.detailItem}>
                        <div className={styles.detailLabel}>{intl.formatMessage(IncidentAlertResources.firedAt)}</div>
                        <div className={styles.detailValue}>{firedAt}</div>
                    </div>
                    {monitorService && (
                        <div className={styles.detailItem}>
                            <div className={styles.detailLabel}>{intl.formatMessage(IncidentAlertResources.monitorService)}</div>
                            <div className={styles.detailValue}>{monitorService}</div>
                        </div>
                    )}
                </div>

                <div className={styles.section}>
                    <div className={styles.detailLabel}>{intl.formatMessage(IncidentAlertResources.alertRule)}</div>
                    <div className={styles.detailValue}>{alertRule}</div>
                </div>

                {description && (
                    <div className={styles.section}>
                        <div className={styles.detailLabel}>{intl.formatMessage(IncidentAlertResources.description)}</div>
                        <div className={styles.detailValue}>{description}</div>
                    </div>
                )}

                {monitoredResource && (
                    <div className={styles.section}>
                        <div className={styles.detailLabel}>{intl.formatMessage(IncidentAlertResources.monitoredResource)}</div>
                        <div className={styles.detailValue}>{monitoredResource}</div>
                    </div>
                )}

                {(subscription || resourceGroup) && (
                    <div className={styles.infoGrid}>
                        {subscription && (
                            <div className={styles.detailItem}>
                                <div className={styles.detailLabel}>{intl.formatMessage(IncidentAlertResources.subscription)}</div>
                                <div className={styles.detailValue}>{subscription}</div>
                            </div>
                        )}
                        {resourceGroup && (
                            <div className={styles.detailItem}>
                                <div className={styles.detailLabel}>{intl.formatMessage(IncidentAlertResources.resourceGroup)}</div>
                                <div className={styles.detailValue}>{resourceGroup}</div>
                            </div>
                        )}
                    </div>
                )}

                {portalUrl && (
                    <button className={styles.viewDetailsButton} onClick={handleViewDetails}>
                        {intl.formatMessage(IncidentAlertResources.portalUrlLinkText)}
                    </button>
                )}
            </div>
        </div>
    );
};

export default IncidentAlert;
