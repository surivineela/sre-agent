export interface Response<T> {
    isSuccessful: boolean;
    error?: any;
    content?: T;
}

export class DataPlaneClient {
    private _sreAgentEndpoint: string;

    constructor(sreAgentEndpoint: string) {
        this._sreAgentEndpoint = sreAgentEndpoint;
    }

    protected getRequestUrl(path: string) {
        const sanitizedPath = path.startsWith('/') ? path : `/${path}`;
        return `${this._sreAgentEndpoint}${sanitizedPath}`;
    }
}
