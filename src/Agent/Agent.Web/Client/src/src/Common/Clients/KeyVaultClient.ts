import { ApiVersions } from '../ApiVersions';
import MakeArmCall, { ARGRequestContent, ARGResponse } from './ArmClient';

export interface KeyVault {
    id: string;
    name: string;
    location: string;
    subscriptionId: string;
    resourceGroup: string;
    vaultUri: string;
}

export interface KeyVaultCertificate {
    id: string;
    name: string;
    attributes: {
        enabled: boolean;
        created: number;
        updated: number;
        expires?: number;
    };
}

export class KeyVaultClient {
    /**
     * Fetches all Key Vaults across provided subscriptions using Azure Resource Graph
     */
    public static getKeyVaultsFromArg(
        subscriptions: string[],
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ): Promise<KeyVault[]> {
        const query = `
            resources
            | where type == "microsoft.keyvault/vaults"
            | project id, name, location, subscriptionId, resourceGroup, properties.vaultUri
        `;

        const content: ARGRequestContent = {
            query,
            subscriptions,
        };

        return MakeArmCall<ARGResponse, ARGRequestContent>({
            method: 'POST',
            url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
            body: content,
            commandName: 'GetKeyVaultsFromArg',
        }).then((response: any) => {
            if (response?.data?.data?.rows) {
                return response.data.data.rows.map((row: any[]) => ({
                    id: row[0],
                    name: row[1],
                    location: row[2],
                    subscriptionId: row[3],
                    resourceGroup: row[4],
                    vaultUri: row[5],
                }));
            }
            return [];
        });
    }
}
