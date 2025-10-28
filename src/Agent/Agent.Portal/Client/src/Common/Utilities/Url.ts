/** `https://foo.com/` + `/path` -> `https://foo.com/path` */
export const addPathToHostname = (origin: string, path: string): string => {
    const url = new URL(origin);
    return new URL(path, url.origin).href;
};

export const appendQueryString = (url: string, queryString: string): string => {
    if (!queryString) {
        return url;
    }

    if (url.includes('?')) {
        return `${url}&${queryString}`;
    }
    return `${url}?${queryString}`;
};

export const getParameterByName = (url: string | null, name: string): string | null => {
    let urlFull = url;
    if (urlFull === null) {
        urlFull = window.location.href;
    }

    if (!name) {
        return null;
    }

    const sanitizedName = name.replace(/[[\]]/g, '\\$&');
    const regex = new RegExp(`[?&]${sanitizedName}(=([^&#]*)|&|#|$)`, 'i');
    const results = regex.exec(urlFull);

    if (!results) {
        return null;
    }

    if (!results[2]) {
        return '';
    }

    return decodeURIComponent(results[2].replace(/\+/g, ' '));
};
