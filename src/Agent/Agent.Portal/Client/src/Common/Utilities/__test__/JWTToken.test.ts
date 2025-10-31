import { describe, expect, it, vi } from 'vitest';
import { JWTToken } from '../JWTToken';

describe('JWTToken', () => {
    // Helper function to create a JWT token with specific claims
    const createTestToken = (claims: any): string => {
        const header = { alg: 'RS256', typ: 'JWT' };
        const encodedHeader = btoa(JSON.stringify(header));
        const encodedPayload = btoa(JSON.stringify(claims));
        const signature = 'test-signature';
        return `${encodedHeader}.${encodedPayload}.${signature}`;
    };

    describe('constructor and basic properties', () => {
        it('should decode a valid JWT token', () => {
            const claims = {
                exp: Math.floor(Date.now() / 1000) + 3600,
                tid: 'test-tenant-id',
                oid: 'test-object-id',
                name: 'Test User',
            };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.isValid).toBe(true);
            expect(token.raw).toBe(tokenString);
        });

        it('should handle invalid JWT token structure', () => {
            const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
            const token = new JWTToken('invalid-token');

            expect(token.isValid).toBe(false);
            expect(token.expiration).toBeNull();
            expect(token.tenantId).toBeNull();
            expect(consoleErrorSpy).toHaveBeenCalled();

            consoleErrorSpy.mockRestore();
        });

        it('should handle token with missing payload', () => {
            const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
            const token = new JWTToken('header..signature');

            expect(token.isValid).toBe(false);
            expect(consoleErrorSpy).toHaveBeenCalled();

            consoleErrorSpy.mockRestore();
        });

        it('should handle token with invalid JSON in payload', () => {
            const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
            const invalidPayload = btoa('not-valid-json{');
            const token = new JWTToken(`header.${invalidPayload}.signature`);

            expect(token.isValid).toBe(false);
            expect(consoleErrorSpy).toHaveBeenCalled();

            consoleErrorSpy.mockRestore();
        });
    });

    describe('expiration property', () => {
        it('should return correct expiration date', () => {
            const expirationTimestamp = Math.floor(Date.now() / 1000) + 3600; // 1 hour from now
            const claims = { exp: expirationTimestamp };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.expiration).toBeInstanceOf(Date);
            expect(token.expiration?.getTime()).toBe(expirationTimestamp * 1000);
        });

        it('should return null when exp claim is missing', () => {
            const claims = { tid: 'test-tenant' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.expiration).toBeNull();
        });

        it('should return null when exp claim is not a number', () => {
            const claims = { exp: 'not-a-number' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.expiration).toBeNull();
        });
    });

    describe('tenantId property', () => {
        it('should return tenant ID from tid claim', () => {
            const claims = { tid: 'test-tenant-id-123' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.tenantId).toBe('test-tenant-id-123');
        });

        it('should return null when tid claim is missing', () => {
            const claims = { oid: 'test-object-id' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.tenantId).toBeNull();
        });
    });

    describe('objectId property', () => {
        it('should return object ID from oid claim', () => {
            const claims = { oid: 'test-object-id-456' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.objectId).toBe('test-object-id-456');
        });

        it('should return null when oid claim is missing', () => {
            const claims = { tid: 'test-tenant-id' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.objectId).toBeNull();
        });
    });

    describe('username property', () => {
        it('should return preferred_username when available', () => {
            const claims = {
                preferred_username: 'user@example.com',
                upn: 'other@example.com',
                unique_name: 'another@example.com',
            };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.username).toBe('user@example.com');
        });

        it('should fall back to upn when preferred_username is missing', () => {
            const claims = {
                upn: 'user@example.com',
                unique_name: 'another@example.com',
            };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.username).toBe('user@example.com');
        });

        it('should fall back to unique_name when both preferred_username and upn are missing', () => {
            const claims = { unique_name: 'user@example.com' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.username).toBe('user@example.com');
        });

        it('should return null when no username claims are present', () => {
            const claims = { tid: 'test-tenant' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.username).toBeNull();
        });
    });

    describe('name property', () => {
        it('should return name from name claim', () => {
            const claims = { name: 'John Doe' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.name).toBe('John Doe');
        });

        it('should return null when name claim is missing', () => {
            const claims = { tid: 'test-tenant' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.name).toBeNull();
        });
    });

    describe('email property', () => {
        it('should return email from email claim', () => {
            const claims = { email: 'user@example.com' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.email).toBe('user@example.com');
        });

        it('should return null when email claim is missing', () => {
            const claims = { name: 'John Doe' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.email).toBeNull();
        });
    });

    describe('allClaims property', () => {
        it('should return all decoded claims', () => {
            const claims = {
                exp: Math.floor(Date.now() / 1000) + 3600,
                tid: 'test-tenant-id',
                oid: 'test-object-id',
                name: 'Test User',
                email: 'test@example.com',
                roles: ['Admin', 'User'],
            };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.allClaims).toEqual(claims);
        });

        it('should return null for invalid token', () => {
            const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
            const token = new JWTToken('invalid-token');

            expect(token.allClaims).toBeNull();

            consoleErrorSpy.mockRestore();
        });
    });

    describe('isExpired method', () => {
        it('should return false for token with future expiration', () => {
            const expirationTimestamp = Math.floor(Date.now() / 1000) + 3600; // 1 hour from now
            const claims = { exp: expirationTimestamp };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.isExpired()).toBe(false);
        });

        it('should return true for expired token', () => {
            const expirationTimestamp = Math.floor(Date.now() / 1000) - 3600; // 1 hour ago
            const claims = { exp: expirationTimestamp };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.isExpired()).toBe(true);
        });

        it('should return true for token expiring within buffer time', () => {
            const expirationTimestamp = Math.floor(Date.now() / 1000) + 60; // 1 minute from now
            const claims = { exp: expirationTimestamp };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            // Default buffer is 5 minutes
            expect(token.isExpired()).toBe(true);
        });

        it('should respect custom buffer time', () => {
            const expirationTimestamp = Math.floor(Date.now() / 1000) + 120; // 2 minutes from now
            const claims = { exp: expirationTimestamp };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            // 1 minute buffer - should not be expired
            expect(token.isExpired(60 * 1000)).toBe(false);

            // 3 minute buffer - should be expired
            expect(token.isExpired(3 * 60 * 1000)).toBe(true);
        });

        it('should return true for token without expiration', () => {
            const claims = { tid: 'test-tenant' };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.isExpired()).toBe(true);
        });

        it('should return true for invalid token', () => {
            const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
            const token = new JWTToken('invalid-token');

            expect(token.isExpired()).toBe(true);

            consoleErrorSpy.mockRestore();
        });
    });

    describe('edge cases and special characters', () => {
        it('should handle base64url encoding correctly', () => {
            // Test with special characters that differ between base64 and base64url
            const claims = {
                tid: 'tenant-with-special-chars+/=',
                name: 'User with special chars +/=',
            };
            const payload = JSON.stringify(claims);
            // Simulate base64url encoding (- instead of +, _ instead of /)
            const encodedPayload = btoa(payload).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
            const tokenString = `header.${encodedPayload}.signature`;
            const token = new JWTToken(tokenString);

            expect(token.isValid).toBe(true);
            expect(token.tenantId).toBe('tenant-with-special-chars+/=');
            expect(token.name).toBe('User with special chars +/=');
        });

        it('should handle empty claims object', () => {
            const claims = {};
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.isValid).toBe(true);
            expect(token.expiration).toBeNull();
            expect(token.tenantId).toBeNull();
            expect(token.objectId).toBeNull();
            expect(token.username).toBeNull();
            expect(token.name).toBeNull();
            expect(token.email).toBeNull();
        });

        it('should handle claims with null values', () => {
            const claims = {
                tid: null,
                oid: null,
                name: null,
            };
            const tokenString = createTestToken(claims);
            const token = new JWTToken(tokenString);

            expect(token.isValid).toBe(true);
            expect(token.tenantId).toBeNull();
            expect(token.objectId).toBeNull();
            expect(token.name).toBeNull();
        });
    });
});
