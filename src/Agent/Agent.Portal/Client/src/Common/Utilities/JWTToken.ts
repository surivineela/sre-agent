/**
 * Represents the decoded claims from a JWT token
 */
export interface DecodedToken {
    /** Token expiration time (Unix timestamp in seconds) */
    exp?: number;
    /** Token issued at time (Unix timestamp in seconds) */
    iat?: number;
    /** Token not before time (Unix timestamp in seconds) */
    nbf?: number;
    /** Issuer */
    iss?: string;
    /** Audience */
    aud?: string | string[];
    /** Subject (user identifier) */
    sub?: string;
    /** Tenant ID */
    tid?: string;
    /** Object ID (user's object ID in Azure AD) */
    oid?: string;
    /** User Principal Name */
    upn?: string;
    /** Unique name */
    unique_name?: string;
    /** Name */
    name?: string;
    /** Preferred username */
    preferred_username?: string;
    /** Email */
    email?: string;
    /** Roles */
    roles?: string[];
    /** Scopes */
    scp?: string;
    /** Application ID */
    appid?: string;
    /** Additional claims */
    [key: string]: any;
}

/**
 * Represents a parsed JWT token with easy access to common claims and utility methods.
 */
export class JWTToken {
    private readonly tokenString: string;
    private readonly claims: DecodedToken | null;

    constructor(tokenString: string) {
        this.tokenString = tokenString;
        this.claims = this.decode();
    }

    /**
     * Decode the JWT token and return the parsed claims.
     */
    private decode(): DecodedToken | null {
        try {
            // JWT structure: header.payload.signature
            const payload = this.tokenString.split('.')[1];
            if (!payload) {
                console.error('Invalid JWT token structure');
                return null;
            }

            // Base64 decode the payload
            const decodedPayload = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
            const claims = JSON.parse(decodedPayload);

            return claims as DecodedToken;
        } catch (error) {
            console.error('Failed to decode JWT token:', error);
            return null;
        }
    }

    /**
     * Get the raw token string.
     */
    get raw(): string {
        return this.tokenString;
    }

    /**
     * Get all decoded claims from the token.
     */
    get allClaims(): DecodedToken | null {
        return this.claims;
    }

    /**
     * Get the token expiration date.
     */
    get expiration(): Date | null {
        if (this.claims?.exp && typeof this.claims.exp === 'number') {
            return new Date(this.claims.exp * 1000); // Convert Unix timestamp to milliseconds
        }
        return null;
    }

    /**
     * Get the tenant ID from the token.
     */
    get tenantId(): string | null {
        return this.claims?.tid || null;
    }

    /**
     * Get the object ID (user's object ID in Azure AD) from the token.
     */
    get objectId(): string | null {
        return this.claims?.oid || null;
    }

    /**
     * Get the user's preferred username from the token.
     */
    get username(): string | null {
        return this.claims?.preferred_username || this.claims?.upn || this.claims?.unique_name || null;
    }

    /**
     * Get the user's name from the token.
     */
    get name(): string | null {
        return this.claims?.name || null;
    }

    /**
     * Get the user's email from the token.
     */
    get email(): string | null {
        return this.claims?.email || null;
    }

    /**
     * Check if the token is expired or will expire within a given buffer time.
     * @param bufferMs - Buffer time in milliseconds (default: 5 minutes)
     * @returns True if the token is expired or will expire within the buffer time
     */
    isExpired(bufferMs: number = 5 * 60 * 1000): boolean {
        const expiration = this.expiration;

        if (!expiration) {
            return true; // Treat tokens without expiration as expired
        }

        return expiration.getTime() - bufferMs <= Date.now();
    }

    /**
     * Check if the token was successfully decoded.
     */
    get isValid(): boolean {
        return this.claims !== null;
    }
}
