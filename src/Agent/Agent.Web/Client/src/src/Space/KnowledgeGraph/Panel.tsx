import { Button, DrawerBody, DrawerHeader, DrawerHeaderTitle, OverlayDrawer } from "@fluentui/react-components";
import { memo, useEffect, useState } from "react";
import { GraphNode } from "../Hooks/useGraph";
import { Dismiss24Regular } from "@fluentui/react-icons";

interface IPanelProps {
    node?: GraphNode;
    setSelectedNode: (node?: GraphNode) => void;
}

const Panel = ({ node, setSelectedNode }: IPanelProps) => {
    const [isOpen, setIsOpen] = useState(false);

    useEffect(() => {
        setIsOpen(!!node && node.isVisible);
    }, [node]);

    return <OverlayDrawer
        modalType="non-modal"
        open={isOpen}
        position="end"
    >
        <DrawerHeader>
            <DrawerHeaderTitle
                action={
                    <Button
                        appearance="subtle"
                        aria-label="Close"
                        icon={<Dismiss24Regular />}
                        onClick={() => setSelectedNode(undefined)}
                    />
                }
            >
                {node?.name ?? ''}
            </DrawerHeaderTitle>
        </DrawerHeader>

        <DrawerBody>
            <p>Drawer content</p>
        </DrawerBody>
    </OverlayDrawer>
}

export default memo(Panel)