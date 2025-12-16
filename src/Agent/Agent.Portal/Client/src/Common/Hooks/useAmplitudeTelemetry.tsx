import { useCallback, useContext } from 'react';
import { TelemetrySource } from '../Constants/Telemetry';
import {
    AmplitudeControlEvent,
    AmplitudeEvent,
    AmplitudeNavigationEvent,
    AmplitudeOperationEvent,
    CompleteAmplitudeData,
    IncompleteAmplitudeData,
    LogType,
} from '../Contracts/Amplitude';
import { LogLevel } from '../Contracts/Telemetry';
import { getSanitizedLogData } from '../Utilities/Sanitization';
import { logTelemetryEvent } from './useTelemetry';
import { logTelemetryLogToConsole } from '../Utilities/Telemetry';
import { AmplitudeContext } from '../Contexts/AmplitudeContext';

/**
 * Core logging function for Amplitude events.
 * Can be used outside React components when context is not available.
 *
 * @param telemetrySource - The source identifier for this telemetry event
 * @param amplitudeEvent - The typed Amplitude event (Control, Navigation, or Operation)
 * @param logType - The category of the event
 * @param metadata - Resource metadata (productName, resourceId, etc.)
 * @param additionalMetadata - Optional extra data to include
 * @param errorInfo - Optional error information for Operation events
 */
export const logAmplitudeEvent = (
    telemetrySource: TelemetrySource,
    amplitudeEvent: AmplitudeEvent,
    logType: LogType,
    additionalData: IncompleteAmplitudeData
) => {
    const { targetType, targetAction, targetName } = amplitudeEvent;

    // Merge isInternal and isInternalTenant into metadata to avoid updating ingestion (at least for now)
    additionalData.metadata = {
        ...additionalData.metadata,
        isInternal: additionalData.isInternal,
        isInternalTenant: additionalData.isInternalTenant,
    };

    // Up-merge `resourceId` from `metadata` for browse/create scenarios
    if (additionalData.metadata.resourceId && typeof additionalData.metadata.resourceId === 'string') {
        additionalData.resourceId = additionalData.metadata.resourceId;
    }

    logTelemetryLogToConsole(telemetrySource, `${targetType} ${targetAction}`, LogLevel.Info, targetName, additionalData);

    const sanitizedAdditionalData = getSanitizedLogData(additionalData);

    const finalAmplitudeData: CompleteAmplitudeData = {
        ...amplitudeEvent,
        ...sanitizedAdditionalData,
        logType,
        loggedTime: new Date().toISOString(),
    };

    logTelemetryEvent({
        action: 'amplitude',
        actionModifier: logType,
        telemetrySource,
        additionalData: {
            ...finalAmplitudeData,
            targetName,
        },
    });
};

/**
 * Hook providing typed Amplitude telemetry methods.
 * Must be used within an AmplitudeContextProvider.
 *
 * @example
 * ```tsx
 * const { logControlEvent, logNavigationEvent, logOperationEvent } = useAmplitudeTelemetry();
 *
 * // Log a button click
 * logControlEvent({
 *     targetType: 'button',
 *     targetAction: 'clicked',
 *     targetName: 'createAgentButton',
 *     targetFriendlyName: 'Create Agent',
 *     valueObjectName: SpecialControlValue.SubmitForm,
 *     valueObjectFriendlyName: SpecialControlValue.SubmitForm,
 * });
 *
 * // Log navigation
 * logNavigationEvent({
 *     targetType: 'tab',
 *     targetAction: 'tabItem',
 *     targetName: 'settingsTab',
 *     targetFriendlyName: 'Settings',
 * });
 *
 * // Log an operation with error
 * logOperationEvent(
 *     {
 *         targetType: 'create',
 *         targetAction: 'failed',
 *         targetName: 'agentCreate',
 *         targetFriendlyName: 'Create Agent',
 *     },
 *     { message: 'Failed to create agent', error: response.error },
 *     { attemptNumber: 1 }
 * );
 * ```
 */
export const useAmplitudeTelemetry = () => {
    const { telemetrySource, amplitudeMetadata } = useContext(AmplitudeContext);

    /**
     * Log an operation event (backend operations like create, update, delete, load)
     */
    const logOperationEvent = useCallback(
        (event: AmplitudeOperationEvent, additionalMetadata?: Record<string, unknown>) => {
            const mergedAmplitudeData: IncompleteAmplitudeData = additionalMetadata ? { ...amplitudeMetadata, ...additionalMetadata } : amplitudeMetadata;
            logAmplitudeEvent(telemetrySource, event, 'Operation', mergedAmplitudeData);
        },
        [telemetrySource, amplitudeMetadata]
    );

    /**
     * Log a control event (user interactions with UI controls)
     */
    const logControlEvent = useCallback(
        (event: AmplitudeControlEvent, additionalMetadata?: Record<string, unknown>) => {
            const mergedAmplitudeData: IncompleteAmplitudeData = additionalMetadata ? { ...amplitudeMetadata, ...additionalMetadata } : amplitudeMetadata;
            logAmplitudeEvent(telemetrySource, event, 'Control', mergedAmplitudeData);
        },
        [telemetrySource, amplitudeMetadata]
    );

    /**
     * Log a navigation event (page/blade navigation)
     */
    const logNavigationEvent = useCallback(
        (event: AmplitudeNavigationEvent, additionalMetadata?: Record<string, unknown>) => {
            const mergedAmplitudeData: IncompleteAmplitudeData = additionalMetadata ? { ...amplitudeMetadata, ...additionalMetadata } : amplitudeMetadata;
            logAmplitudeEvent(telemetrySource, event, 'Navigation', mergedAmplitudeData);
        },
        [telemetrySource, amplitudeMetadata]
    );

    return { logOperationEvent, logControlEvent, logNavigationEvent };
};
