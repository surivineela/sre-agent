export type Identity = {
    type?: string;
    tenantId?: string;
    principalId?: string;
    userAssignedIdentities?: UserAssignedIdentity;
};

export type UserAssignedIdentity = Record<string, UserAssignedIdentityDetails>;

export type UserAssignedIdentityDetails = {
    principalId?: string;
    clientId?: string;
    tenantId?: string;
};
