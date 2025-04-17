import { useEffect, useRef } from "react"
import { GraphEdge, GraphNode } from "../Contracts/Graph";
import { Node, Edge } from "@xyflow/react";

export const useElkLayout = () => {
    const workerRef = useRef<Worker>();

    const layoutGraph = async (nodes: Node<GraphNode>[], edges: Edge<GraphEdge>[]): Promise<any> => {
        return new Promise((resolve, reject) => {
            const worker = workerRef.current;
            if (!worker) return reject('Worker not initialized')

            const handleMessage = (event: MessageEvent<{ error: unknown, type: string, layout: any }>) => {
                const { type, layout, error } = event.data
                if (type === 'success') {
                    resolve(layout)
                } else {
                    reject(error)
                }
                worker.removeEventListener('message', handleMessage)
            }

            worker.onmessage = (event) => {
                handleMessage(event);
            };
            worker.postMessage({ nodes, edges })
        });
    }

    useEffect(() => {
        workerRef.current = new Worker(new URL('../elkWorker.ts', import.meta.url), {
            type: 'module',
        });

        return () => {
            workerRef.current?.terminate();
        }
    }, []);

    return layoutGraph;
}