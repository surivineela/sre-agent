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
            const data = error.response.data;
            let errorMessage = data.message || data.error?.message || data.error || data.title;
            errorMessage = typeof errorMessage === 'string' ? errorMessage : JSON.stringify(errorMessage);

            // Include validation error details if available
            if (data.details?.errors && Array.isArray(data.details.errors)) {
                const validationErrors = data.details.errors
                    .map((e: { field?: string; message?: string }) => e.message || e.field)
                    .filter(Boolean)
                    .join('; ');
                if (validationErrors) {
                    errorMessage = `${errorMessage}: ${validationErrors}`;
                }
            }

            return errorMessage;
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
