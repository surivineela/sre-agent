export interface ARGOptions {
    $top?: number;
    $skipToken?: string;
}

export interface ARGRequestContent {
    subscriptions?: string[];
    query: string;
    options?: ARGOptions;
}

export interface ARGResponse {
    count: number;
    resultTruncated: boolean;
    data: ARGResponseObjData;
    totalRecord: number;
    $skipToken?: string;
}

export interface ARGResponseObjData {
    columns: ARGResponseDataColumn[];
    rows: any[][];
}

export interface ARGResponseDataColumn {
    name: string;
    type: 'string' | 'integer' | 'object';
}
