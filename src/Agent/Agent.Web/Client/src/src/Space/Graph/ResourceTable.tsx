import {
    Button,
    Spinner,
    Table,
    TableBody,
    TableCell,
    TableCellLayout,
    TableHeader,
    TableHeaderCell,
    TableRow,
} from '@fluentui/react-components';
import { ChevronDownRegular, ChevronRightRegular } from '@fluentui/react-icons';
import * as React from 'react';
import { useIntl } from 'react-intl';
import { getResourceTypeFriendlyName } from '../../Common/Helpers/Resources';
import { ComponentResources, GraphResources } from '../../Strings/SREAgentResources';
import { ResourceExtended } from '../Contracts/Graph';

type ChildResource = {
    name: string;
    resourceType: string;
    repoConnection: string | React.ReactElement;
    icon?: string;
};

type Resource = {
    name: string;
    childResources: ChildResource[];
    isLoading?: boolean;
};

type ResourcesTableProps = {
    resources: Resource[];
    onLoadAppGroupResources?: (appGroupId: string) => Promise<void>;
    appGroups?: ResourceExtended[];
};

export const ResourcesTable: React.FC<ResourcesTableProps> = ({ resources, onLoadAppGroupResources, appGroups = [] }) => {
    const [expanded, setExpanded] = React.useState<Record<string, boolean>>({});
    const intl = useIntl();

    const toggleExpand = async (name: string) => {
        const isCurrentlyExpanded = expanded[name] ?? false;

        if (!isCurrentlyExpanded && onLoadAppGroupResources) {
            const appGroup = appGroups.find(ag => ag.name === name);
            if (appGroup) {
                await onLoadAppGroupResources(appGroup.id);
            }
        }

        setExpanded(prev => ({ ...prev, [name]: !prev[name] }));
    };

    return (
        <Table>
            <TableHeader>
                <TableRow>
                    <TableHeaderCell>{intl.formatMessage(GraphResources.tableHeaderName)}</TableHeaderCell>
                    <TableHeaderCell>{intl.formatMessage(GraphResources.tableHeaderResourceType)}</TableHeaderCell>
                    <TableHeaderCell>{intl.formatMessage(GraphResources.tableHeaderRepositoryConnection)}</TableHeaderCell>
                </TableRow>
            </TableHeader>
            <TableBody>
                {resources.map(resource => {
                    const isExpanded = expanded[resource.name] ?? false;
                    const rows = [
                        <TableRow key={resource.name}>
                            <TableCell>
                                <TableCellLayout
                                    media={
                                        <Button
                                            appearance="subtle"
                                            size="small"
                                            icon={isExpanded ? <ChevronDownRegular /> : <ChevronRightRegular />}
                                            onClick={() => toggleExpand(resource.name)}
                                        />
                                    }
                                >
                                    <strong>{resource.name}</strong> {isExpanded ? `(${resource.childResources.length})` : ''}
                                </TableCellLayout>
                            </TableCell>
                            <TableCell></TableCell>
                            <TableCell></TableCell>
                        </TableRow>,
                    ];

                    if (isExpanded) {
                        if (resource.isLoading) {
                            rows.push(
                                <TableRow key={`${resource.name}-loading`}>
                                    <TableCell>
                                        <TableCellLayout>
                                            <span style={{ paddingLeft: 24 }}>
                                                <Spinner size="tiny" style={{ marginRight: 8 }} />
                                                {intl.formatMessage(ComponentResources.loading)}
                                            </span>
                                        </TableCellLayout>
                                    </TableCell>
                                    <TableCell></TableCell>
                                    <TableCell></TableCell>
                                </TableRow>
                            );
                        } else {
                            rows.push(
                                ...resource.childResources.map((child, idx) => (
                                    <TableRow key={`${resource.name}-${idx}`}>
                                        <TableCell>
                                            <TableCellLayout>
                                                <div style={{ paddingLeft: '32px', display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                    {child.icon && (
                                                        <img src={child.icon} alt={child.resourceType} style={{ width: 16, height: 16 }} />
                                                    )}
                                                    {child.name}
                                                </div>
                                            </TableCellLayout>
                                        </TableCell>
                                        <TableCell>{getResourceTypeFriendlyName(child.resourceType)}</TableCell>
                                        <TableCell>{child.repoConnection}</TableCell>
                                    </TableRow>
                                ))
                            );
                        }
                    }

                    return rows;
                })}
            </TableBody>
        </Table>
    );
};
