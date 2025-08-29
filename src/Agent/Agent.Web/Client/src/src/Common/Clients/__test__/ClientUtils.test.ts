import { describe, expect, it } from 'vitest';
import { getDataPlaneErrorMessage } from '../DataPlaneClient';

describe('ClientUtils', () => {
    describe('getDataPlaneErrorMessage', () => {
        it('prefers response.data string', () => {
            expect(getDataPlaneErrorMessage({ response: { data: 'boom' } })).toBe('boom');
        });
        it('picks message/error/title from object', () => {
            expect(getDataPlaneErrorMessage({ response: { data: { message: 'm' } } })).toBe('m');
            expect(getDataPlaneErrorMessage({ response: { data: { error: 'e' } } })).toBe('e');
            expect(getDataPlaneErrorMessage({ response: { data: { title: 't' } } })).toBe('t');
        });
        it('falls back to statusText then error.message', () => {
            expect(getDataPlaneErrorMessage({ response: { status: 400, statusText: 'Bad' } })).toBe('400: Bad');
            expect(getDataPlaneErrorMessage({ message: 'oops' })).toBe('oops');
        });
    });
});
