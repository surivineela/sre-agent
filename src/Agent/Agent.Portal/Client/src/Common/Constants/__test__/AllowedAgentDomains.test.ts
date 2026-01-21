import { describe, expect, it } from 'vitest';
import { ALLOWED_AGENT_DOMAIN_SUFFIXES, isAllowedAgentDomain } from '../../Constants/AllowedAgentDomains';

describe('AllowedAgentDomains', () => {
    describe('ALLOWED_AGENT_DOMAIN_SUFFIXES', () => {
        it('should contain expected domain suffixes', () => {
            expect(ALLOWED_AGENT_DOMAIN_SUFFIXES).toContain('.azuresre.ai');
            expect(ALLOWED_AGENT_DOMAIN_SUFFIXES).toContain('.sre.azure.com');
        });
    });

    describe('isAllowedAgentDomain', () => {
        describe('allowed domains', () => {
            it('should allow *.azuresre.ai domains', () => {
                expect(isAllowedAgentDomain('https://myagent.azuresre.ai')).toBe(true);
                expect(isAllowedAgentDomain('https://sub.myagent.azuresre.ai')).toBe(true);
                expect(isAllowedAgentDomain('https://test-agent.azuresre.ai/path')).toBe(true);
            });

            it('should allow *.sre.azure.com domains', () => {
                expect(isAllowedAgentDomain('https://myagent.sre.azure.com')).toBe(true);
                expect(isAllowedAgentDomain('https://sub.myagent.sre.azure.com')).toBe(true);
                expect(isAllowedAgentDomain('https://test-agent.sre.azure.com/path')).toBe(true);
            });

            it('should handle URL-encoded input', () => {
                const encoded = encodeURIComponent('https://myagent.azuresre.ai');
                expect(isAllowedAgentDomain(encoded)).toBe(true);
            });

            it('should be case-insensitive for domain matching', () => {
                expect(isAllowedAgentDomain('https://MyAgent.AZURESRE.AI')).toBe(true);
                expect(isAllowedAgentDomain('https://MyAgent.SRE.AZURE.COM')).toBe(true);
            });
        });

        describe('blocked domains (security tests)', () => {
            it('should block arbitrary external domains', () => {
                expect(isAllowedAgentDomain('https://malicious-site.com')).toBe(false);
                expect(isAllowedAgentDomain('https://attacker.evil.com')).toBe(false);
                expect(isAllowedAgentDomain('https://phishing.example.org')).toBe(false);
            });

            it('should block domains that look similar but are not allowed', () => {
                // Typosquatting attempts
                expect(isAllowedAgentDomain('https://azuresre.ai.evil.com')).toBe(false);
                expect(isAllowedAgentDomain('https://fake-azuresre.ai')).toBe(false);
                expect(isAllowedAgentDomain('https://sre.azure.com.evil.com')).toBe(false);
            });

            it('should block non-HTTPS URLs in production (except localhost)', () => {
                expect(isAllowedAgentDomain('http://myagent.azuresre.ai')).toBe(false);
                expect(isAllowedAgentDomain('ftp://myagent.azuresre.ai')).toBe(false);
            });

            it('should block bare domain without subdomain', () => {
                // The allowed suffixes start with a dot, so bare domains should not match
                expect(isAllowedAgentDomain('https://azuresre.ai')).toBe(false);
                expect(isAllowedAgentDomain('https://sre.azure.com')).toBe(false);
            });
        });

        describe('edge cases', () => {
            it('should return false for empty or null input', () => {
                expect(isAllowedAgentDomain('')).toBe(false);
                expect(isAllowedAgentDomain(null as unknown as string)).toBe(false);
                expect(isAllowedAgentDomain(undefined as unknown as string)).toBe(false);
            });

            it('should return false for invalid URLs', () => {
                expect(isAllowedAgentDomain('not-a-url')).toBe(false);
                expect(isAllowedAgentDomain('://missing-protocol.com')).toBe(false);
                expect(isAllowedAgentDomain('https://')).toBe(false);
            });

            it('should handle URLs with ports', () => {
                expect(isAllowedAgentDomain('https://myagent.azuresre.ai:443')).toBe(true);
                expect(isAllowedAgentDomain('https://myagent.azuresre.ai:8080')).toBe(true);
            });

            it('should handle URLs with paths and query strings', () => {
                expect(isAllowedAgentDomain('https://myagent.azuresre.ai/static/?trustedAuthority=test')).toBe(true);
                expect(isAllowedAgentDomain('https://myagent.azuresre.ai/path/to/resource?foo=bar#hash')).toBe(true);
            });
        });
    });
});
