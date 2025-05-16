import axios from "axios";

export default class ErrorUtilities {
    public static readonly defaultErrorMessage = "An error occurred while loading the data.";
    public static extractErrorMessage(error: Error, defaultErrorMessage = ErrorUtilities.defaultErrorMessage): string {
        if (axios.isAxiosError(error)) {
            return error.response?.data ?? defaultErrorMessage
        } else {
            return error?.message ?? defaultErrorMessage;
        }
    }
}