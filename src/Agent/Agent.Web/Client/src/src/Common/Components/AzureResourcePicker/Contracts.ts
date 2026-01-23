import React from 'react';

export interface BaseAzureResource {
    id: string;
    name: string;
    type?: string;
}

export interface AzureResourceWithPermission extends BaseAzureResource {
    myRole: string | null;
    canAssignRoles: boolean;
    /** Whether this resource is recommended (has SRE Agent-compatible resources) */
    recommended: boolean;
    selected: boolean;
}

export interface SubscriptionWithPermission extends AzureResourceWithPermission {
    subscriptionId: string;
    displayName: string;
    state: string;
    tenantId: string;
}

export interface ResourceGroupWithPermission extends AzureResourceWithPermission {
    location: string;
    /** Extracted from id */
    subscriptionId: string;
    properties?: {
        provisioningState: string;
    };
    tags?: Record<string, string>;
    managedBy?: string;
}

export interface AzureResourcePickerDialogProps {
    isOpen: boolean;
    onDismiss: () => void;
    onApply: (selectedIds: string[]) => void;
    title: string;
    description?: string;
    infoMessage?: string;
    showRecommendedLabel: string;
    showRecommendedTooltip: string;
    disabledSectionMessage: string;
    addButtonLabel: string;
    isLoading: boolean;
    /** Initial selected IDs */
    selectedIds: string[];
    /** For rendering the content */
    children: React.ReactNode;
}

export const RoleDefinitionIds = {
    owner: '8e3af657-a8ff-443c-a75c-2fe8c4bcb635',
    contributor: 'b24988ac-6180-42a0-ab88-20f7382dd24c',
    reader: 'acdd72a7-3385-48ef-bd42-f606fba81ae7',
    userAccessAdministrator: '18d7d88d-d35e-4fb5-a5c3-7773c20a72d9',
} as const;

export const RoleDisplayNames: Record<string, string> = {
    [RoleDefinitionIds.owner]: 'Owner',
    [RoleDefinitionIds.contributor]: 'Contributor',
    [RoleDefinitionIds.reader]: 'Reader',
    [RoleDefinitionIds.userAccessAdministrator]: 'User Access Administrator',
};

export const getRoleDisplayName = (roleId: string): string | null => {
    return RoleDisplayNames[roleId] ?? null;
};

export const canRoleAssignRoles = (roleId: string): boolean => {
    return roleId === RoleDefinitionIds.owner || roleId === RoleDefinitionIds.userAccessAdministrator;
};
