export interface Response<T> {
    isSuccessful: boolean;
    error?: any;
    content?: T;
}

export const getDataPlaneErrorMessage = (error: any): string => {
    if (error?.response?.data) {
        if (typeof error.response.data === 'string') {
            return error.response.data;
        }

        if (typeof error.response.data === 'object') {
            const parsedError =
                error.response.data.message || error.response.data.error?.message || error.response.data.error || error.response.data.title;
            return typeof parsedError === 'string' ? parsedError : JSON.stringify(parsedError);
        }
    }

    if (error?.response?.statusText) {
        return `${error.response.status}: ${error.response.statusText}`;
    }

    const parsedError = error?.message || '';
    return typeof parsedError === 'string' ? parsedError : JSON.stringify(parsedError);
};

export class DataPlaneClient {
    private _sreAgentEndpoint: string;

    constructor(sreAgentEndpoint: string) {
        this._sreAgentEndpoint = sreAgentEndpoint;
    }

    protected getErrorMessage(error: any): string {
        return getDataPlaneErrorMessage(error);
    }

    protected getRequestUrl(path: string) {
        const sanitizedPath = path.startsWith('/') ? path : `/${path}`;
        return `${this._sreAgentEndpoint}${sanitizedPath}`;
    }
}
