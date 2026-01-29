/**
 * Allowed domain suffixes for external agent URLs.
 * Only agents hosted on these domains are permitted to be loaded in iframes.
 *
 * SECURITY: This allowlist prevents iframe injection attacks where an attacker
 * could craft a malicious URL to exfiltrate tokens and user data via postMessage.
 */
export const ALLOWED_AGENT_DOMAIN_SUFFIXES = ['.azuresre.ai', '.sre.azure.com'];

/**
 * Validates whether a URL is from an allowed agent domain.
 * @param url - The URL to validate (can be encoded or decoded)
 * @returns `true` if the URL's hostname ends with an allowed domain suffix
 */
export const isAllowedAgentDomain = (url: string): boolean => {
    if (!url) {
        return false;
    }

    try {
        // Handle URL-encoded input
        const decodedUrl = decodeURIComponent(url);
        const parsedUrl = new URL(decodedUrl);

        // Only allow HTTPS in production (allow HTTP for localhost dev)
        const isLocalhost = parsedUrl.hostname === 'localhost' || parsedUrl.hostname === '127.0.0.1';
        if (!isLocalhost && parsedUrl.protocol !== 'https:') {
            return false;
        }

        // Check if hostname ends with any allowed suffix
        const hostname = parsedUrl.hostname.toLowerCase();
        return ALLOWED_AGENT_DOMAIN_SUFFIXES.some(suffix => hostname.endsWith(suffix.toLowerCase()));
    } catch {
        // Invalid URL
        return false;
    }
};
