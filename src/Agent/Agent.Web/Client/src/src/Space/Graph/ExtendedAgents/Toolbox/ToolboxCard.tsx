import { Link, mergeClasses } from '@fluentui/react-components';
import { ChevronDownUp20Regular } from '@fluentui/react-icons';
import { Handle, Node, NodeProps } from '@xyflow/react';
import { FC, memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import {
    ExtendedAgentGraphContext,
    ExtendedAgentGraphNode,
    ExtendedAgentNodeSize,
    ToolboxData,
} from '../../../Contracts/ExtendedAgentGraph';
import { HANDLE_POSITIONS, HANDLE_POSITION_MAP } from '../../../Contracts/Graph';
import { useToolNodeStyles } from '../../../Styles/ExtendedAgentGraph.styles';
import { getHandleId } from '../../Utility';
import { useToolboxCardStyles } from './ToolboxCard.styles';
import { ToolboxRow } from './ToolboxRow';

const Handles = memo(() => {
    const { handle } = useToolNodeStyles();

    return (
        <>
            {HANDLE_POSITIONS.map(position => (
                <div key={position}>
                    <Handle className={handle} type="target" position={HANDLE_POSITION_MAP[position]} id={getHandleId(position, true)} />
                    <Handle className={handle} type="source" position={HANDLE_POSITION_MAP[position]} id={getHandleId(position, false)} />
                </div>
            ))}
        </>
    );
});

Handles.displayName = 'ExpandedToolboxHandles';

interface ToolboxProps extends NodeProps<Node<ExtendedAgentGraphNode>> {
    expanded: boolean;
}

export const Toolbox = (props: ToolboxProps) => {
    const { id, data, expanded } = props;
    const intl = useIntl();
    const { hoverNode, unHoverNode, toggleToolboxExpanded } = useContext(ExtendedAgentGraphContext);

    const { groupContainer, toolsContainer, collapseLinkContainer, collapseLink } = useToolboxCardStyles();

    const toolboxData = data?.data as ToolboxData | undefined;
    const visibleTools = useMemo(() => {
        if (!toolboxData?.tools?.length) {
            return [];
        }
        if (expanded) {
            return toolboxData.tools;
        }
        return toolboxData.tools.slice(0, ExtendedAgentNodeSize.toolsCollapsedMaxRows);
    }, [toolboxData?.tools]);
    const toolCount = toolboxData?.toolCount ?? 0;
    const agentName = toolboxData?.agentName ?? '';

    const handleCollapseClick = (e: React.MouseEvent) => {
        e.stopPropagation();
        toggleToolboxExpanded(agentName);
    };

    const { toolsContainerClassName, toolsContainerHeight, toolsContainerOverflowY } = useMemo(() => {
        if (expanded && toolCount > ExtendedAgentNodeSize.toolsExpandedMaxRows) {
            const height = ExtendedAgentNodeSize.getExpandedToolsContainerHeight(toolCount);
            return {
                toolsContainerClassName: mergeClasses(toolsContainer, 'nowheel'),
                toolsContainerHeight: `${height}px`,
                toolsContainerOverflowY: 'auto' as const,
            };
        } else {
            return {
                toolsContainerClassName: toolsContainer,
                toolsContainerHeight: undefined,
                toolsContainerOverflowY: 'unset' as const,
            };
        }
    }, [expanded, toolCount]);

    return (
        <div className={groupContainer} onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <div
                className={toolsContainerClassName}
                style={{
                    height: toolsContainerHeight,
                    overflowY: toolsContainerOverflowY,
                }}
            >
                {visibleTools.map(toolItem => (
                    <ToolboxRow
                        key={toolItem.tool.name}
                        agentName={agentName}
                        tool={toolItem.tool}
                        connector={toolItem.connector}
                        isSystemTool={toolItem.isSystemTool}
                    />
                ))}
            </div>
            {toolCount > ExtendedAgentNodeSize.toolsCollapsedMaxRows && (
                <div className={collapseLinkContainer}>
                    <Link className={collapseLink} onClick={handleCollapseClick}>
                        <ChevronDownUp20Regular aria-hidden="true" />
                        {expanded
                            ? intl.formatMessage(ExtendedAgentsGraphResources.collapseToShowLess)
                            : intl.formatMessage(ExtendedAgentsGraphResources.expandToShowAllTools, { count: toolCount })}
                    </Link>
                </div>
            )}
        </div>
    );
};

Toolbox.displayName = 'Toolbox';

export const ExpandedToolboxCard: FC<Omit<ToolboxProps, 'expanded'>> = props => {
    return <Toolbox {...props} expanded={true} />;
};

ExpandedToolboxCard.displayName = 'ExpandedToolboxCard';

export const CollapsedToolboxCard: FC<Omit<ToolboxProps, 'expanded'>> = props => {
    return <Toolbox {...props} expanded={false} />;
};

CollapsedToolboxCard.displayName = 'CollapsedToolboxCard';
