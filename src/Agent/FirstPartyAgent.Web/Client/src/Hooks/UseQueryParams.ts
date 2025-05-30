import { useMemo } from "react";
import { useSearchParams } from "react-router-dom";

export const useQueryParams = () => {
    const [searchParams,setSearchParams] = useSearchParams();
    
    const queryParamValues = useMemo(() => {
        const mode = searchParams.get("mode") || "";
        const isDebug = searchParams.get("debug")?.toLowerCase() === "true";
        const isPlayground = mode.toLowerCase() === "playground";
        return {
            mode,
            isDebug,
            isPlayground
        }
    }, [searchParams]);

    return {
        ...queryParamValues,
        searchParams,
        setSearchParams
    };
}