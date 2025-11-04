export type ResultFormat = 'table' | 'objectArray';

export interface ARGOptions {
    $top?: number;
    $skipToken?: string;
    resultFormat?: ResultFormat;
}

export interface ARGRequestContent {
    subscriptions?: string[];
    query: string;
    options?: ARGOptions;
}

// Base response with common properties
export interface ARGResponseBase {
    count: number;
    resultTruncated: boolean;
    totalRecords: number;
    $skipToken?: string;
}

// Table format response (columns + rows)
export interface ARGResponseTable extends ARGResponseBase {
    data: {
        columns: ARGResponseDataColumn[];
        rows: any[][];
    };
}

// Object array format response (default)
export interface ARGResponseObjectArray<T = any> extends ARGResponseBase {
    data: T[];
}

// Union type for flexibility
export type ARGResponse<T = any> = ARGResponseTable | ARGResponseObjectArray<T>;

export interface ARGResponseDataColumn {
    name: string;
    type: 'string' | 'integer' | 'object';
}

// Type guard to check if response is in table format
export const isTableFormat = (response: ARGResponse): response is ARGResponseTable => {
    return (
        response.data !== null &&
        typeof response.data === 'object' &&
        'columns' in response.data &&
        'rows' in response.data &&
        Array.isArray(response.data.columns) &&
        Array.isArray(response.data.rows)
    );
};
