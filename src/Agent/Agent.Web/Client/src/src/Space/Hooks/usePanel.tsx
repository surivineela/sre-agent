import axios from "axios";
import { GraphContext, ResourceExtended } from "../Contracts/Graph";
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { Guid } from "../../Common/Helpers/Guid";
import { Link, Toast, ToastBody, ToastIntent, ToastTitle, ToastTrigger, useToastController } from "@fluentui/react-components";
import { getAgentHeaders } from "../../Common/Helpers/headers";

const getResource = async (resourceId: string): Promise<ResourceExtended | undefined> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/resource/${resourceId}`, {
            headers: getAgentHeaders()
        });
        return (data ?? [])?.[0];
    } catch {
        return undefined;
    }
}

const patchResource = async (resourceId: string, remarks: string): Promise<void> => {
    await axios.patch(`../api/v1/graph/resource/${resourceId}/remarks`, { remarks }, {
        headers: getAgentHeaders()
    });
}

export const createThread = async (resourceId: string) => {
    const url = `../api/v1/threads`;

    const response = await axios.post(url, {
        startMessage: {
            text: `Resource ${resourceId} is unhealthy could you help diagnose what is wrong?`,
            userId: 'web-client-user',
            displayName: 'Web Client User',
        }
    }, {
        headers: getAgentHeaders()
    });
    return response?.data;
}

export const getPropertyValue = (input?: string[]): string => {
    return input?.[0] ?? '';
}

export const usePanel = () => {

    const { selectedNode } = useContext(GraphContext);

    const [resource, setResource] = useState<ResourceExtended>();
    const [initialRemarks, setInitialRemarks] = useState<string>('');
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [isUpdating, setIsUpdating] = useState<boolean>(false);

    const toasterId = useMemo(() => Guid.newGuid(), []);
    const { dispatchToast, updateToast } = useToastController(toasterId);

    const isMounted = useRef(true);

    const refresh = () => {
        if (selectedNode) {
            setIsLoading(true);
            return getResource(selectedNode.id)
                .then((resource) => {
                    if (isMounted.current) {
                        setResource(resource);
                        setInitialRemarks(getPropertyValue(resource?.properties?.remarks));
                    }
                }).finally(() => {
                    if (isMounted.current) {
                        setIsLoading(false);
                    }
                })
        }
    }

    const notify = (status: ToastIntent, errorMessage?: string) => {
        const name = selectedNode?.name;
        const title = "Annotation update";
        let description =
            `We are updating annotation for your resource${name ? ` '${name}'` : ""}`;

        switch (status) {
            case 'success':
                description = "Your annotation is updated successfully";
                break;
            case 'error':
                description = `Failed to update the annotation with the error: ${errorMessage}`;
                break;
        }

        if (status === 'info') {
            dispatchToast(
                <Toast>
                    <ToastTitle
                        action={
                            <ToastTrigger>
                                <Link>{"Dismiss"}</Link>
                            </ToastTrigger>
                        }
                    >
                        {title}
                    </ToastTitle>
                    <ToastBody>{description}</ToastBody>
                </Toast>,
                {
                    intent: status,
                    timeout: 7000,
                    position: "top-end",
                    toastId: toasterId,
                },
            );
        } else {
            updateToast({
                content: <Toast>
                    <ToastTitle
                        action={
                            <ToastTrigger>
                                <Link>{"Dismiss"}</Link>
                            </ToastTrigger>
                        }
                    >
                        {title}
                    </ToastTitle>
                    <ToastBody>{description}</ToastBody>
                </Toast>,
                intent: status,
                toastId: toasterId,
                timeout: 7000,
                position: "top-end",
            })
        }

    };

    const onSubmit = useCallback(async (remarks: string) => {
        if (selectedNode) {
            setIsUpdating(true);
            notify('info');

            try {
                await patchResource(selectedNode.id, remarks);
                notify('success');
            } catch (e: any) {
                notify('error', e)
            } finally {
                setIsUpdating(false);
                refresh();
            }

        }
    }, [selectedNode]);

    useEffect(() => {
        refresh();
    }, [selectedNode]);

    useEffect(() => {
        isMounted.current = true;

        return () => {
            isMounted.current = false;
        }
    })

    return {
        resource,
        initialRemarks,
        isLoading,
        isUpdating,
        onSubmit,
        toasterId
    }

}
