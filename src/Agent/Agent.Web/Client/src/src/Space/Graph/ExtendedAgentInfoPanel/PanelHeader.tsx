import { Button, Caption1, Menu, MenuButton, MenuItem, MenuList, MenuPopover, MenuTrigger, Text } from '@fluentui/react-components';
import {
    Beaker20Regular,
    Delete20Regular,
    Dismiss20Regular,
    Edit20Regular,
    MoreHorizontal20Regular,
    PanelRightContractRegular,
} from '@fluentui/react-icons';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, PlaygroundResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedConnector, ExtendedTool, ExtendedTrigger } from '../../Contracts/ExtendedAgentGraph';
import { PlaygroundTarget } from '../../Playground/PlaygroundModal';
import { ConnectorType, getConnectorIcon, getConnectorName } from '../../Settings/Connectors/Wizard/Common/ConnectorType';
import { useExtendedAgentInfoStyles } from '../../Styles/ExtendedAgentGraph.styles';
import { EntityIcon, EntityIconType } from '../EntityIcon';
import { ExtendedEntityType } from '../ExtendedAgentYamlUtils';

type HeaderEditContext = {
    entity: ExtendedAgent | ExtendedTool | ExtendedConnector | ExtendedTrigger;
    type: ExtendedEntityType;
};

type PanelHeaderProps = {
    headerIconType?: EntityIconType;
    headerTitle: string;
    headerSubtitle?: string;
    headerEditContext?: HeaderEditContext;
    playgroundTarget?: PlaygroundTarget;
    showAgentBuilderPlayground: boolean;
    isAgentContext: boolean;
    selectedAgent?: ExtendedAgent;
    selectedTool?: ExtendedTool;
    selectedConnector?: ExtendedConnector;
    isDeleting: boolean;
    onEdit: (entity: ExtendedTool | ExtendedConnector | ExtendedTrigger | ExtendedAgent | undefined, type: ExtendedEntityType) => void;
    onDeleteClick: (type: 'agent' | 'tool', entity: ExtendedAgent | ExtendedTool | undefined) => void;
    onOpenPlaygroundClick: () => void;
    onClose?: () => void;
    onDragHandlePointerDown?: (event: React.PointerEvent<HTMLDivElement>) => void;
    collapsibleProps?: {
        isCollapsed: boolean;
        setCollapsed: (collapsed: boolean) => void;
    };
};

export const PanelHeader = memo(
    ({
        headerIconType,
        headerTitle,
        headerSubtitle,
        headerEditContext,
        playgroundTarget,
        showAgentBuilderPlayground,
        isAgentContext,
        selectedAgent,
        selectedTool,
        selectedConnector,
        isDeleting,
        onEdit,
        onDeleteClick,
        onOpenPlaygroundClick,
        onClose,
        onDragHandlePointerDown,
        collapsibleProps,
    }: PanelHeaderProps) => {
        const styles = useExtendedAgentInfoStyles();
        const intl = useIntl();

        return (
            <div className={styles.header}>
                <div
                    className={styles.headerInfo}
                    onPointerDown={event => {
                        if (event.button !== 0) return;
                        onDragHandlePointerDown?.(event);
                    }}
                >
                    <div className={styles.headerIconAndText}>
                        {headerIconType && (
                            <div className={styles.flexShrinkNone}>
                                {selectedConnector?.connectorType === ConnectorType.McpServer ? (
                                    <img
                                        style={{ height: '40px', width: '40px', borderRadius: '8px' }}
                                        src={getConnectorIcon(selectedConnector?.connectorType as ConnectorType, intl)}
                                        alt={getConnectorName(selectedConnector?.connectorType as ConnectorType, intl)}
                                    />
                                ) : (
                                    <EntityIcon type={headerIconType} shorthandStyle={{ wrapperSize: 40, iconSize: 28, borderRadius: 8 }} />
                                )}
                            </div>
                        )}
                        <div className={styles.headerTitleAndSubtitle}>
                            <Text weight="semibold" size={500} className={styles.headerTitleText}>
                                {headerTitle}
                            </Text>
                            {headerSubtitle && <Caption1>{headerSubtitle}</Caption1>}
                        </div>
                    </div>
                </div>
                <div className={styles.flexRowCenter4}>
                    {headerEditContext && headerEditContext.type !== 'connector' && (
                        <Button
                            appearance="subtle"
                            size="small"
                            icon={<Edit20Regular />}
                            onClick={() => onEdit(headerEditContext.entity, headerEditContext.type)}
                            title={intl.formatMessage(ExtendedAgentsGraphResources.yamlOpenButton)}
                        />
                    )}
                    {((playgroundTarget && showAgentBuilderPlayground) ||
                        (headerEditContext?.type === 'agent' && isAgentContext && selectedAgent) ||
                        (headerEditContext?.type === 'tool' && selectedTool)) && (
                        <Menu>
                            <MenuTrigger disableButtonEnhancement>
                                <MenuButton appearance="subtle" size="small" icon={<MoreHorizontal20Regular />} />
                            </MenuTrigger>
                            <MenuPopover>
                                <MenuList>
                                    {showAgentBuilderPlayground && playgroundTarget && (
                                        <MenuItem icon={<Beaker20Regular />} onClick={onOpenPlaygroundClick}>
                                            {intl.formatMessage(PlaygroundResources.openPlaygroundButton)}
                                        </MenuItem>
                                    )}
                                    {headerEditContext?.type === 'agent' && isAgentContext && selectedAgent && (
                                        <MenuItem
                                            icon={<Delete20Regular />}
                                            onClick={() => onDeleteClick('agent', selectedAgent)}
                                            disabled={isDeleting}
                                        >
                                            {intl.formatMessage(SreAgentResources.deleteSubagentTitle)}
                                        </MenuItem>
                                    )}
                                    {headerEditContext?.type === 'tool' && selectedTool && (
                                        <MenuItem
                                            icon={<Delete20Regular />}
                                            onClick={() => onDeleteClick('tool', selectedTool)}
                                            disabled={isDeleting}
                                        >
                                            {intl.formatMessage(SreAgentResources.deleteToolTitle)}
                                        </MenuItem>
                                    )}
                                </MenuList>
                            </MenuPopover>
                        </Menu>
                    )}
                    {onClose && (
                        <Button
                            appearance="subtle"
                            size="small"
                            icon={<Dismiss20Regular />}
                            onClick={onClose}
                            title={intl.formatMessage(SreAgentResources.closePanel)}
                            aria-label={intl.formatMessage(SreAgentResources.closePanel)}
                        />
                    )}
                    {collapsibleProps && (
                        <Button
                            appearance="subtle"
                            size="small"
                            icon={<PanelRightContractRegular />}
                            onClick={() => collapsibleProps.setCollapsed(true)}
                            title={intl.formatMessage(SreAgentResources.collapsePanel)}
                            aria-label={intl.formatMessage(SreAgentResources.collapsePanel)}
                        />
                    )}
                </div>
            </div>
        );
    }
);

PanelHeader.displayName = 'PanelHeader';
