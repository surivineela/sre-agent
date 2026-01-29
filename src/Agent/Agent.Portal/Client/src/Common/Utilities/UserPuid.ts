/**
 * Utility for extracting the user's PUID (Passport Unique ID) from ID token claims.
 *
 * The PUID is a legacy hexadecimal identifier that uniquely identifies a user across tenants.
 * It can be extracted from MSAL ID token claims using the algorithm from Azure Portal.
 *
 * Privacy Note: PUID enables cross-tenant user tracking. Use with consideration for privacy implications.
 *
 * @see https://microsoft.sharepoint.com/teams/azureid/Specs/Cross-org/Using%20PUIDs%20for%20Azure.docx
 */

// Prefixes used in the altsecid claim for cross-tenant scenarios
const LIVE_ID_PREFIX = 'live.com:';
const ORG_ID_PREFIX = 'urn:cid:';

export interface IdTokenClaimsWithPuid {
    /** Identity provider - if different from issuer, this is a cross-tenant scenario */
    idp?: string;
    /** Issuer */
    iss?: string;
    /** PUID claim - available in single-tenant scenarios */
    puid?: string;
    /** Alternative Security ID - contains PUID in cross-tenant scenarios */
    altsecid?: string;
}

export interface PuidResult {
    /** The extracted PUID (16 hex characters) or empty string if not found */
    puid: string;
    /** Whether this is an organizational (work/school) account vs personal Microsoft account */
    isOrgId: boolean;
    /** Whether the user is a foreign principal (guest/cross-tenant) */
    isForeignPrincipal: boolean;
}

/**
 * Extracts the PUID from ID token claims using the Azure Portal algorithm.
 *
 * - Single tenant case (idp missing or equals iss): Use the "puid" claim directly
 * - Cross tenant case (idp !== iss): Parse the "altsecid" claim which contains the PUID with a prefix
 *
 * @param idTokenClaims - The ID token claims from MSAL's activeAccount
 * @returns PuidResult with the extracted PUID and metadata
 */
export const extractPuidFromIdTokenClaims = (idTokenClaims: IdTokenClaimsWithPuid | undefined): PuidResult => {
    const defaultResult: PuidResult = {
        puid: '',
        isOrgId: false,
        isForeignPrincipal: false,
    };

    if (!idTokenClaims) {
        return defaultResult;
    }

    const { idp, iss, puid, altsecid } = idTokenClaims;

    // Single tenant case: idp is missing or equals iss
    if (!idp || idp === iss) {
        return {
            puid: puid || '',
            isOrgId: !!puid,
            isForeignPrincipal: false,
        };
    }

    // Cross tenant case: Parse the altsecid claim
    if (altsecid) {
        const altSecIdLower = altsecid.toLowerCase();

        if (altSecIdLower.startsWith(LIVE_ID_PREFIX)) {
            // Personal Microsoft account (live.com)
            return {
                puid: altsecid.substring(LIVE_ID_PREFIX.length),
                isOrgId: false,
                isForeignPrincipal: true,
            };
        }

        if (altSecIdLower.startsWith(ORG_ID_PREFIX)) {
            // Organizational account (work/school)
            return {
                puid: altsecid.substring(ORG_ID_PREFIX.length),
                isOrgId: true,
                isForeignPrincipal: true,
            };
        }

        // Unknown prefix - return default result gracefully
    }

    return defaultResult;
};

/**
 * Simple helper to get just the PUID string from ID token claims.
 *
 * @param idTokenClaims - The ID token claims from MSAL's activeAccount
 * @returns The PUID string or undefined if not available
 */
export const getPuidFromIdTokenClaims = (idTokenClaims: IdTokenClaimsWithPuid | undefined): string | undefined => {
    const result = extractPuidFromIdTokenClaims(idTokenClaims);
    return result.puid || undefined;
};
