export interface Response<T> {
    isSuccessful: boolean;
    error?: any;
    content?: T;
    metadata?: {
        status: number;
        headers: Record<string, string | undefined>;
    };
}
