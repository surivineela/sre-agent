import { Button, Image, makeStyles, MessageBar, MessageBarBody, Spinner, Text, tokens, Tooltip } from '@fluentui/react-components';
import { CheckmarkCircle20Filled, ChevronDown20Regular, ChevronUp20Regular, DismissCircle20Filled } from '@fluentui/react-icons';
import { useState } from 'react';
import { useIntl } from 'react-intl';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { useSubscriptions } from '../../../Common/Contexts/SubscriptionsContext';
import { useDeployment } from '../../../Common/Hooks/useDeployment';
import { parseArmId } from '../../../Common/Utilities/ArmId';
import { getDeploymentOperationErrorMessage } from '../../../Common/Utilities/Client';
import { getResourceTypeFriendlyName, resolveResourceIcon } from '../../../Common/Utilities/Resources';
import { DeployResources, PortalResources } from '../../../Strings/Resources';

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        padding: tokens.spacingVerticalL,
        height: '100%',
        overflow: 'hidden',
    },
    metadata: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        flexShrink: 0,
    },
    metadataRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
    },
    metadataLabel: {
        fontWeight: tokens.fontWeightSemibold,
        minWidth: '140px',
        color: tokens.colorNeutralForeground2,
    },
    metadataValue: {
        color: tokens.colorNeutralForeground1,
        wordBreak: 'break-word',
    },
    operationsContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        flexGrow: 1,
        minHeight: 0,
        overflow: 'hidden',
    },
    operationsTitle: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        flexShrink: 0,
    },
    operationsList: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        overflowY: 'auto',
        maxHeight: '275px',
    },
    operationItem: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
        padding: tokens.spacingVerticalS,
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    operationIcon: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minWidth: '20px',
    },
    operationContent: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        minWidth: 0,
    },
    operationHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    operationName: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    operationStatus: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
    },
    expandButton: {
        minWidth: 'auto',
        padding: '4px',
    },
    errorDetails: {
        marginTop: tokens.spacingVerticalS,
        padding: tokens.spacingVerticalS,
        backgroundColor: tokens.colorPaletteRedBackground1,
        borderRadius: tokens.borderRadiusSmall,
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorPaletteRedForeground1,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
    },
});

const getStatusIcon = (status: string) => {
    const normalizedStatus = status?.toLowerCase();

    if (normalizedStatus === 'succeeded') {
        return <CheckmarkCircle20Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />;
    }

    if (normalizedStatus === 'failed') {
        return <DismissCircle20Filled style={{ color: tokens.colorPaletteRedForeground1 }} />;
    }

    return <Spinner size="tiny" />;
};

const getStatusText = (status: string, intl: ReturnType<typeof useIntl>): string => {
    const normalizedStatus = status?.toLowerCase();

    switch (normalizedStatus) {
        case 'creating':
            return intl.formatMessage(DeployResources.creating);
        case 'succeeded':
            return intl.formatMessage(DeployResources.succeeded);
        case 'failed':
            return intl.formatMessage(DeployResources.failed);
        case 'updating':
            return intl.formatMessage(DeployResources.updating);
        case 'running':
            return intl.formatMessage(DeployResources.running);
        default:
            return status || intl.formatMessage(DeployResources.running);
    }
};

interface DeploymentTrackingProps {
    deploymentResourceId: string;
}

export const Deploy = (props: DeploymentTrackingProps) => {
    const { deploymentResourceId } = props;
    const styles = useStyles();
    const intl = useIntl();
    const { getSubscriptionById } = useSubscriptions();
    const [expandedErrors, setExpandedErrors] = useState<Set<number>>(new Set());

    const { correlationId, operations, isLoading, deploymentComplete, deploymentSucceeded, error } = useDeployment(
        deploymentResourceId,
        true,
        TelemetrySource.SreAgentCreate
    );

    // Parse deployment resource ID to extract deployment name, subscription, and resource group
    const deploymentParsed = parseArmId(deploymentResourceId);
    const deploymentName = deploymentParsed.resourceName || '';
    const subscriptionId = deploymentParsed.subscription || '';
    const resourceGroupName = deploymentParsed.resourceGroup || '';

    // Get subscription display name from subscriptions context
    const subscription = getSubscriptionById(subscriptionId);
    const subscriptionDisplayName = subscription?.displayName || subscriptionId;

    // Get start time from first operation timestamp, or use current time as fallback
    const startTime = operations.length > 0 && operations[0].timestamp ? operations[0].timestamp : new Date().toISOString();
    const formattedStartTime = new Date(startTime).toLocaleString();

    const toggleErrorExpansion = (index: number) => {
        setExpandedErrors(prev => {
            const newSet = new Set(prev);
            if (newSet.has(index)) {
                newSet.delete(index);
            } else {
                newSet.add(index);
            }
            return newSet;
        });
    };

    return (
        <div className={styles.root}>
            {!deploymentComplete && (
                <MessageBar intent="info">
                    <MessageBarBody>{intl.formatMessage(DeployResources.deploymentInProgress)}</MessageBarBody>
                </MessageBar>
            )}

            {deploymentComplete && deploymentSucceeded && (
                <MessageBar intent="success">
                    <MessageBarBody>{intl.formatMessage(DeployResources.deploymentSucceeded)}</MessageBarBody>
                </MessageBar>
            )}

            {deploymentComplete && !deploymentSucceeded && (
                <MessageBar intent="error">
                    <MessageBarBody>{intl.formatMessage(DeployResources.deploymentFailed)}</MessageBarBody>
                </MessageBar>
            )}

            {error && (
                <MessageBar intent="error">
                    <MessageBarBody>{error}</MessageBarBody>
                </MessageBar>
            )}

            <div className={styles.metadata}>
                <div className={styles.metadataRow}>
                    <Text className={styles.metadataLabel}>{intl.formatMessage(DeployResources.deploymentName)}:</Text>
                    <Text className={styles.metadataValue}>{deploymentName}</Text>
                </div>
                <div className={styles.metadataRow}>
                    <Text className={styles.metadataLabel}>{intl.formatMessage(PortalResources.subscription)}:</Text>
                    <Text className={styles.metadataValue}>{subscriptionDisplayName}</Text>
                </div>
                <div className={styles.metadataRow}>
                    <Text className={styles.metadataLabel}>{intl.formatMessage(PortalResources.resourceGroup)}:</Text>
                    <Text className={styles.metadataValue}>{resourceGroupName}</Text>
                </div>
                <div className={styles.metadataRow}>
                    <Text className={styles.metadataLabel}>{intl.formatMessage(DeployResources.startTime)}:</Text>
                    <Text className={styles.metadataValue}>{formattedStartTime}</Text>
                </div>
                <div className={styles.metadataRow}>
                    <Text className={styles.metadataLabel}>{intl.formatMessage(DeployResources.correlationId)}:</Text>
                    <Text className={styles.metadataValue}>{correlationId}</Text>
                </div>
            </div>

            <div className={styles.operationsContainer}>
                <Text className={styles.operationsTitle}>{intl.formatMessage(DeployResources.resourceOperations)}</Text>

                {isLoading && operations.length === 0 && (
                    <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM }}>
                        <Spinner size="tiny" />
                        <Text>{intl.formatMessage(DeployResources.loadingOperations)}</Text>
                    </div>
                )}

                {!isLoading && operations.length === 0 && (
                    <Text className={styles.operationStatus}>{intl.formatMessage(DeployResources.noOperations)}</Text>
                )}

                <div className={styles.operationsList}>
                    {operations.map((operation, index) => {
                        const isFailed = operation.status?.toLowerCase() === 'failed';
                        const errorMessage = isFailed ? getDeploymentOperationErrorMessage(operation.statusMessage) : null;
                        const isExpanded = expandedErrors.has(index);

                        return (
                            <div key={`${operation.resourceName}-${index}`} className={styles.operationItem}>
                                <div className={styles.operationIcon}>{getStatusIcon(operation.status)}</div>
                                <div className={styles.operationContent}>
                                    <Text className={styles.operationName}>{operation.resourceName}</Text>
                                    <div className={styles.operationHeader}>
                                        <Image
                                            src={resolveResourceIcon(operation.resourceType)}
                                            alt={getResourceTypeFriendlyName(operation.resourceType)}
                                            height={16}
                                            width={16}
                                        />
                                        <Text className={styles.operationStatus}>
                                            {getResourceTypeFriendlyName(operation.resourceType)} • {getStatusText(operation.status, intl)}
                                        </Text>
                                    </div>
                                    {isExpanded && errorMessage && <div className={styles.errorDetails}>{errorMessage}</div>}
                                </div>
                                {isFailed && errorMessage && (
                                    <Tooltip
                                        content={intl.formatMessage(isExpanded ? PortalResources.collapse : PortalResources.expand)}
                                        relationship="label"
                                    >
                                        <Button
                                            appearance="subtle"
                                            icon={isExpanded ? <ChevronUp20Regular /> : <ChevronDown20Regular />}
                                            onClick={() => toggleErrorExpansion(index)}
                                            className={styles.expandButton}
                                        />
                                    </Tooltip>
                                )}
                            </div>
                        );
                    })}
                </div>
            </div>
        </div>
    );
};
