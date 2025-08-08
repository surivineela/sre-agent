import { Link, SearchBox, tokens } from '@fluentui/react-components';
import * as React from 'react';
import { useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { isPaasResourceType, resolveResourceIcon } from '../../Common/Helpers/Resources';
import { ComponentResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { Resource, ResourceExtended } from '../Contracts/Graph';
import { ConnectRepositoryLink, getRepoIcon } from './RepositoryConnectionDialog';
import { ResourcesTable } from './ResourceTable';

interface GraphGridViewProps {
    resources?: Resource[];
    selectedAppGroup?: ResourceExtended;
    appGroups?: ResourceExtended[];
    resourceGroups?: string[];
    onLoadAppGroupResources?: (appGroupId: string) => Promise<{ resources: Resource[] }>;
}

const getRepositoryConnection = (resource?: Resource, appGroup?: ResourceExtended, intl?: any) => {
    const extendedResource = resource as unknown as ResourceExtended;
    let sourceCodeStatus = extendedResource?.sourceCodeLinkageStatus;

    if (!sourceCodeStatus) {
        sourceCodeStatus = appGroup?.sourceCodeLinkageStatus;
    }

    if (sourceCodeStatus) {
        if (sourceCodeStatus.status === 'Linked' && sourceCodeStatus.repositoryUrl) {
            return (
                <Link href={sourceCodeStatus.repositoryUrl} target="_blank" rel="noopener noreferrer">
                    {getRepoIcon(sourceCodeStatus.repositoryUrl)} {sourceCodeStatus.repositoryUrl}
                </Link>
            );
        } else if (sourceCodeStatus.repositoryUrl) {
            return (
                <Link href={sourceCodeStatus.repositoryUrl} target="_blank" rel="noopener noreferrer">
                    {getRepoIcon(sourceCodeStatus.repositoryUrl)} {sourceCodeStatus.repositoryUrl}
                </Link>
            );
        } else {
            return <ConnectRepositoryLink resourceId={resource?.resourceId} />;
        }
    }

    if (isPaasResourceType(resource?.type)) {
        return <ConnectRepositoryLink resourceId={resource?.resourceId} />;
    }

    return intl?.formatMessage(SreAgentResources.NA) || 'N/A';
};

const createResourceTableRow = (resource: Resource, appGroup?: ResourceExtended, intl?: any) => ({
    name: resource.name,
    resourceType: resource.type,
    repoConnection: getRepositoryConnection(resource, appGroup, intl),
    icon: resolveResourceIcon(resource.kind || resource.type),
});

const createLoadingAppGroupRow = (appGroup: ResourceExtended, intl: any) => ({
    name: appGroup.name,
    isLoading: true,
    childResources: [
        {
            name: intl?.formatMessage(ComponentResources.loading),
            resourceType: '',
            repoConnection: '',
            icon: '',
        },
    ],
});

const getRelevantResourcesForAppGroup = (resources: Resource[], appGroup: ResourceExtended, selectedAppGroup?: ResourceExtended) => {
    const groupResources = resources.filter(resource => {
        return resource.resourceId.includes(appGroup.id) || appGroup.properties?.resourceId?.some(id => id === resource.resourceId);
    });

    return groupResources.length > 0 ? groupResources : appGroup === selectedAppGroup ? resources : [];
};

const buildLoadedAppGroupChildResources = (appGroup: ResourceExtended, appGroupResources: Resource[], intl?: any) => {
    const childResources: Array<{ name: string; resourceType: string; repoConnection: any; icon: string }> = [];

    appGroupResources.forEach(resource => {
        childResources.push(createResourceTableRow(resource, appGroup, intl));
    });

    return childResources;
};

const buildAppGroupChildResources = (appGroup: ResourceExtended, relevantResources: Resource[], intl?: any) => {
    const childResources: Array<{ name: string; resourceType: string; repoConnection: any; icon: string }> = [];

    relevantResources.forEach(resource => {
        childResources.push(createResourceTableRow(resource, appGroup, intl));
    });

    return childResources.length > 0 ? childResources : [];
};

const transformResourcesWithoutAppGroups = (resources: Resource[], intl?: any) => {
    return resources.map(resource => {
        return {
            name: resource.name,
            childResources: [createResourceTableRow(resource, undefined, intl)],
        };
    });
};

const transformAppGroup = (
    appGroup: ResourceExtended,
    resources: Resource[],
    selectedAppGroup?: ResourceExtended,
    loadingAppGroups?: Set<string>,
    loadedAppGroupData?: Map<string, { resources: Resource[] }>,
    intl?: any
) => {
    if (loadingAppGroups?.has(appGroup.id)) {
        return createLoadingAppGroupRow(appGroup, intl);
    }

    const loadedData = loadedAppGroupData?.get(appGroup.id);
    if (loadedData) {
        const { resources: appGroupResources } = loadedData;
        const childResources = buildLoadedAppGroupChildResources(appGroup, appGroupResources, intl);

        return {
            name: appGroup.name,
            childResources: childResources,
        };
    }

    const relevantResources = getRelevantResourcesForAppGroup(resources, appGroup, selectedAppGroup);
    const childResources = buildAppGroupChildResources(appGroup, relevantResources, intl);

    return {
        name: appGroup.name,
        childResources: childResources,
    };
};

const transformResourcesToTableFormat = (
    resources: Resource[],
    selectedAppGroup?: ResourceExtended,
    appGroups?: ResourceExtended[],
    loadingAppGroups?: Set<string>,
    loadedAppGroupData?: Map<string, { resources: Resource[] }>,
    intl?: any
) => {
    if (!appGroups || appGroups.length === 0) {
        return transformResourcesWithoutAppGroups(resources, intl);
    }

    return appGroups.map(appGroup => transformAppGroup(appGroup, resources, selectedAppGroup, loadingAppGroups, loadedAppGroupData, intl));
};

export const GraphGridView: React.FC<GraphGridViewProps> = ({
    resources = [],
    selectedAppGroup,
    appGroups = [],
    resourceGroups = [],
    onLoadAppGroupResources,
}) => {
    const [searchQuery, setSearchQuery] = useState('');
    const [loadingAppGroups, setLoadingAppGroups] = useState<Set<string>>(new Set());
    const [loadedAppGroupData, setLoadedAppGroupData] = useState<Map<string, { resources: Resource[] }>>(new Map());
    const intl = useIntl();

    const resourceGroupsCount = resourceGroups.length;
    const logicalAppGroupsCount = appGroups.length;

    const allTableResources = transformResourcesToTableFormat(
        resources,
        selectedAppGroup,
        appGroups,
        loadingAppGroups,
        loadedAppGroupData,
        intl
    );

    const handleLoadAppGroupResources = async (appGroupId: string) => {
        if (!onLoadAppGroupResources || loadingAppGroups.has(appGroupId) || loadedAppGroupData.has(appGroupId)) {
            return;
        }

        setLoadingAppGroups(prev => new Set([...prev, appGroupId]));

        try {
            const data = await onLoadAppGroupResources(appGroupId);
            setLoadedAppGroupData(prev => new Map([...prev, [appGroupId, data]]));
        } catch (error) {
            //Nothing to do
        } finally {
            setLoadingAppGroups(prev => {
                const newSet = new Set(prev);
                newSet.delete(appGroupId);
                return newSet;
            });
        }
    };

    const filteredTableResources = useMemo(() => {
        if (!searchQuery.trim()) {
            return allTableResources;
        }

        const query = searchQuery.toLowerCase();
        return allTableResources.filter((resource: any) => {
            if (resource.name.toLowerCase().includes(query)) {
                return true;
            }

            return resource.childResources.some(
                (child: any) => child.name.toLowerCase().includes(query) || child.resourceType.toLowerCase().includes(query)
            );
        });
    }, [allTableResources, searchQuery]);

    return (
        <div style={{ padding: '20px' }}>
            <div
                style={{
                    display: 'flex',
                    gap: '80px',
                    marginBottom: '24px',
                }}
            >
                <div>
                    <div style={{ fontSize: '14px', color: tokens.colorNeutralForeground2, marginBottom: '4px' }}>
                        {intl.formatMessage(SreAgentResources.resourceGroups)}
                    </div>
                    <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{resourceGroupsCount}</div>
                </div>
                <div>
                    <div style={{ fontSize: '14px', color: tokens.colorNeutralForeground2, marginBottom: '4px' }}>
                        {intl.formatMessage(SreAgentResources.coreApplicationGroups)}
                    </div>
                    <div style={{ fontSize: '24px', fontWeight: 'bold' }}>{logicalAppGroupsCount}</div>
                </div>
            </div>

            <div style={{ marginBottom: '16px' }}>
                <SearchBox
                    placeholder={`${intl.formatMessage(SreAgentResources.search)}...`}
                    value={searchQuery}
                    onChange={(_, data) => setSearchQuery(data.value)}
                    style={{ minWidth: '330px' }}
                />
            </div>
            <ResourcesTable
                resources={filteredTableResources}
                onLoadAppGroupResources={handleLoadAppGroupResources}
                appGroups={appGroups}
            />
        </div>
    );
};

export default GraphGridView;
