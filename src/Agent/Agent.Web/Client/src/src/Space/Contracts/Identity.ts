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
  
  export type FederatedCredentials = {
    issuer?: string;
    subject?: string;
    audiences?: string[];
  };
  
  export enum IdentityType {
    systemAssigned = 'SystemAssigned',
    userAssigned = 'UserAssigned',
  }
  
  export enum IdentityKeys {
    system = 'system',
    addIdentity = '_ADD_IDENTITY_',
  }
  