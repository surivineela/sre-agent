import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';
import { DeploymentClient } from '../Clients/DeploymentClient';
import { TelemetrySource } from '../Constants/Telemetry';
import { ArmDeploymentOperation, ProvisioningState } from '../Contracts/Deployment';
import { useTelemetry } from './useTelemetry';

export interface DeploymentOperation {
    resourceName: string;
    resourceType: string;
    status: ProvisioningState;
    statusMessage?: string;
    timestamp?: string;
}

export interface DeploymentState {
    correlationId: string;
    operations: DeploymentOperation[];
    isLoading: boolean;
    deploymentComplete: boolean;
    deploymentSucceeded: boolean;
    error?: string;
}

export const useDeployment = (deploymentResourceId: string, enabled: boolean, telemetrySource: TelemetrySource) => {
    const intl = useIntl();
    const { logEvent } = useTelemetry(telemetrySource, deploymentResourceId);

    const [state, setState] = useState<DeploymentState>({
        correlationId: '',
        operations: [],
        isLoading: true,
        deploymentComplete: false,
        deploymentSucceeded: false,
    });

    const [failureCount, setFailureCount] = useState(0);

    const deploymentClient = useMemo(() => DeploymentClient.getInstance(telemetrySource), [telemetrySource]);

    const fetchOperations = useCallback(async () => {
        if (!deploymentResourceId || !enabled) {
            return;
        }

        const [deploymentResponse, operationsResponse] = await Promise.all([
            deploymentClient.getDeployment(deploymentResourceId),
            deploymentClient.getDeploymentOperations(deploymentResourceId),
        ]);

        // Check if we can get the deployment details first
        if (!deploymentResponse.isSuccessful) {
            setFailureCount(prev => prev + 1);

            // Only show error after 3 failed attempts to allow deployment to initialize
            if (failureCount >= 2) {
                logEvent({
                    action: 'Deployment tracking error',
                    actionModifier: 'failed',
                    additionalData: {
                        deploymentResourceId,
                        error: deploymentResponse.error,
                        failureCount: failureCount + 1,
                    },
                });

                setState(prev => ({
                    ...prev,
                    isLoading: false,
                    error: intl.formatMessage(PortalResources.requestError),
                }));
            }
            return;
        }

        // Success - reset failure count
        setFailureCount(0);

        // Deployment exists, now process operations (may not be available yet)
        const operations: DeploymentOperation[] =
            operationsResponse.isSuccessful && operationsResponse.content?.value
                ? operationsResponse.content.value
                      .filter((op: ArmDeploymentOperation) => op.properties?.targetResource)
                      .map((op: ArmDeploymentOperation) => ({
                          resourceName: op.properties.targetResource?.resourceName || 'Unknown',
                          resourceType: op.properties.targetResource?.resourceType || 'Unknown',
                          status: op.properties.provisioningState as ProvisioningState,
                          statusMessage: op.properties.statusMessage,
                          timestamp: op.properties.timestamp,
                      }))
                : [];

        const deploymentProvisioningState = deploymentResponse?.content?.properties?.provisioningState;
        const isComplete =
            deploymentProvisioningState === 'Succeeded' ||
            deploymentProvisioningState === 'Failed' ||
            deploymentProvisioningState === 'Canceled';
        const isSuccess = deploymentProvisioningState === 'Succeeded';

        setState({
            correlationId: deploymentResponse?.content?.properties?.correlationId || '',
            operations,
            isLoading: false,
            deploymentComplete: isComplete,
            deploymentSucceeded: isSuccess,
        });

        if (isComplete) {
            logEvent({
                action: 'Deployment tracking complete',
                actionModifier: isSuccess ? 'success' : 'failed',
                additionalData: {
                    deploymentResourceId,
                    deploymentProvisioningState,
                    operationCount: operations.length,
                },
            });
        }
    }, [intl, deploymentResourceId, enabled, logEvent, deploymentClient, failureCount]);

    // Fetch and poll deployment ops until complete
    useEffect(() => {
        if (!enabled || !deploymentResourceId) {
            return;
        }

        fetchOperations();

        const intervalId = setInterval(() => {
            if (!state.deploymentComplete) {
                fetchOperations();
            }
        }, 5000);

        return () => clearInterval(intervalId);
    }, [enabled, deploymentResourceId, state.deploymentComplete, fetchOperations]);

    return state;
};
