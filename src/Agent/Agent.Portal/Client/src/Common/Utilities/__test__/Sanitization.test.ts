import { describe, expect, it } from 'vitest';
import {
    AdditionalLogData,
    RedactedMessage,
    getSanitizedLogData,
    hasCredentialValueInString,
    sanitizeMessageString,
    sanitizeString,
    sanitizeUriString,
} from '../Sanitization';

describe('Sanitization utilities', () => {
    describe('hasCredentialValueInString', () => {
        it('returns true for strings with credential patterns', () => {
            expect(hasCredentialValueInString('password=secret123')).toBe(true);
            expect(hasCredentialValueInString('token=abc123')).toBe(true);
            expect(hasCredentialValueInString('key=mykey')).toBe(true);
            expect(hasCredentialValueInString('sig=signature')).toBe(true);
        });

        it('returns false for strings without credential patterns', () => {
            expect(hasCredentialValueInString('hello world')).toBe(false);
            expect(hasCredentialValueInString('user@example.com')).toBe(false);
            expect(hasCredentialValueInString('password')).toBe(false); // no equals
        });
    });

    describe('sanitizeMessageString', () => {
        it('redacts strings with credential values', () => {
            const result = sanitizeMessageString('Connect with password=secret123');
            expect(result).toContain(RedactedMessage.passwordOrKeyDetected);
            expect(result).not.toContain('secret123');
        });

        it('sanitizes strings with credential keys', () => {
            const result = sanitizeMessageString('access_token=abc123def');
            expect(result).toContain(RedactedMessage.passwordOrKeyDetected);
        });

        it('sanitizes embedded tokens within string', () => {
            const result = sanitizeMessageString('Connection token=abc123 used');
            expect(result).toContain(RedactedMessage.passwordOrKeyDetected);
        });

        it('returns clean strings unchanged', () => {
            const clean = 'This is a safe message';
            expect(sanitizeMessageString(clean)).toBe(clean);
        });
    });

    describe('sanitizeString', () => {
        it('redacts password values', () => {
            const result = sanitizeString('Server=myserver;password=secret123');
            expect(result).toContain(RedactedMessage.passwordOrKeyDetected);
            expect(result).not.toContain('secret123');
        });

        it('redacts token values', () => {
            const result = sanitizeString('url with token=abc123');
            expect(result).toContain(RedactedMessage.passwordOrKeyDetected);
        });

        it('preserves non-sensitive content', () => {
            const safe = 'Regular message without credentials';
            expect(sanitizeString(safe)).toBe(safe);
        });
    });

    describe('sanitizeUriString', () => {
        it('redacts JWT tokens in URIs', () => {
            const jwt = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U';
            const result = sanitizeUriString(`https://api.example.com/data/${jwt}`);
            expect(result).toContain(RedactedMessage.passwordOrKeyDetected);
        });

        it('redacts account keys in URIs', () => {
            const result = sanitizeUriString('https://storage.blob.core.windows.net/?accountkey=mykey123');
            expect(result).toContain(RedactedMessage.passwordOrKeyDetected);
        });

        it('preserves safe URI content', () => {
            const safe = 'https://example.com/api/users?id=123&name=test';
            expect(sanitizeUriString(safe)).toBe(safe);
        });
    });

    describe('getSanitizedLogData', () => {
        it('returns empty object for null/undefined input', () => {
            expect(getSanitizedLogData(null as any)).toEqual({});
            expect(getSanitizedLogData(undefined as any)).toEqual({});
        });

        it('sanitizes string values in log data', () => {
            const logData: AdditionalLogData = {
                message: 'Connection failed with password=secret123',
                userId: 'user@example.com',
            };
            const result = getSanitizedLogData(logData);
            expect(result.message).toContain(RedactedMessage.passwordOrKeyDetected);
            expect(result.userId).toBe('user@example.com');
        });

        it('redacts credential keys regardless of value', () => {
            const logData = {
                username: 'admin',
                password: 'safe-value',
                token: 'my-token',
                apiKey: 'key123',
            };
            const result = getSanitizedLogData(logData);
            expect(result.username).toBe('admin');
            expect(result.password).toBe(RedactedMessage.passwordOrKeyDetected);
            expect(result.token).toBe(RedactedMessage.passwordOrKeyDetected);
            expect(result.apiKey).toBe(RedactedMessage.passwordOrKeyDetected);
        });

        it('sanitizes resourceId by removing query strings', () => {
            const logData = {
                resourceId: '/subscriptions/abc123/resourceGroups/rg?password=secret&token=tok',
            };
            const result = getSanitizedLogData(logData);
            expect(result.resourceId).not.toContain('password');
            expect(result.resourceId).not.toContain('secret');
        });

        it('handles nested objects', () => {
            const logData = {
                outer: 'safe',
                nested: {
                    password: 'secret',
                    safe: 'value',
                },
            };
            const result = getSanitizedLogData(logData);
            expect(result.outer).toBe('safe');
            expect((result.nested as any).password).toBe(RedactedMessage.passwordOrKeyDetected);
            expect((result.nested as any).safe).toBe('value');
        });

        it('does not modify original object', () => {
            const logData = {
                message: 'password=secret',
                secret: 'mySecret',
            };
            const original = JSON.stringify(logData);
            getSanitizedLogData(logData);
            expect(JSON.stringify(logData)).toBe(original);
        });

        it('handles arrays and complex nested structures', () => {
            const logData = {
                items: {
                    token: 'should-be-redacted',
                    info: 'safe-info',
                },
            };
            const result = getSanitizedLogData(logData);
            expect((result.items as any).token).toBe(RedactedMessage.passwordOrKeyDetected);
            expect((result.items as any).info).toBe('safe-info');
        });
    });

    describe('RedactedMessage constants', () => {
        it('has expected constant values', () => {
            expect(RedactedMessage.passwordOrKeyDetected).toBe('REDACTED (possible password or key detected)');
            expect(RedactedMessage.freeform).toBe('REDACTED (freeform)');
        });
    });
});
