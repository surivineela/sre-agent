import { describe, expect, it } from 'vitest';
import { ThreadSource } from '../../Contracts/DataPlane/Thread';
import { getThreadsGetUrlPath, ThreadSeverity, ThreadsGetOptions } from '../ThreadClient';

describe('getThreadsGetUrlPath', () => {
    it('Has all filters - descending', () => {
        const options: ThreadsGetOptions = {
            skip: 10,
            top: 20,
            descending: true,
            filters: {
                searchText: 'test',
                timestamps: {
                    min: { timestamp: '2025-01-01T00:00:00Z', inclusive: true },
                    max: { timestamp: '2025-12-31T23:59:59Z', inclusive: false },
                },
                source: ThreadSource.incident,
            },
            severity: ThreadSeverity.Critical,
        };

        const url = getThreadsGetUrlPath(options);
        expect(url).toBe(
            `/api/v1/threads?skip=10&top=20&orderby=modifiedTimestamp+desc&filter=contains(tolower(title),'test') and modifiedTimestamp ge 2025-01-01T00:00:00Z and modifiedTimestamp lt 2025-12-31T23:59:59Z and source eq 'Incident'&severity=Critical`
        );
    });

    it('Has all filters - ascending', () => {
        const options: ThreadsGetOptions = {
            skip: 10,
            top: 20,
            descending: false,
            filters: {
                searchText: 'test',
                timestamps: {
                    min: { timestamp: '2025-01-01T00:00:00Z', inclusive: true },
                    max: { timestamp: '2025-12-31T23:59:59Z', inclusive: false },
                },
                source: ThreadSource.incident,
            },
            severity: ThreadSeverity.Critical,
        };

        const url = getThreadsGetUrlPath(options);
        expect(url).toBe(
            `/api/v1/threads?skip=10&top=20&orderby=modifiedTimestamp&filter=contains(tolower(title),'test') and modifiedTimestamp ge 2025-01-01T00:00:00Z and modifiedTimestamp lt 2025-12-31T23:59:59Z and source eq 'Incident'&severity=Critical`
        );
    });

    it('No search text', () => {
        const options: ThreadsGetOptions = {
            skip: 10,
            top: 20,
            descending: true,
            filters: {
                timestamps: {
                    min: { timestamp: '2025-01-01T00:00:00Z', inclusive: true },
                    max: { timestamp: '2025-12-31T23:59:59Z', inclusive: false },
                },
                source: ThreadSource.incident,
            },
            severity: ThreadSeverity.Critical,
        };

        const url = getThreadsGetUrlPath(options);
        expect(url).toBe(
            `/api/v1/threads?skip=10&top=20&orderby=modifiedTimestamp+desc&filter=modifiedTimestamp ge 2025-01-01T00:00:00Z and modifiedTimestamp lt 2025-12-31T23:59:59Z and source eq 'Incident'&severity=Critical`
        );
    });

    it('No min timestamp', () => {
        const options: ThreadsGetOptions = {
            skip: 10,
            top: 20,
            descending: true,
            filters: {
                searchText: 'test',
                timestamps: {
                    max: { timestamp: '2025-12-31T23:59:59Z', inclusive: true },
                },
                source: ThreadSource.incident,
            },
            severity: ThreadSeverity.Critical,
        };

        const url = getThreadsGetUrlPath(options);
        expect(url).toBe(
            `/api/v1/threads?skip=10&top=20&orderby=modifiedTimestamp+desc&filter=contains(tolower(title),'test') and modifiedTimestamp le 2025-12-31T23:59:59Z and source eq 'Incident'&severity=Critical`
        );
    });

    it('No max timestamp', () => {
        const options: ThreadsGetOptions = {
            skip: 10,
            top: 20,
            descending: true,
            filters: {
                searchText: 'test',
                timestamps: {
                    min: { timestamp: '2025-01-01T00:00:00Z', inclusive: false },
                },
                source: ThreadSource.incident,
            },
            severity: ThreadSeverity.Critical,
        };

        const url = getThreadsGetUrlPath(options);
        expect(url).toBe(
            `/api/v1/threads?skip=10&top=20&orderby=modifiedTimestamp+desc&filter=contains(tolower(title),'test') and modifiedTimestamp gt 2025-01-01T00:00:00Z and source eq 'Incident'&severity=Critical`
        );
    });

    it('No source', () => {
        const options: ThreadsGetOptions = {
            skip: 10,
            top: 20,
            descending: true,
            filters: {
                searchText: 'test',
                timestamps: {
                    min: { timestamp: '2025-01-01T00:00:00Z', inclusive: true },
                    max: { timestamp: '2025-12-31T23:59:59Z', inclusive: false },
                },
            },
            severity: ThreadSeverity.Critical,
        };

        const url = getThreadsGetUrlPath(options);
        expect(url).toBe(
            `/api/v1/threads?skip=10&top=20&orderby=modifiedTimestamp+desc&filter=contains(tolower(title),'test') and modifiedTimestamp ge 2025-01-01T00:00:00Z and modifiedTimestamp lt 2025-12-31T23:59:59Z&severity=Critical`
        );
    });

    it('No severity', () => {
        const options: ThreadsGetOptions = {
            skip: 10,
            top: 20,
            descending: true,
            filters: {
                searchText: 'test',
                timestamps: {
                    min: { timestamp: '2025-01-01T00:00:00Z', inclusive: true },
                    max: { timestamp: '2025-12-31T23:59:59Z', inclusive: false },
                },
                source: ThreadSource.incident,
            },
        };

        const url = getThreadsGetUrlPath(options);
        expect(url).toBe(
            `/api/v1/threads?skip=10&top=20&orderby=modifiedTimestamp+desc&filter=contains(tolower(title),'test') and modifiedTimestamp ge 2025-01-01T00:00:00Z and modifiedTimestamp lt 2025-12-31T23:59:59Z and source eq 'Incident'`
        );
    });
});
