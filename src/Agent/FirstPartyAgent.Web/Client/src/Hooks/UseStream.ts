import { useMutation } from "@tanstack/react-query"
import axios, { AxiosRequestConfig } from "axios";
import { useEffect, useRef, useState } from "react";

export const useStream = <T>() => {
    const [streamResponses, setStreamResponses] = useState<T>(null);
    const abortControllerRef = useRef<AbortController | null>(null);
    const reactQuery = useMutation({
        mutationFn: async (req: AxiosRequestConfig) => {
            abortControllerRef.current = new AbortController();
            try {
                const request: AxiosRequestConfig = {
                    ...req,
                    responseType: 'stream',
                    signal: abortControllerRef.current?.signal ?? null,
                    onDownloadProgress: (progressEvent) => {
                        const currentTarget = progressEvent.event.currentTarget as XMLHttpRequest;
                        if (currentTarget.status >= 200 && currentTarget.status < 300) {
                            const data = currentTarget.response;
                            if (data) {
                                setStreamResponses(data);
                            }
                        }
                    }
                }
                setStreamResponses(null);
                const response = await axios.request<T>(request);
                setStreamResponses(response.data);
                return response.data;
            }catch (error) {
                // Throw error if it's not due to cancellation
                if(!axios.isCancel(error)) {
                    throw error;
                }
            }
        },
        mutationKey: ["processStream"],
    });

    const abortStream = () => {
        if (abortControllerRef.current) {
            abortControllerRef.current.abort();
            abortControllerRef.current = null;
        }
    }

    useEffect(() => {
        return () => {
            abortStream();
        }
    }, []);
    return { ...reactQuery, streamResponses, abortStream };
}