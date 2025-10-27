export interface Response<T> {
    isSuccessful: boolean;
    error?: any;
    content?: T;
}
