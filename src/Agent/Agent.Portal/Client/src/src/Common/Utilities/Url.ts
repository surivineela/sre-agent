/** `https://foo.com/` + `/path` -> `https://foo.com/path` */
export const addPathToHostname = (origin: string, path: string) => {
    const url = new URL(origin);
    return new URL(path, url.origin).href;
};
