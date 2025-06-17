import { CheckboxVisibility, ConstrainMode, DetailsListLayoutMode, IColumn, SelectionMode } from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { useIntl } from 'react-intl';
import React, { useCallback, useMemo } from 'react';
import { AgentMode } from '../../Common/Contracts/Azure/SreAgent';
import { PermissionsResources } from '../../Strings/SREAgentResources';
import { useManagedResourcesStyles } from './Styles/ManagedResources.styles';

export enum AgentRoles {
    contributor = 'contributor',
    containerAppsContributor = 'containerAppsContributor',
    logAnalyticsReader = 'logAnalyticsReader',
    websitesContributor = 'websitesContributor',
    webPlanContributor = 'webPlanContributor',
    reader = 'reader',
    containerAppsOperator = 'containerAppsOperator',
    azureKubernetesServiceClusterAdmin = 'azureKubernetesServiceClusterAdmin',
    azureKubernetesServiceRbacClusterAdmin = 'azureKubernetesServiceRbacClusterAdmin',
    azureKubernetesServiceRbacReader = 'azureKubernetesServiceRbacReader',
    azureKubernetesServiceClusterUser = 'azureKubernetesServiceClusterUser',
    azureMonitorMonitoringContributor = 'azureMonitorMonitoringContributor',
    applicationInsightsComponentContributor = 'applicationInsightsComponentContributor',
    logAnalyticsContributor = 'logAnalyticsContributor',
    storageBlobDataContributor = 'storageBlobDataContributor',
    documentDbAccountContributor = 'documentDbAccountContributor',
    postgreSqlContributor = 'postgreSqlContributor',
    redisCacheContributor = 'redisCacheContributor',
    sqlDbContributor = 'sqlDbContributor',
}

enum RoleListColumnKey {
    role = 'role',
    description = 'description',
}

interface RoleGridItem {
    role: string;
    title: string;
    description: string;
}

interface PermissionsDetailsListProps {
    mode: string;
};

const PermissionsDetailsList: React.FC<PermissionsDetailsListProps> = ({ mode }) => {
    const styles = useManagedResourcesStyles();
    const intl = useIntl();

    const coreRoles = useMemo(() => [
        {
            role: AgentRoles.azureMonitorMonitoringContributor,
            title: intl.formatMessage(PermissionsResources.azureMonitorMonitoringContributor),
            description: intl.formatMessage(PermissionsResources.azureMonitorMonitoringContributorDescription),
        },
        {
            role: AgentRoles.applicationInsightsComponentContributor,
            title: intl.formatMessage(PermissionsResources.applicationInsightsComponentContributor),
            description: intl.formatMessage(PermissionsResources.applicationInsightsComponentContributorDescription),
        },
        {
            role: AgentRoles.logAnalyticsContributor,
            title: intl.formatMessage(PermissionsResources.logAnalyticsContributor),
            description: intl.formatMessage(PermissionsResources.logAnalyticsContributorDescription),
        },
        {
            role: AgentRoles.websitesContributor,
            title: intl.formatMessage(PermissionsResources.websitesContributor),
            description: intl.formatMessage(PermissionsResources.websitesContributorDescription),
        },
        {
            role: AgentRoles.redisCacheContributor,
            title: intl.formatMessage(PermissionsResources.redisCacheContributor),
            description: intl.formatMessage(PermissionsResources.redisCacheContributorDescription),
        },
        {
            role: AgentRoles.sqlDbContributor,
            title: intl.formatMessage(PermissionsResources.sqlDbContributor),
            description: intl.formatMessage(PermissionsResources.sqlDbContributorDescription),
        },
        {
            role: AgentRoles.storageBlobDataContributor,
            title: intl.formatMessage(PermissionsResources.storageBlobDataContributor),
            description: intl.formatMessage(PermissionsResources.storageBlobDataContributorDescription),
        },
        {
            role: AgentRoles.documentDbAccountContributor,
            title: intl.formatMessage(PermissionsResources.documentDbAccountContributor),
            description: intl.formatMessage(PermissionsResources.documentDbAccountContributorDescription),
        },
    ], [intl]);

    const readOnlyRoles = useMemo(() => [
        { role: AgentRoles.reader, title: intl.formatMessage(PermissionsResources.reader), description: intl.formatMessage(PermissionsResources.readerDescription) },
        {
            role: AgentRoles.containerAppsOperator,
            title: intl.formatMessage(PermissionsResources.containerAppsOperator),
            description: intl.formatMessage(PermissionsResources.containerAppsOperatorDescription),
        },
        {
            role: AgentRoles.azureKubernetesServiceRbacReader,
            title: intl.formatMessage(PermissionsResources.azureKubernetesServiceRbacReader),
            description: intl.formatMessage(PermissionsResources.azureKubernetesServiceRbacReaderDescription),
        },
        {
            role: AgentRoles.azureKubernetesServiceClusterUser,
            title: intl.formatMessage(PermissionsResources.azureKubernetesServiceClusterUserRole),
            description: intl.formatMessage(PermissionsResources.azureKubernetesServiceClusterUserRoleDescription),
        },
        ...coreRoles,
    ], [coreRoles, intl]);

    const reviewAndAutonomousRoles = useMemo(() => [
        { role: AgentRoles.contributor, title: intl.formatMessage(PermissionsResources.contributor), description: intl.formatMessage(PermissionsResources.contributorDescription) },
        {
            role: AgentRoles.containerAppsContributor,
            title: intl.formatMessage(PermissionsResources.containerAppsContributor),
            description: intl.formatMessage(PermissionsResources.containerAppsContributorDescription),
        },
        {
            role: AgentRoles.azureKubernetesServiceClusterAdmin,
            title: intl.formatMessage(PermissionsResources.azureKubernetesServiceClusterAdmin),
            description: intl.formatMessage(PermissionsResources.azureKubernetesServiceClusterAdminDescription),
        },
        {
            role: AgentRoles.azureKubernetesServiceRbacClusterAdmin,
            title: intl.formatMessage(PermissionsResources.azureKubernetesServiceRbacClusterAdmin),
            description: intl.formatMessage(PermissionsResources.azureKubernetesServiceRbacClusterAdminDescription),
        },
        {
            role: AgentRoles.containerAppsContributor,
            title: intl.formatMessage(PermissionsResources.containerAppsContributor),
            description: intl.formatMessage(PermissionsResources.containerAppsContributorDescription),
        },
        {
            role: AgentRoles.webPlanContributor,
            title: intl.formatMessage(PermissionsResources.webPlanContributor),
            description: intl.formatMessage(PermissionsResources.webPlanContributorDescription),
        },
        ...coreRoles,
    ], [coreRoles, intl]);

    const permissionsGridItems = useMemo(() => {
        const lowercaseMode = mode.toLowerCase();
        switch (lowercaseMode) {
            case AgentMode.autonomous:
            case AgentMode.review:
                return reviewAndAutonomousRoles;
            case AgentMode.readonly:
            default:
                return readOnlyRoles;
        }
    }, [mode, readOnlyRoles, reviewAndAutonomousRoles]);

    const rolesDescription = useMemo(() => {
        const lowercaseMode = mode.toLowerCase();
        switch (lowercaseMode) {
            case AgentMode.autonomous:
                return intl.formatMessage(PermissionsResources.autonomousModeDescription);
            case AgentMode.review:
                return intl.formatMessage(PermissionsResources.reviewModeDescription);
            case AgentMode.readonly:
            default:
                return intl.formatMessage(PermissionsResources.readOnlyModeDescription);
        }
    }, [mode, intl]);

    const onRenderRoles = useCallback(
        (item: RoleGridItem) => {
            return <div className={styles.detailsListRow}>{item.title}</div>;
        },
        [styles.detailsListRow]
    );

    const onRenderDescription = useCallback(
        (item: RoleGridItem) => {
            return <div className={styles.detailsListRow}>{item.description}</div>;
        },
        [styles.detailsListRow]
    );

    const columns = React.useMemo<IColumn[]>(() => {
        return [
            {
                key: RoleListColumnKey.role,
                name: intl.formatMessage(PermissionsResources.roles),
                fieldName: RoleListColumnKey.role,
                minWidth: 200,
                maxWidth: 200,
                isResizable: true,
                isMultiline: true,
                onRender: onRenderRoles,
            },
            {
                key: RoleListColumnKey.description,
                name: intl.formatMessage(PermissionsResources.description),
                fieldName: RoleListColumnKey.description,
                minWidth: 300,
                maxWidth: 500,
                isResizable: true,
                isMultiline: true,
                onRender: onRenderDescription,
            },
        ];
    }, [intl, onRenderRoles, onRenderDescription]);

    return (
        <div>
            <div style={{paddingBottom: '10px', paddingTop: '10px'}}>{rolesDescription}</div>
            <div style={{ minHeight: '440px', maxHeight: '440px', overflowY: 'scroll' }} data-is-scrollable="true">
                <ShimmeredDetailsList
                    compact={true}
                    selectionMode={SelectionMode.none}
                    columns={columns}
                    constrainMode={ConstrainMode.horizontalConstrained}
                    items={permissionsGridItems}
                    layoutMode={DetailsListLayoutMode.justified}
                    enableShimmer={false}
                    checkboxVisibility={CheckboxVisibility.hidden}
                />
            </div>
        </div>
    );
};

export default PermissionsDetailsList;
