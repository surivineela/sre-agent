import { newGuid } from './Guid';

class SessionManager {
    private static sessionId: string | null = null;

    public static getSessionId(): string {
        if (!this.sessionId) {
            this.sessionId = newGuid();
        }

        return this.sessionId;
    }
}

export const getSessionId = (): string => SessionManager.getSessionId();
