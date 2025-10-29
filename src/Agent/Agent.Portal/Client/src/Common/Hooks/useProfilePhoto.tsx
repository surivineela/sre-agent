import { useEffect, useMemo, useRef, useState } from 'react';
import { GraphClient } from '../Clients/GraphClient';
import { TelemetrySource } from '../Constants/Telemetry';
import { useAuth } from '../Contexts/AuthContext';
import { LogLevel } from '../Contracts/Telemetry';
import { useTelemetry } from './useTelemetry';

export const useProfilePhoto = (telemetrySource: TelemetrySource) => {
    const { isAuthenticated } = useAuth();
    const { logEvent } = useTelemetry(telemetrySource, undefined);

    const [photoUrl, setPhotoUrl] = useState<string>();
    const photoUrlRef = useRef<string>();

    const graphClient = useMemo(() => GraphClient.getInstance(telemetrySource), [telemetrySource]);

    useEffect(() => {
        return () => {
            if (photoUrlRef.current) {
                URL.revokeObjectURL(photoUrlRef.current);
                photoUrlRef.current = undefined;
            }
        };
    }, []);

    useEffect(() => {
        if (!isAuthenticated) {
            return;
        }

        const fetchPhoto = async () => {
            const response = await graphClient.getProfilePhoto();

            if (response.isSuccessful) {
                const url = response.content;
                if (url) {
                    if (photoUrlRef.current) {
                        URL.revokeObjectURL(photoUrlRef.current);
                    }
                    photoUrlRef.current = url;
                }
                setPhotoUrl(url);
            } else {
                logEvent({
                    action: 'fetch-profile-photo',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        error: response.error instanceof Error ? response.error.message : String(response.error),
                    },
                });
                setPhotoUrl(undefined);
            }
        };

        fetchPhoto();
    }, [graphClient, isAuthenticated, logEvent]);

    return { photoUrl };
};
