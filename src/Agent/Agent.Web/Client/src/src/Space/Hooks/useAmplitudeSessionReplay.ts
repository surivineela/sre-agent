import * as amplitude from '@amplitude/analytics-browser';
import { sessionReplayPlugin } from '@amplitude/plugin-session-replay-browser';
import { useEffect } from 'react';
import { Guid } from '../../Common/Helpers/Guid';
import { DefaultUserIdAndDisplayName, useAuthenticatedUserInfo } from './useAuthenticatedUserInfo';

// This value is ok to be exposed to the client side
const AmplitudeSessionReplayProjectId = 'bf6baf53dab3672fb4a208883b148067';

export const useAmplitudeSessionReplay = () => {
    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    useEffect(() => {
        let timeout: NodeJS.Timeout | undefined = undefined;
        // Create and Install Session Replay Plugin
        const sessionReplayTracking = sessionReplayPlugin();
        amplitude.add(sessionReplayTracking);

        timeout = setTimeout(() => {
            let userId = userIdAndDisplayName.userId;
            if (!userId) {
                userId = `${Guid.newGuid()}-defaultUser`; // Fallback to random GUID if userId is not available
            } else if (userId === DefaultUserIdAndDisplayName.userId) {
                userId = `${Guid.newGuid()}-webClientUser`;
            }

            amplitude.init(AmplitudeSessionReplayProjectId, {
                serverZone: 'EU',
                autocapture: true,
                userId: userId,
                deviceId: userId,
                sessionId: Date.now(),
            });
            // Delay initialization to prevent unnecessary session recorded due to extra re-renders
        }, 1500);

        return () => {
            clearTimeout(timeout);
        };
    }, [userIdAndDisplayName]);
};
