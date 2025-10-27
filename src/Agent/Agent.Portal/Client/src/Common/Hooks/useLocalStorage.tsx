import { useCallback, useEffect, useState } from 'react';
import { TelemetrySource } from '../Constants/Telemetry';
import { LogLevel } from '../Contracts/Telemetry';
import { logTelemetryEvent } from './useTelemetry';

const readFromStorage = <T,>(key: string, defaultValue: T, telemetrySource: TelemetrySource): T => {
    try {
        const item = localStorage.getItem(key);
        return item ? (JSON.parse(item) as T) : defaultValue;
    } catch (error) {
        logTelemetryEvent({
            action: 'read-from-storage',
            actionModifier: 'failed',
            logLevel: LogLevel.Warning,
            telemetrySource,
            additionalData: {
                key,
                error: error instanceof Error ? error.message : String(error),
            },
        });
        return defaultValue;
    }
};

/**
 * Generic hook for persisting state in localStorage with type safety.
 * Automatically handles JSON serialization/deserialization and syncs across tabs.
 *
 * @template T - The type of value to store
 * @param key - localStorage key
 * @param defaultValue - Default value if key doesn't exist
 * @param telemetrySource
 * @returns Object with value and setValue
 */
export const useLocalStorage = <T,>(key: string, defaultValue: T, telemetrySource: TelemetrySource) => {
    const [value, setValue] = useState<T>(defaultValue);

    useEffect(() => {
        setValue(readFromStorage(key, defaultValue, telemetrySource));
    }, [key, defaultValue, telemetrySource]);

    const setStoredValue = useCallback(
        (newValue: T | ((prev: T) => T)) => {
            try {
                const valueToStore = newValue instanceof Function ? newValue(value) : newValue;

                setValue(valueToStore);
                localStorage.setItem(key, JSON.stringify(valueToStore));

                window.dispatchEvent(
                    new StorageEvent('storage', {
                        key,
                        newValue: JSON.stringify(valueToStore),
                        storageArea: localStorage,
                    })
                );
            } catch (error) {
                logTelemetryEvent({
                    action: 'set-storage-value',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    telemetrySource,
                    additionalData: {
                        key,
                        error: error instanceof Error ? error.message : String(error),
                    },
                });
            }
        },
        [key, value, telemetrySource]
    );

    useEffect(() => {
        const handleStorageChange = (e: StorageEvent) => {
            if (e.key === key && e.storageArea === localStorage) {
                try {
                    const newValue = e.newValue ? (JSON.parse(e.newValue) as T) : defaultValue;
                    setValue(newValue);
                } catch (error) {
                    logTelemetryEvent({
                        action: 'parse-storage-event',
                        actionModifier: 'failed',
                        logLevel: LogLevel.Warning,
                        telemetrySource,
                        additionalData: {
                            key,
                            error: error instanceof Error ? error.message : String(error),
                        },
                    });
                }
            }
        };

        window.addEventListener('storage', handleStorageChange);
        return () => window.removeEventListener('storage', handleStorageChange);
    }, [key, defaultValue, telemetrySource]);

    return { value, setValue: setStoredValue };
};
