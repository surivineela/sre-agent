import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";

export const useQueryParams = () => {
    const [searchParams] = useSearchParams();
    const [mode, setMode] = useState<string>("");
    const [isDebug, setIsDebug] = useState<boolean>(false);
    const [isPlayground, setIsPlayground] = useState<boolean>(false);
    useEffect(() => {
        const modeFromSearchParams = searchParams.get("mode") || "";
        const debugFromSearchParams = searchParams.get("debug") || "";
        setMode(modeFromSearchParams);
        setIsDebug(debugFromSearchParams.toLowerCase() === "true");
        setIsPlayground(modeFromSearchParams.toLowerCase() === "playground");
    }, [searchParams]);
    return {
        mode,
        isDebug,
        isPlayground,
        searchParams
    };
}