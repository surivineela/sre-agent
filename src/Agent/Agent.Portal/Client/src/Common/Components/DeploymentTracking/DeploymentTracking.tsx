import { Button, Image, MessageBar, MessageBarBody, Spinner, Text, Tooltip } from '@fluentui/react-components';
import { ChevronDown20Regular, ChevronUp20Regular } from '@fluentui/react-icons';
import { ReactNode, useState } from 'react';
import { useIntl } from 'react-intl';
import { DeployResources, PortalResources } from '../../../Strings/Resources';
import { TelemetrySource } from '../../Constants/Telemetry';
import { useSubscriptions } from '../../Contexts/SubscriptionsContext';
import { DeploymentOperation, useDeployment } from '../../Hooks/useDeployment';
import { parseArmId } from '../../Utilities/ArmId';
import { getDeploymentOperationErrorMessage } from '../../Utilities/Client';
import { getDeploymentStatusIcon, getDeploymentStatusText } from '../../Utilities/DeploymentStatus';
import { getResourceTypeFriendlyName, resolveResourceIcon } from '../../Utilities/Resources';
import { useDeploymentTrackingStyles } from './DeploymentTracking.styles';

export interface DeploymentTrackingProps {
    /** The ARM resource ID of the deployment to track */
    deploymentResourceId: string;
    /** Telemetry source for tracking */
    telemetrySource: TelemetrySource;
    /**
     * Optional: Custom render function for error details.
     * Allows consumers to add context-specific error messaging.
     * If not provided, defaults to showing just the error message.
     */
    renderErrorDetails?: (errorMessage: string, operation: DeploymentOperation) => ReactNode;
}

export const DeploymentTracking = ({ deploymentResourceId, telemetrySource, renderErrorDetails }: DeploymentTrackingProps) => {
    const styles = useDeploymentTrackingStyles();
    const intl = useIntl();
    const { getSubscriptionById } = useSubscriptions();
    const [expandedErrors, setExpandedErrors] = useState<Set<number>>(new Set());

    const { correlationId, operations, isLoading, deploymentComplete, deploymentSucceeded, error } = useDeployment(
        deploymentResourceId,
        true,
        telemetrySource
    );

    const deploymentParsed = parseArmId(deploymentResourceId);
    const deploymentName = deploymentParsed.resourceName || '';
    const subscriptionId = deploymentParsed.subscription || '';
    const resourceGroupName = deploymentParsed.resourceGroup || '';

    const subscription = getSubscriptionById(subscriptionId);
    const subscriptionDisplayName = subscription?.displayName || subscriptionId;

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
                    <div className={styles.loadingContainer}>
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
                                <div className={styles.operationIcon}>{getDeploymentStatusIcon(operation.status)}</div>
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
                                            {getResourceTypeFriendlyName(operation.resourceType)} •{' '}
                                            {getDeploymentStatusText(operation.status, intl)}
                                        </Text>
                                    </div>
                                    {isExpanded && errorMessage && (
                                        <div className={styles.errorDetails}>
                                            {renderErrorDetails ? renderErrorDetails(errorMessage, operation) : errorMessage}
                                        </div>
                                    )}
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
