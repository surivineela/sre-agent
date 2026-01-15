import { useCallback, useState } from 'react';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { useLocalStorage } from '../../../Common/Hooks/useLocalStorage';
import { useTelemetry } from '../../../Common/Hooks/useTelemetry';

export enum AgentSpaceNavItem {
    Overview = 'overview',
    Configuration = 'configuration',
    GenevaActionPolicies = 'genevaActionPolicies',
    Connectors = 'connectors',
}

const NAV_COLLAPSED_STORAGE_KEY = 'agent-space-nav-collapsed';

interface UseAgentSpaceNavReturn {
    isNavOpen: boolean;
    selectedView: AgentSpaceNavItem;
    setSelectedView: (view: AgentSpaceNavItem) => void;
    toggleNav: () => void;
}

export const useAgentSpaceNav = (initialView: AgentSpaceNavItem = AgentSpaceNavItem.Overview): UseAgentSpaceNavReturn => {
    const { logEvent } = useTelemetry(TelemetrySource.AgentSpaceView, undefined);
    const { value: isCollapsed, setValue: setIsCollapsed } = useLocalStorage<boolean>(
        NAV_COLLAPSED_STORAGE_KEY,
        false,
        TelemetrySource.AgentSpaceView
    );
    const [selectedView, setSelectedView] = useState<AgentSpaceNavItem>(initialView);

    const isNavOpen = !isCollapsed;

    const toggleNav = useCallback(() => {
        const newCollapsedState = !isCollapsed;
        setIsCollapsed(newCollapsedState);
        logEvent({
            action: 'AgentSpaceNavToggle',
            actionModifier: newCollapsedState ? 'collapsed' : 'expanded',
            additionalData: { isCollapsed: newCollapsedState },
        });
    }, [isCollapsed, setIsCollapsed, logEvent]);

    const handleSetSelectedView = useCallback(
        (view: AgentSpaceNavItem) => {
            setSelectedView(view);
            logEvent({
                action: 'AgentSpaceNavItemSelected',
                actionModifier: 'selected',
                additionalData: { view },
            });
        },
        [logEvent]
    );

    return {
        isNavOpen,
        selectedView,
        setSelectedView: handleSetSelectedView,
        toggleNav,
    };
};
