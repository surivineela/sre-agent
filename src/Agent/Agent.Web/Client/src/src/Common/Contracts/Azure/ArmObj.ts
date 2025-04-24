import { KeyValue } from "../KeyValue";

export interface MsiIdentity {
  principalId: string;
  tenantId: string;
  type: string;
  userAssignedIdentities: KeyValue<KeyValue<string>>;
}
export interface ArmObj<T> {
  id: string;
  kind?: string;
  properties: T;
  type?: string;
  tags?: KeyValue<string>;
  location: string;
  name: string;
  identity?: MsiIdentity;
  sku?: ArmSku;
}

export interface ArmArray<T> {
  value: ArmObj<T>[];
  nextLink?: string | null;
  id?: string;
}

export interface ArmSku {
  name: string;
  tier: string;
  size: string;
  family: string;
  capacity: string;
}

export interface Identity {
  principalId: string;
  tenantId: string;
  type: string;
  userAssignedIdentities: KeyValue<KeyValue<string>>;
}