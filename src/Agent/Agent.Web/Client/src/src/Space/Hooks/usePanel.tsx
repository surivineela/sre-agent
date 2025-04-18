import axios from "axios";
import { GraphContext, ResourceExtended } from "../Contracts/Graph";
import { useContext, useEffect, useState } from "react";

const getResource = async (resourceId: string): Promise<ResourceExtended | undefined> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/resource/${resourceId}`);
        return (data ?? [])?.[0];
    } catch {
        return undefined;
    }
}

export const getPropertyValue = (input?: string[]): string => {
    return input?.[0] ?? '';
}

export const usePanel = () => {

    const { selectedNode } = useContext(GraphContext);

    const [resource, setResource] = useState<ResourceExtended>();
    const [initialRemarks, setInitialRemarks] = useState<string>('');
    const [isLoading, setIsLoading] = useState<boolean>(false);

    useEffect(() => {
        let isSubscribed = true;

        if (selectedNode) {
            setIsLoading(true);

            getResource(selectedNode.id)
                .then((resource) => {
                    if (isSubscribed) {
                        setResource(resource);
                        setInitialRemarks(getPropertyValue(resource?.properties?.remarks));
                    }
                }).finally(() => {
                    if (isSubscribed) {
                        setIsLoading(false);
                    }
                })
        }

        return () => {
            isSubscribed = false;
        }
    }, [selectedNode]);

    return {
        resource,
        initialRemarks,
        isLoading,
    }

}
