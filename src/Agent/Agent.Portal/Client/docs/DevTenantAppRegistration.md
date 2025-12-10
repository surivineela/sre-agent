# Dev tenant app registration

Just throwing this here in case we ever have to re-create

## API perms

- Azure Service Management - user_impersonation
- Microsoft.Graph - offline_access, openid, profile, User.Read
- Application Insights API - Data.Read
- Azure SRE Agent - Threads.ReadWrite.All

## Manifest

``` json
{
 "id": "458cc2b3-e38d-416f-87ec-401267665c0a",
 "deletedDateTime": null,
 "appId": "875ae537-dc31-49a7-851d-fbc362cd44a9",
 "applicationTemplateId": null,
 "disabledByMicrosoftStatus": null,
 "createdDateTime": "2025-12-10T16:15:59Z",
 "displayName": "sre-agent-portal-dev",
 "description": null,
 "groupMembershipClaims": null,
 "identifierUris": [],
 "isDeviceOnlyAuthSupported": null,
 "isFallbackPublicClient": null,
 "nativeAuthenticationApisEnabled": null,
 "notes": null,
 "publisherDomain": "appserviceux1.onmicrosoft.com",
 "serviceManagementReference": null,
 "signInAudience": "AzureADMyOrg",
 "tags": [],
 "tokenEncryptionKeyId": null,
 "samlMetadataUrl": null,
 "defaultRedirectUri": null,
 "certification": null,
 "optionalClaims": null,
 "requestSignatureVerification": null,
 "addIns": [],
 "api": {
  "acceptMappedClaims": null,
  "knownClientApplications": [],
  "requestedAccessTokenVersion": null,
  "oauth2PermissionScopes": [],
  "preAuthorizedApplications": []
 },
 "appRoles": [],
 "info": {
  "logoUrl": null,
  "marketingUrl": null,
  "privacyStatementUrl": null,
  "supportUrl": null,
  "termsOfServiceUrl": null
 },
 "keyCredentials": [],
 "parentalControlSettings": {
  "countriesBlockedForMinors": [],
  "legalAgeGroupRule": "Allow"
 },
 "passwordCredentials": [],
 "publicClient": {
  "redirectUris": []
 },
 "requiredResourceAccess": [
  {
   "resourceAppId": "00000003-0000-0000-c000-000000000000",
   "resourceAccess": [
    {
     "id": "e1fe6dd8-ba31-4d61-89e7-88639da4683d",
     "type": "Scope"
    },
    {
     "id": "37f7f235-527c-4136-accd-4a02d197296e",
     "type": "Scope"
    },
    {
     "id": "14dad69e-099b-42c9-810b-d002981feec1",
     "type": "Scope"
    },
    {
     "id": "7427e0e9-2fba-42fe-b0c0-848c9e6a8182",
     "type": "Scope"
    }
   ]
  },
  {
   "resourceAppId": "797f4846-ba00-4fd7-ba43-dac1f8f63013",
   "resourceAccess": [
    {
     "id": "41094075-9dad-400e-a0bd-54e686782033",
     "type": "Scope"
    }
   ]
  }
 ],
 "verifiedPublisher": {
  "displayName": null,
  "verifiedPublisherId": null,
  "addedDateTime": null
 },
 "web": {
  "homePageUrl": null,
  "logoutUrl": "https://localhost:5174/auth/callback",
  "redirectUris": [],
  "implicitGrantSettings": {
   "enableAccessTokenIssuance": false,
   "enableIdTokenIssuance": false
  },
  "redirectUriSettings": []
 },
 "servicePrincipalLockConfiguration": {
  "isEnabled": true,
  "allProperties": true,
  "credentialsWithUsageVerify": true,
  "credentialsWithUsageSign": true,
  "identifierUris": false,
  "tokenEncryptionKeyId": true
 },
 "spa": {
  "redirectUris": [
   "https://localhost:5174/auth/callback"
  ]
 }
}
```
