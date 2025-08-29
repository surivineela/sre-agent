import { describe, expect, it } from 'vitest';
import Url from '../Url';

describe('Url helpers', () => {
    it('appendQueryString appends ? or & appropriately', () => {
        expect(Url.appendQueryString('https://x/y', 'a=1')).toBe('https://x/y?a=1');
        expect(Url.appendQueryString('https://x/y?p=0', 'a=1')).toBe('https://x/y?p=0&a=1');
        expect(Url.appendQueryString('https://x/y', '')).toBe('https://x/y');
    });

    it('getParameterByName parses values and decodes + to space', () => {
        const url = 'https://host/path?foo=bar+baz&empty=&encoded=a%20b&arr=a,b,c';
        expect(Url.getParameterByName(url, 'foo')).toBe('bar baz');
        expect(Url.getParameterByName(url, 'empty')).toBe('');
        expect(Url.getParameterByName(url, 'encoded')).toBe('a b');
        expect(Url.getParameterByName(url, 'missing')).toBeNull();
    });

    it('getParameterArrayByName splits comma-separated values', () => {
        const url = 'https://host/path?arr=a,b,c';
        expect(Url.getParameterArrayByName(url, 'arr')).toEqual(['a', 'b', 'c']);
        expect(Url.getParameterArrayByName(url, 'missing')).toEqual([]);
    });

    it('getPathAndQuery strips hash and preserves query', () => {
        const url = 'https://host/a/b?c=1#hash';
        expect(Url.getPathAndQuery(url)).toBe('/a/b?c=1');
    });

    it('getPath and getHostName from explicit url', () => {
        const url = 'https://sub.domain.tld:4444/a/b?c=1#h';
        expect(Url.getPath(url)).toBe('/a/b');
        expect(Url.getHostName(url)).toBe('sub.domain.tld');
    });
});
